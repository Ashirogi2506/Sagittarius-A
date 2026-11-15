using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionSimulation
{
    public sealed class MissionSimulator : IMissionSimulator
    {
        static bool Has(string[] values, string value) { return values != null && Array.IndexOf(values, value) >= 0; }
        static float Clamp(float v) { return Math.Max(0f, Math.Min(1f, v)); }
        static bool Nonnegative(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f; }
        static bool ValidComponent(SpacecraftComponent c)
        {
            return c != null && !string.IsNullOrWhiteSpace(c.id) && Nonnegative(c.massKg) && Nonnegative(c.cost) &&
                Nonnegative(c.powerProducedW) && Nonnegative(c.powerUsedW) && Nonnegative(c.dataMBPerDay) &&
                Nonnegative(c.downlinkMBPerDay) && Nonnegative(c.storageCapacityMB) && Nonnegative(c.fuelRequired) &&
                Nonnegative(c.fuelProvided) && Nonnegative(c.science) && Nonnegative(c.protection) && Nonnegative(c.reliability) && c.reliability <= 1f;
        }
        static void Issue(ValidationResult r, Severity s, string code, string text) { r.issues.Add(new ValidationIssue(s, code, text)); }

        public ValidationResult Validate(MissionScenario s, PlayerDesign d)
        {
            if (s == null || d == null) throw new ArgumentNullException(s == null ? "scenario" : "design");
            var r = new ValidationResult(); var b = r.resources;
            if (s.objective == null || s.rules == null || s.requiredSystemTags == null || s.components == null || s.orbits == null || s.launchers == null || s.budget <= 0 || s.durationDays <= 0 || s.objective.requiredScience <= 0 || s.objective.requiredDataMB <= 0 || s.partialThreshold <= 0 || s.successThreshold > 1 || s.partialThreshold >= s.successThreshold || s.rules.warningPenalty < 0 || s.rules.missingSupportFactor < 0 || s.rules.missingSupportFactor > 1 || s.rules.shortageReliabilityFloor < 0 || s.rules.shortageReliabilityFloor > 1 || s.rules.objectivePoints < 0 || s.rules.sciencePoints < 0 || s.rules.engineeringPoints < 0 || s.rules.efficiencyPoints < 0 || s.rules.reliabilityPoints < 0 || s.rules.dataPoints < 0)
            { Issue(r, Severity.Blocking, "scenario", "Scenario is incomplete or has invalid budget, duration or objective targets."); return r; }
            if (!Nonnegative(s.budget) || !Nonnegative(s.durationDays) || !Nonnegative(s.objective.requiredScience) || !Nonnegative(s.objective.requiredDataMB) ||
                !Nonnegative(s.minimumReliability) || s.minimumReliability > 1f || !Nonnegative(s.partialThreshold) || !Nonnegative(s.successThreshold) ||
                !Nonnegative(s.rules.warningPenalty) || !Nonnegative(s.rules.missingSupportFactor) || !Nonnegative(s.rules.shortageReliabilityFloor) ||
                !Nonnegative(s.rules.objectivePoints) || !Nonnegative(s.rules.sciencePoints) || !Nonnegative(s.rules.engineeringPoints) ||
                !Nonnegative(s.rules.efficiencyPoints) || !Nonnegative(s.rules.reliabilityPoints) || !Nonnegative(s.rules.dataPoints) ||
                s.components.Any(c => !ValidComponent(c)) || s.components.Select(c => c.id).Distinct().Count() != s.components.Length ||
                s.orbits.Any(o => o == null || string.IsNullOrWhiteSpace(o.id) || !Nonnegative(o.scienceMultiplier) || !Nonnegative(o.downlinkMultiplier) || !Nonnegative(o.fuelRequired)) ||
                s.launchers.Any(l => l == null || string.IsNullOrWhiteSpace(l.id) || !Nonnegative(l.capacityKg) || !Nonnegative(l.cost) || !Nonnegative(l.reliability) || l.reliability > 1f) ||
                (s.events != null && s.events.Any(e => e == null || !Nonnegative(e.probability) || e.probability > 1f || !Nonnegative(e.scienceMultiplier) || !Nonnegative(e.dataMultiplier) || !Nonnegative(e.reliabilityMultiplier))) ||
                (s.hazards != null && s.hazards.Any(h => h == null || !Nonnegative(h.requiredProtection) || !Nonnegative(h.reliabilityPenalty) || h.reliabilityPenalty > 1f)))
            { Issue(r, Severity.Blocking, "scenario", "Scenario contains invalid or duplicate data. Review the mission configuration."); return r; }
            var orbit = s.orbits.FirstOrDefault(x => x.id == d.orbitId);
            var launcher = s.launchers.FirstOrDefault(x => x.id == d.launcherId);
            if (orbit == null) Issue(r, Severity.Blocking, "orbit", "Select a valid orbit.");
            if (launcher == null) Issue(r, Severity.Blocking, "launcher", "Select a valid launch vehicle.");
            var selected = new List<SpacecraftComponent>(); var ids = new HashSet<string>();
            foreach (var id in d.componentIds ?? new string[0])
            {
                var c = s.components.FirstOrDefault(x => x.id == id);
                if (c == null || !ids.Add(id)) { Issue(r, Severity.Blocking, "component", "Unknown or duplicate component: " + id); continue; }
                selected.Add(c);
            }
            b.reliability = 1f;
            foreach (var c in selected)
            {
                b.massKg += c.massKg; b.cost += c.cost; b.powerProducedW += c.powerProducedW; b.powerUsedW += c.powerUsedW;
                b.dataMBPerDay += c.dataMBPerDay; b.downlinkMBPerDay += c.downlinkMBPerDay;
                b.storageCapacityMB += c.storageCapacityMB;
                b.fuelProvided += c.fuelProvided; b.fuelRequired += c.fuelRequired;
                bool orbitFits = c.compatibleOrbitIds == null || c.compatibleOrbitIds.Length == 0 || (orbit != null && Has(c.compatibleOrbitIds, orbit.id));
                if (Has(c.tags, s.objective.requiredInstrumentTag) && (c.destinations == null || c.destinations.Length == 0 || Has(c.destinations, s.destination)) && orbitFits) b.science += c.science;
                if (c.kind == "instrument" && !orbitFits) Issue(r, Severity.Warning, "instrument_orbit", c.name + " is not compatible with the selected orbit.");
                b.protection += c.protection; b.reliability *= Clamp(c.reliability);
                if (c.destinations != null && c.destinations.Length > 0 && !Has(c.destinations, s.destination))
                    Issue(r, Severity.Warning, "destination", c.name + " is not suitable for " + s.destination + ".");
                foreach (var tag in c.requiredTags ?? new string[0])
                    if (!selected.Exists(x => Has(x.tags, tag))) Issue(r, Severity.Warning, "support", c.name + " requires " + tag + ".");
            }
            foreach (var tag in s.requiredSystemTags)
                if (!selected.Exists(x => Has(x.tags, tag))) Issue(r, Severity.Warning, "support", "Missing required spacecraft system: " + tag + ".");
            if (!selected.Exists(x => Has(x.tags, s.objective.requiredInstrumentTag) && (x.destinations == null || x.destinations.Length == 0 || Has(x.destinations, s.destination)) && (x.compatibleOrbitIds == null || x.compatibleOrbitIds.Length == 0 || (orbit != null && Has(x.compatibleOrbitIds, orbit.id))))) Issue(r, Severity.Warning, "instrument", "No instrument satisfies the objective at this destination and orbit.");
            if (orbit != null)
            {
                b.fuelRequired += orbit.fuelRequired; b.downlinkMBPerDay *= Math.Max(0, orbit.downlinkMultiplier);
                if (orbit.destination != s.destination || !Has(orbit.objectiveIds, s.objective.id)) Issue(r, Severity.Warning, "objective_orbit", "The orbit does not support this objective.");
            }
            if (launcher != null)
            {
                b.cost += launcher.cost;
                // Missing reliability in legacy scenario JSON means no launcher modifier.
                if (launcher.reliability > 0) b.reliability *= Clamp(launcher.reliability);
                if (b.massKg > launcher.capacityKg) Issue(r, Severity.Blocking, "mass", "Spacecraft mass exceeds launcher capacity.");
                if (!Has(launcher.destinations, s.destination) || (orbit != null && !Has(launcher.compatibleOrbitIds, orbit.id))) Issue(r, Severity.Blocking, "launcher_compatibility", "Launcher cannot deliver this mission to the selected orbit.");
            }
            if (b.cost > s.budget) Issue(r, Severity.Blocking, "budget", "Mission cost exceeds the available budget.");
            if (b.powerProducedW < b.powerUsedW) Issue(r, Severity.Warning, "power", "Power supply is below demand; science and reliability will decrease.");
            if (b.downlinkMBPerDay < b.dataMBPerDay) Issue(r, Severity.Warning, "data", "Downlink capacity is below data production; some data will be lost.");
            if (b.dataMBPerDay > b.downlinkMBPerDay + b.storageCapacityMB) Issue(r, Severity.Warning, "storage", "The daily data surplus exceeds onboard storage.");
            if (b.fuelProvided < b.fuelRequired) Issue(r, Severity.Warning, "fuel", "Fuel is insufficient; mission performance will decrease.");
            foreach (var h in s.hazards ?? new EnvironmentalHazard[0])
                if (b.protection < h.requiredProtection) Issue(r, Severity.Warning, "hazard", h.name + ": " + h.explanation);
            if (b.reliability < s.minimumReliability) Issue(r, Severity.Warning, "reliability", "Estimated reliability is below the mission target for the configured duration.");
            return r;
        }

        public SimulationResult Run(MissionScenario s, PlayerDesign d, int seed)
        {
            var v = Validate(s, d); var result = new SimulationResult { validation = v };
            if (!v.CanLaunch) { result.outcome = MissionOutcome.Blocked; result.summary = "Launch blocked by the listed design errors."; result.explanations.Add(result.summary); return result; }
            var b = v.resources; var orbit = s.orbits.First(x => x.id == d.orbitId);
            float power = b.powerUsedW <= 0 ? 1 : Clamp(b.powerProducedW / b.powerUsedW);
            float fuel = b.fuelRequired <= 0 ? 1 : Clamp(b.fuelProvided / b.fuelRequired);
            float support = v.issues.Exists(x => x.code == "support") ? s.rules.missingSupportFactor : 1f;
            float suitable = v.issues.Exists(x => x.code == "instrument" || x.code == "objective_orbit") ? 0f : 1f;
            float floor = s.rules.shortageReliabilityFloor;
            float reliability = b.reliability * (floor + (1f - floor) * power) * (floor + (1f - floor) * fuel);
            foreach (var h in s.hazards ?? new EnvironmentalHazard[0]) if (b.protection < h.requiredProtection) reliability *= Clamp(1 - h.reliabilityPenalty);
            float scienceFactor = 1, dataFactor = 1; var random = new Random(seed);
            foreach (var e in s.events ?? new MissionEvent[0]) if (random.NextDouble() < Clamp(e.probability))
            { result.events.Add(e.description); scienceFactor *= Clamp(e.scienceMultiplier); dataFactor *= Clamp(e.dataMultiplier); reliability *= Clamp(e.reliabilityMultiplier); }
            reliability = Clamp(reliability);
            // Simplified aggregate model: rates are daily averages and reliability scales expected returned output.
            float generated = Math.Max(0, b.dataMBPerDay) * s.durationDays * power * fuel * support;
            // Data events represent losses from the downlink, including when capacity is already saturated.
            result.dataReturnedMB = Math.Min(generated, Math.Max(0, b.downlinkMBPerDay) * s.durationDays) * reliability * dataFactor;
            result.scienceDelivered = b.science * Math.Max(0, orbit.scienceMultiplier) * power * fuel * support * suitable * scienceFactor * reliability;
            result.reliability = reliability;
            float dataProgress = Clamp(result.dataReturnedMB / s.objective.requiredDataMB);
            float scienceProgress = Clamp(result.scienceDelivered / s.objective.requiredScience);
            float progress = Math.Min(dataProgress, scienceProgress);
            result.outcome = progress >= s.successThreshold ? MissionOutcome.Success : progress >= s.partialThreshold ? MissionOutcome.PartialSuccess : MissionOutcome.Failure;
            if (result.outcome == MissionOutcome.Success) result.completedObjectives.Add(s.objective.id);
            else result.failedObjectives.Add(s.objective.id);
            result.summary = result.outcome == MissionOutcome.Success ? "The mission met its observation objective." :
                result.outcome == MissionOutcome.PartialSuccess ? "The mission returned some results but did not fully meet its objective." :
                "The mission did not return enough useful observations.";
            var rules = s.rules;
            Add(result, "Objective completion", rules.objectivePoints * progress, rules.objectivePoints, "Completion is limited by the lower of science and returned-data progress.");
            Add(result, "Scientific value", rules.sciencePoints * scienceProgress, rules.sciencePoints, "Science reflects suitable instruments, orbit, resources, events and reliability.");
            Add(result, "Engineering validity", rules.engineeringPoints * Clamp(1f - v.issues.Count(x => x.severity == Severity.Warning) * rules.warningPenalty), rules.engineeringPoints, "Each design warning reduces engineering validity.");
            Add(result, "Resource efficiency", rules.efficiencyPoints * Clamp(1f - b.cost / s.budget), rules.efficiencyPoints, "Remaining budget contributes to resource efficiency.");
            Add(result, "Reliability and risk", rules.reliabilityPoints * reliability, rules.reliabilityPoints, "Reliability includes components, resource shortages, hazards and events.");
            Add(result, "Data returned", rules.dataPoints * dataProgress, rules.dataPoints, "Data returned is limited by production, downlink capacity and reliability.");
            foreach (var issue in v.issues) result.explanations.Add(issue.message);
            result.explanations.Add(string.Format("Returned {0:F0} MB of {1:F0} MB required; delivered {2:F1} of {3:F1} science required.", result.dataReturnedMB, s.objective.requiredDataMB, result.scienceDelivered, s.objective.requiredScience));
            if (result.events.Count == 0) result.explanations.Add("No mission events occurred.");
            return result;
        }
        static void Add(SimulationResult r, string name, float points, float maximum, string reason) { r.score.entries.Add(new ScoreEntry(name, points, maximum, reason)); }
    }
}
