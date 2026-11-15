using System;
using System.Collections.Generic;

namespace MissionSimulation
{
    // All numerical values in scenario files are gameplay assumptions, not flight specifications.
    [Serializable] public class MissionScenario
    {
        public string id, name, destination, assumptions;
        public float budget, durationDays, minimumReliability, partialThreshold, successThreshold;
        public MissionObjective objective;
        public SimulationRules rules;
        public string[] requiredSystemTags;
        public SpacecraftComponent[] components;
        public OrbitOption[] orbits;
        public LaunchVehicle[] launchers;
        public EnvironmentalHazard[] hazards;
        public MissionEvent[] events;
    }
    [Serializable] public class SimulationRules
    {
        public float objectivePoints, sciencePoints, engineeringPoints, efficiencyPoints, reliabilityPoints, dataPoints;
        public float warningPenalty, missingSupportFactor, shortageReliabilityFloor;
    }
    [Serializable] public class MissionObjective
    {
        public string id, description, requiredInstrumentTag;
        public float requiredScience, requiredDataMB;
    }
    [Serializable] public class SpacecraftComponent
    {
        public string id, name, kind, explanation;
        public string[] tags, destinations, requiredTags, compatibleOrbitIds;
        public float massKg, cost, powerProducedW, powerUsedW, dataMBPerDay, downlinkMBPerDay;
        public float fuelRequired, fuelProvided, science, reliability, protection, storageCapacityMB;
    }
    [Serializable] public class OrbitOption
    {
        public string id, name, destination, explanation;
        public string[] objectiveIds;
        public float scienceMultiplier, downlinkMultiplier, fuelRequired;
    }
    [Serializable] public class LaunchVehicle
    {
        public string id, name, explanation;
        public string[] destinations, compatibleOrbitIds;
        public float capacityKg, cost, reliability;
    }
    [Serializable] public class EnvironmentalHazard
    {
        public string name, explanation;
        public float requiredProtection, reliabilityPenalty;
    }
    [Serializable] public class MissionEvent
    {
        public string id, description;
        public float probability, scienceMultiplier, dataMultiplier, reliabilityMultiplier;
    }
    [Serializable] public class PlayerDesign
    {
        public string orbitId, launcherId;
        public string[] componentIds;
    }
    [Serializable] public class ResourceBudget
    {
        public float massKg, cost, powerProducedW, powerUsedW, dataMBPerDay, downlinkMBPerDay;
        public float fuelProvided, fuelRequired, reliability, science, protection, storageCapacityMB;
    }
    public enum Severity { Warning, Blocking }
    [Serializable] public class ValidationIssue
    {
        public Severity severity;
        public string code, message, title, suggestedFix;
        public ValidationIssue(Severity severity, string code, string message)
        { this.severity = severity; this.code = code; this.message = message; title = code.Replace('_', ' '); suggestedFix = FixFor(code); }
        private static string FixFor(string code)
        {
            switch (code)
            {
                case "orbit": case "objective_orbit": case "instrument_orbit": return "Choose an orbit compatible with the instrument and objective.";
                case "launcher": case "launcher_compatibility": return "Choose a compatible launch vehicle.";
                case "mass": return "Choose a larger launcher or reduce spacecraft mass.";
                case "budget": return "Choose less expensive equipment or launcher.";
                case "power": return "Add power generation or reduce demand.";
                case "data": return "Improve downlink or storage, or reduce data production.";
                case "storage": return "Add onboard storage or improve downlink capacity.";
                case "fuel": return "Add fuel capacity or choose a lower-fuel orbit.";
                case "hazard": return "Add environmental protection suitable for the destination.";
                case "reliability": return "Select more reliable or better-protected components.";
                case "instrument": return "Select a suitable science instrument.";
                case "support": return "Install the missing supporting system.";
                case "destination": return "Choose equipment compatible with the destination.";
                case "component": return "Select known components only once.";
                default: return "Review the mission data and selections.";
            }
        }
    }
    [Serializable] public class ValidationResult
    {
        public ResourceBudget resources = new ResourceBudget();
        public List<ValidationIssue> issues = new List<ValidationIssue>();
        public bool CanLaunch { get { return !issues.Exists(x => x.severity == Severity.Blocking); } }
    }
    [Serializable] public class ScoreEntry
    {
        public string category, reason;
        public float points, maximum;
        public ScoreEntry(string category, float points, float maximum, string reason)
        { this.category = category; this.points = points; this.maximum = maximum; this.reason = reason; }
    }
    [Serializable] public class ScoreBreakdown
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
        public float Total { get { float n = 0; foreach (var e in entries) n += e.points; return Math.Max(0f, Math.Min(100f, n)); } }
    }
    public enum MissionOutcome { Blocked, Failure, PartialSuccess, Success }
    [Serializable] public class SimulationResult
    {
        public MissionOutcome outcome;
        public ValidationResult validation;
        public float dataReturnedMB, scienceDelivered, reliability;
        public string summary;
        public List<string> events = new List<string>();
        public List<string> explanations = new List<string>();
        public List<string> completedObjectives = new List<string>();
        public List<string> failedObjectives = new List<string>();
        public ScoreBreakdown score = new ScoreBreakdown();
    }
    public interface IMissionSimulator
    {
        ValidationResult Validate(MissionScenario scenario, PlayerDesign design);
        SimulationResult Run(MissionScenario scenario, PlayerDesign design, int seed);
    }
}
