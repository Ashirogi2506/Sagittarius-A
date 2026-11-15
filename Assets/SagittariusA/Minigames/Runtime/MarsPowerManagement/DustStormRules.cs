using System;
using System.Collections.Generic;
using System.Linq;

namespace SagittariusA.Minigames.MarsPowerManagement
{
    public enum SystemCategory { Essential, Mission, Optional }

    public sealed class StormSystem
    {
        public string Id, Label; public SystemCategory Category; public float DemandKw; public bool On = true;
        public float OffStreak;      // seconds continuously off (essential tracking)
        public float TotalOff;       // seconds off in total
    }

    // Self-contained display values for the dust storm event. Not the mission simulator's power model.
    public sealed class DustStormRules
    {
        public const float Duration = 45f;
        public const float BatteryCapacityKwh = 10f;       // display-only units for the meter
        public float BatteryPercent { get; private set; } = 70f;
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public MinigameOutcome? Outcome { get; private set; }
        public string FailureReason { get; private set; }
        public int Mistakes { get; private set; }           // essential switch-offs
        public float EssentialTolerance { get; }
        public float ScienceOnSeconds { get; private set; }
        public IReadOnlyList<StormSystem> Systems => systems;
        private readonly List<StormSystem> systems;
        private readonly (float t, float kw)[] curve;
        private const float DrainPerKwSecond = 0.55f;       // % per kW of deficit per second
        private const float ChargePerKwSecond = 0.30f;      // % per kW of surplus per second
        private const float Step = 0.1f;
        private float accumulator;

        public DustStormRules(MinigameContext context)
        {
            var rng = MinigameRandom.Create(context, "mars-dust-storm");
            int level = MinigameDifficulty.Clamp(context?.DifficultyLevel ?? 1);
            EssentialTolerance = level == 1 ? 3f : level == 2 ? 2.5f : 2f;
            systems = new List<StormSystem> {
                new StormSystem { Id = "comms", Label = "Communications", Category = SystemCategory.Essential, DemandKw = 2 },
                new StormSystem { Id = "thermal", Label = "Thermal control", Category = SystemCategory.Essential, DemandKw = 3 },
                new StormSystem { Id = "computer", Label = "Navigation computer", Category = SystemCategory.Essential, DemandKw = 2 },
                new StormSystem { Id = "science", Label = "Science instrument", Category = SystemCategory.Mission, DemandKw = 3 },
                new StormSystem { Id = "heater", Label = "Sample heater", Category = SystemCategory.Mission, DemandKw = 2 },
                new StormSystem { Id = "lights", Label = "Auxiliary lighting", Category = SystemCategory.Optional, DemandKw = 1 },
            };
            // Storm sequence: clear -> first dip -> partial recovery -> deeper dip -> clearing. Seed shifts timing, difficulty deepens dips.
            float shift = (float)rng.NextDouble() * 4f - 2f, depth = (level - 1) * 0.8f + (float)rng.NextDouble() * 0.6f;
            curve = new[] {
                (0f, 14f), (6f + shift, 13f), (11f + shift, 6.5f - depth), (17f + shift, 6f - depth),
                (22f + shift, 10.5f - depth * .5f), (26f + shift, 10f - depth * .5f), (30f + shift, 5f - depth), (36f + shift, 5.5f - depth),
                (41f, 10f), (45f, 12f) };
        }

        public float AvailableKw => SolarAt(Elapsed);
        public float DemandKw => systems.Where(s => s.On).Sum(s => s.DemandKw);
        public float EssentialDemandKw => systems.Where(s => s.Category == SystemCategory.Essential).Sum(s => s.DemandKw);
        public float StormProgress => Math.Min(1f, Elapsed / Duration);
        public float ScienceRatio => Elapsed <= 0 ? 1f : ScienceOnSeconds / Elapsed;

        public float SolarAt(float t)
        {
            if (t <= curve[0].t) return curve[0].kw;
            for (int i = 1; i < curve.Length; i++)
                if (t <= curve[i].t) { var a = curve[i - 1]; var b = curve[i]; float k = (t - a.t) / Math.Max(0.001f, b.t - a.t); return a.kw + (b.kw - a.kw) * k; }
            return curve[curve.Length - 1].kw;
        }

