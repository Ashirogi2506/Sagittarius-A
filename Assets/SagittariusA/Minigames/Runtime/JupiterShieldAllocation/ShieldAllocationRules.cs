using System;
using System.Collections.Generic;
using System.Linq;

namespace SagittariusA.Minigames.JupiterShieldAllocation
{
    public enum ShieldType { Light, Medium, Heavy }
    public enum RiskLevel { Low = 1, Medium = 2, High = 3 }

    public sealed class ShieldZone
    {
        public string Id, Label; public bool Critical; public RiskLevel Risk;
        public ShieldType? Shield;
        // Science instruments tolerate one level less protection (at a science cost, see docs).
        public int RequiredProtection => Id == "science" ? Math.Max(0, (int)Risk - 1) : (int)Risk;
        public int Protection => Shield.HasValue ? ShieldAllocationRules.ProtectionOf(Shield.Value) : 0;
        public bool Protected => Protection >= RequiredProtection;
    }

    public sealed class PlaceResult { public bool Accepted; public string Message; public PlaceResult(bool a, string m) { Accepted = a; Message = m; } }

    // Rules for "Radiation Shield Allocation". The radiation pattern is seeded and always shown to the player.
    public sealed class ShieldAllocationRules
    {
        public static int ProtectionOf(ShieldType t) => t == ShieldType.Light ? 1 : t == ShieldType.Medium ? 2 : 3;
        public static int MassOf(ShieldType t) => t == ShieldType.Light ? 2 : t == ShieldType.Medium ? 3 : 5;

