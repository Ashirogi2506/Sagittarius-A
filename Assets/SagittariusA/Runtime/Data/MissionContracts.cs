using System;
using System.Collections.Generic;

namespace SagittariusA.Gameplay.Data
{
    [Serializable]
    public sealed class MissionConfiguration
    {
        public List<string> selectedComponentIds = new List<string>();
        public string orbitId;
        public string launcherId;
    }

    public enum ValidationSeverity
    {
        Information,
        Warning,
        Error
    }

    [Serializable]
    public sealed class ValidationMessage
    {
        public string code;
        public ValidationSeverity severity;
        public string title;
        public string explanation;
        public string suggestedFix;
    }

    [Serializable]
    public sealed class MissionValidationResult
    {
        public bool canLaunch;
        public MissionResourceTotals totals = new MissionResourceTotals();
        public List<ValidationMessage> messages = new List<ValidationMessage>();
    }

    public enum MissionOutcome
    {
        Failure,
        PartialSuccess,
        Success
    }

    [Serializable]
    public sealed class ScoreCategory
    {
        public string id;
        public string displayName;
        public float score;
        public float maximumScore;
        public string explanation;
    }

    [Serializable]
    public sealed class MissionResult
    {
        public MissionOutcome outcome;
        public float totalScore;
        public List<ScoreCategory> scoreBreakdown = new List<ScoreCategory>();
        public List<string> positiveDecisions = new List<string>();
        public List<string> problems = new List<string>();
        public string scientificResult;
        public bool unlockNextMission;
    }

    public interface IMissionSimulationService
    {
        MissionValidationResult Validate(MissionDefinition mission, MissionConfiguration configuration);
        MissionResult Simulate(MissionDefinition mission, MissionConfiguration configuration);
    }
}