        public StormSystem Find(string id) => systems.FirstOrDefault(s => s.Id == id);

        public bool SetSystem(string id, bool on)
        {
            var s = Find(id);
            if (s == null || Finished || s.On == on) return false;
            s.On = on;
            if (!on && s.Category == SystemCategory.Essential) Mistakes++;
            if (on) s.OffStreak = 0;
            return true;
        }

        // Fixed-step simulation so the same inputs give the same storm regardless of frame rate. Returns true when the task ends.
        public bool Tick(float dt)
        {
            if (Finished || dt <= 0) return false;
            accumulator += dt;
            while (accumulator >= Step && !Finished) { accumulator -= Step; StepOnce(Step); }
            return Finished;
        }

        private void StepOnce(float dt)
        {
            Elapsed += dt;
            float net = SolarAt(Elapsed) - DemandKw;
            BatteryPercent += net >= 0 ? net * ChargePerKwSecond * dt : net * DrainPerKwSecond * dt;
            BatteryPercent = Math.Max(0, Math.Min(100, BatteryPercent));
            if (Find("science").On) ScienceOnSeconds += dt;
            foreach (var s in systems.Where(x => !x.On)) { s.OffStreak += dt; s.TotalOff += dt; }
            if (BatteryPercent <= 0) { End(MinigameOutcome.Failure, "The battery ran flat."); return; }
            var down = systems.FirstOrDefault(s => s.Category == SystemCategory.Essential && !s.On && s.OffStreak > EssentialTolerance);
            if (down != null) { End(MinigameOutcome.Failure, $"{down.Label} was off for more than {EssentialTolerance:0.#} seconds."); return; }
            if (Elapsed >= Duration - 1e-4f)
                End(BatteryPercent > 20f && ScienceRatio >= 0.5f ? MinigameOutcome.Success : MinigameOutcome.PartialSuccess, null);
        }
        private void End(MinigameOutcome outcome, string reason) { Finished = true; Outcome = outcome; FailureReason = reason; }

        // Player abandons mid-storm is handled as Cancelled by the framework; this is only for natural ends.
        public const string EducationalNote = "Mars dust storms can reduce sunlight, forcing solar-powered missions to conserve energy and protect essential systems.";

        public int Score
        {
            get
            {
                float s = 100 - 8 * Mistakes - Math.Max(0, 60 - BatteryPercent) * 0.4f - (1 - ScienceRatio) * 30f;
                return MinigameScoring.Clamp((int)Math.Round(s));
            }
        }

        public MinigameResult BuildResult(MinigameContext context)
        {
            var outcome = Outcome ?? MinigameOutcome.Failure;
            var r = MinigameResult.For(context, outcome, outcome switch {
                MinigameOutcome.Success => $"Storm survived with {BatteryPercent:0}% battery and science running {ScienceRatio * 100:0}% of the time.",
                MinigameOutcome.PartialSuccess => BatteryPercent <= 20f ? $"Storm survived, but the battery finished at {BatteryPercent:0}%."
                                                                        : $"Storm survived, but science ran only {ScienceRatio * 100:0}% of the time.",
                _ => FailureReason ?? "Essential systems could not be kept running.",
            });
            r.Score = outcome == MinigameOutcome.Failure ? Math.Min(Score, 35) : outcome == MinigameOutcome.PartialSuccess ? Math.Min(Score, 70) : Score;
            r.MistakeCount = Mistakes; r.CompletionTimeSeconds = Elapsed; r.CompletionPercent = StormProgress;
            r.EducationalNotes.Add(EducationalNote);
            switch (outcome)
            {
                case MinigameOutcome.Success: r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ReliabilityModifier, 0.05f, "Systems were protected through the storm.")); break;
                case MinigameOutcome.PartialSuccess: r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ScienceReturnModifier, -0.15f, "Science or battery margin was sacrificed.")); break;
                default:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ReliabilityModifier, -0.30f, "Essential systems lost power."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.MissionTimePenalty, 2f, "Recovery after the storm takes time."));
                    break;
            }
            return r;
        }
    }
}