        public IReadOnlyList<ShieldZone> Zones => zones;
        public int MassBudget { get; }
        public float TimeLimit { get; }
        public int Mistakes { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public MinigameOutcome? Outcome { get; private set; }
        private readonly List<ShieldZone> zones;

        public ShieldAllocationRules(MinigameContext context, float timeLimitSeconds)
        {
            var rng = MinigameRandom.Create(context, "jupiter-shield");
            int level = MinigameDifficulty.Clamp(context?.DifficultyLevel ?? 1);
            TimeLimit = timeLimitSeconds;
            zones = new List<ShieldZone> {
                new ShieldZone { Id = "computer", Label = "Flight computer", Critical = true },
                new ShieldZone { Id = "comms", Label = "Communications", Critical = true },
                new ShieldZone { Id = "power", Label = "Power controller", Critical = false },
                new ShieldZone { Id = "science", Label = "Science instrument", Critical = false },
            };
            // Seeded radiation pattern: one critical zone is High, the other Medium or High; the rest vary.
            var risks = new[] { RiskLevel.High, rng.Next(2) == 0 ? RiskLevel.Medium : RiskLevel.High, (RiskLevel)(1 + rng.Next(3)), (RiskLevel)(2 + rng.Next(2)) };
            if (rng.Next(2) == 0) (risks[0], risks[1]) = (risks[1], risks[0]);
            for (int i = 0; i < zones.Count; i++) zones[i].Risk = risks[i];
            MassBudget = MinimumMass + (level == 1 ? 2 : level == 2 ? 1 : 0);
        }

        // Lightest mass that protects every zone adequately.
        public int MinimumMass => zones.Sum(z => z.RequiredProtection == 0 ? 0 : MassOf(Cheapest(z.RequiredProtection)));
        public static ShieldType Cheapest(int protection) => protection <= 1 ? ShieldType.Light : protection == 2 ? ShieldType.Medium : ShieldType.Heavy;
        public int MassUsed => zones.Where(z => z.Shield.HasValue).Sum(z => MassOf(z.Shield.Value));
        public int MassLeft => MassBudget - MassUsed;
        public ShieldZone Find(string id) => zones.FirstOrDefault(z => z.Id == id);

        // One tile per zone. Placing on an occupied zone swaps the tile and refunds the old one.
        public PlaceResult Place(string zoneId, ShieldType shield)
        {
            var zone = Find(zoneId);
            if (Finished) return new PlaceResult(false, "The layout is already locked in.");
            if (zone == null) { Mistakes++; return new PlaceResult(false, "Shields only fit on the marked system zones."); }
            int refund = zone.Shield.HasValue ? MassOf(zone.Shield.Value) : 0;
            int after = MassUsed - refund + MassOf(shield);
            if (after > MassBudget)
            {
                Mistakes++;
                return new PlaceResult(false, $"Too heavy. A {shield} shield needs {MassOf(shield)} mass units; only {MassLeft + refund} left.");
            }
            zone.Shield = shield;
            return new PlaceResult(true, zone.Protected ? $"{zone.Label} protected." : $"{zone.Label} still needs more protection.");
        }

        public bool Remove(string zoneId)
        {
            var z = Find(zoneId);
            if (Finished || z == null || !z.Shield.HasValue) return false;
            z.Shield = null; return true;
        }
        public void ResetLayout() { if (!Finished) foreach (var z in zones) z.Shield = null; }

        public float Progress => zones.Count(z => z.Protected) / (float)zones.Count;

        public MinigameOutcome Evaluate()
        {
            if (MassUsed > MassBudget) return MinigameOutcome.Failure;
            if (zones.Where(z => z.Critical).Any(z => !z.Protected)) return MinigameOutcome.Failure;
            return zones.All(z => z.Protected) ? MinigameOutcome.Success : MinigameOutcome.PartialSuccess;
        }

        public MinigameOutcome Confirm() { if (!Finished) { Finished = true; Outcome = Evaluate(); } return Outcome.Value; }

        public bool Tick(float dt)
        {
            if (Finished || dt <= 0) return false;
            Elapsed += dt;
            if (TimeLimit > 0 && Elapsed >= TimeLimit) { Elapsed = TimeLimit; Finished = true; Outcome = Evaluate(); return true; }
            return false;
        }

        public const string EducationalNote = "Radiation shielding improves survival near Jupiter, but every additional shield increases spacecraft mass.";

        public MinigameResult BuildResult(MinigameContext context)
        {
            var outcome = Outcome ?? Evaluate();
            var weak = zones.Where(z => !z.Protected).Select(z => z.Label).ToList();
            var r = MinigameResult.For(context, outcome, outcome switch {
                MinigameOutcome.Success => $"Every system is shielded using {MassUsed} of {MassBudget} mass units.",
                MinigameOutcome.PartialSuccess => $"Critical systems survive, but {string.Join(" and ", weak)} took radiation damage.",
                _ => MassUsed > MassBudget ? "The shielding exceeds the mass budget." : $"{string.Join(" and ", weak.Take(2))} were underprotected during the surge.",
            });
            int score = MinigameScoring.Standard(Mistakes, Elapsed, TimeLimit) - 5 * Math.Max(0, MassUsed - MinimumMass) / 2;
            r.Score = outcome == MinigameOutcome.Success ? MinigameScoring.Clamp(score) : outcome == MinigameOutcome.PartialSuccess ? Math.Min(MinigameScoring.Clamp(score), 70) : Math.Min(MinigameScoring.Clamp(score), 35);
            r.MistakeCount = Mistakes; r.CompletionTimeSeconds = Elapsed; r.CompletionPercent = Progress;
            r.EducationalNotes.Add(EducationalNote);
            switch (outcome)
            {
                case MinigameOutcome.Success:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.RadiationDamageModifier, -0.25f, "Vulnerable electronics are shielded."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ReliabilityModifier, 0.05f, "Fewer radiation upsets."));
                    break;
                case MinigameOutcome.PartialSuccess: r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ScienceReturnModifier, -0.15f, "A non-critical system was underprotected.")); break;
                default:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.RadiationDamageModifier, 0.35f, "Critical electronics took radiation damage."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ReliabilityModifier, -0.25f, "Higher risk of system failure."));
                    break;
            }
            return r;
        }
    }
}
