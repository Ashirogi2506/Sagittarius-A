using System;
using System.Collections.Generic;

namespace SagittariusA.Minigames
{
    public enum MinigameOutcome { Success, PartialSuccess, Failure, Cancelled }

    // What the game flow asks for. Values are copied into the result; the minigame never edits the simulation.
    [Serializable]
    public sealed class MinigameContext
    {
        public string MinigameId;
        public string MissionId;
        public string EventId;
        public int DifficultyLevel = 1;          // 1-3, clamped by MinigameDifficulty
        public int RandomSeed;
        public float TimeLimitSeconds = -1f;     // < 0 uses the definition default; 0 means no limit
        public bool AllowCancel = true;
        // Optional display-only values from the mission event (for example "orbit" = "Low Earth orbit").
        public Dictionary<string, string> DisplayValues = new Dictionary<string, string>();
    }

    // A requested consequence. The simulation adapter decides how (and whether) to apply it.
    [Serializable]
    public sealed class MinigameEffect
    {
        public string EffectType;
        public float Value;
        public string Explanation;
        public MinigameEffect() { }
        public MinigameEffect(string type, float value, string explanation) { EffectType = type; Value = value; Explanation = explanation; }
        public override string ToString() => $"{EffectType} {(Value >= 0 ? "+" : "")}{Value:0.##}";
    }

    public static class MinigameEffectTypes
    {
        public const string DataQualityModifier = "DataQualityModifier";
        public const string PowerAvailabilityModifier = "PowerAvailabilityModifier";
        public const string MissionTimePenalty = "MissionTimePenalty";
        public const string ReliabilityModifier = "ReliabilityModifier";
        public const string ScienceReturnModifier = "ScienceReturnModifier";
        public const string RadiationDamageModifier = "RadiationDamageModifier";
    }

    [Serializable]
    public sealed class MinigameResult
    {
        public string MinigameId;
        public string MissionId;
        public string EventId;
        public MinigameOutcome Outcome;
        public int Score;                       // 0-100
        public float CompletionTimeSeconds;
        public int MistakeCount;
        public float CompletionPercent;         // 0-1
        public string Summary;
        public List<string> EducationalNotes = new List<string>();
        public List<MinigameEffect> SuggestedEffects = new List<MinigameEffect>();

        public static MinigameResult For(MinigameContext context, MinigameOutcome outcome, string summary) => new MinigameResult {
            MinigameId = context?.MinigameId, MissionId = context?.MissionId, EventId = context?.EventId, Outcome = outcome, Summary = summary };

        // Enforces the documented ranges. Called by the session before the result leaves the framework.
        public MinigameResult Normalize()
        {
            Score = MinigameScoring.Clamp(Score);
            CompletionPercent = CompletionPercent < 0 ? 0 : CompletionPercent > 1 ? 1 : CompletionPercent;
            if (CompletionTimeSeconds < 0 || float.IsNaN(CompletionTimeSeconds)) CompletionTimeSeconds = 0;
            if (MistakeCount < 0) MistakeCount = 0;
            EducationalNotes ??= new List<string>();
            SuggestedEffects ??= new List<MinigameEffect>();
            Summary ??= "";
            return this;
        }
    }

    // Static description of a minigame type. Lives next to the minigame so MinigameManager never needs editing.
    public sealed class MinigameDefinition
    {
        public string Id;
        public string MissionId;
        public string MissionLabel;            // "Earth observation"
        public string Title;
        public string Instruction;             // one short sentence, always visible
        public string EducationalSubtitle;     // optional
        public float DefaultTimeLimitSeconds;  // 0 = untimed
        public bool PauseWorld = true;         // sets Time.timeScale = 0 while open
        public string EducationalNote;         // shown on the result panel
    }

    public interface IMinigame
    {
        string Id { get; }
        bool IsRunning { get; }
        void Initialize(MinigameContext context);
        void Begin();
        void Tick(float unscaledDeltaTime);
        void Cancel();
        event Action<MinigameResult> Completed;
    }

    // Accessibility/testing switches shared by every minigame.
    public static class MinigameSettings
    {
        // When true every minigame runs untimed (Mars still plays its storm sequence; see docs).
        public static bool DisableTimeLimits;

        public static float ResolveTimeLimit(MinigameContext context, MinigameDefinition definition)
        {
            if (DisableTimeLimits) return 0f;
            float limit = context != null && context.TimeLimitSeconds >= 0 ? context.TimeLimitSeconds : definition?.DefaultTimeLimitSeconds ?? 0f;
            return limit > 0 ? limit : 0f;
        }
    }

    public static class MinigameDifficulty
    {
        public static int Clamp(int level) => level < 1 ? 1 : level > 3 ? 3 : level;
    }

    public static class MinigameScoring
    {
        public static int Clamp(int score) => score < 0 ? 0 : score > 100 ? 100 : score;

        // Shared formula from the brief: 100, minus 8 per mistake, minus up to 20 for time used.
        public static int Standard(int mistakes, float elapsedSeconds, float timeLimitSeconds, float referenceSeconds = 60f)
        {
            float span = timeLimitSeconds > 0 ? timeLimitSeconds : referenceSeconds;
            float used = span > 0 ? Math.Min(1f, Math.Max(0f, elapsedSeconds / span)) : 0f;
            return Clamp((int)Math.Round(100 - 8 * mistakes - 20 * used));
        }
    }

    // Deterministic per-minigame random source. Never touches UnityEngine.Random.
    public static class MinigameRandom
    {
        public static Random Create(MinigameContext context, string salt) =>
            new Random(unchecked((context?.RandomSeed ?? 0) * 31 + MinigameDifficulty.Clamp(context?.DifficultyLevel ?? 1) * 7919 + StableHash(salt)));
        // string.GetHashCode is randomized per process in .NET; this one is stable.
        public static int StableHash(string s)
        {
            unchecked { int h = 23; foreach (char c in s ?? "") h = h * 31 + c; return h; }
        }
    }
}
