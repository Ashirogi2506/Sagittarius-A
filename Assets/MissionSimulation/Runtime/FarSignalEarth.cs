using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionSimulation.FarSignal
{
    // Matches the team's proposed JSON. Unused story/dialogue fields remain owned by the narrative team.
    [Serializable] public class EarthScenario
    {
        public string title, missionId, nextMissionId, status;
        public long budgetUsd, busCostUsd;
        public float busMassKg, busPowerKw, minimumBatteryKwh, minimumFuelUnits;
        public ResultMessages resultMessages;
        public Launcher[] launchers; public Nozzle[] nozzles; public Fairing[] fairings;
        public Engine[] engines; public CargoBay[] cargoModules; public PowerSystem[] powerSystems;
        public Cargo[] cargo; public LandingPad[] landingMap;
    }
    [Serializable] public class ResultMessages { public string blocked, success, successWithTradeoffs; }
    [Serializable] public class Launcher { public string id, name; public long costUsd; public float basePayloadKg; public string[] nozzleMounts; }
    [Serializable] public class Nozzle { public string id, name, mount; public long costUsd; public float payloadGainKg; }
    [Serializable] public class Fairing { public string id, name; public long costUsd; public int maxModuleSize; public float payloadLossKg; }
    [Serializable] public class Engine { public string id, name; public long costUsd; public float massKg, fuelUnits; public int freeLandingCorrections; }
    [Serializable] public class CargoBay { public string id, name; public long costUsd; public float massKg; public int size, slots; }
    [Serializable] public class PowerSystem { public string id, name; public long costUsd; public float massKg, maxPowerKw, batteryKwh; }
    [Serializable] public class Cargo { public string id, name; public long costUsd; public float massKg, extraPowerKw, extraFuelUnits; public int slots; public bool required, adjacentSlots; }
    [Serializable] public class LandingPad { public string id, name, description; public int openMinute, closeMinute; public float fuelUnits; }
    [Serializable] public class CargoPlacement { public string cargoId; public int startSlot; }
    [Serializable] public class EarthDesign
    {
        public string launcherId, nozzleId, fairingId, engineId, cargoModuleId, powerSystemId, landingPadId;
        public CargoPlacement[] cargo;
    }
    public enum IssueLevel { Warning, Error }
    [Serializable] public class Issue
    {
        public IssueLevel level; public string code, explanation, suggestedFix;
        public Issue(IssueLevel level, string code, string explanation, string fix)
        { this.level = level; this.code = code; this.explanation = explanation; suggestedFix = fix; }
    }
    [Serializable] public class LaunchReport
    {
        public string missionId, nextMissionId, landingPadId;
        public long costUsd, budgetMarginUsd;
        public float payloadKg, launchCapacityKg, payloadMarginKg, requiredPowerKw, availablePowerKw, batteryKwh, fuelUnits, plannedLandingFuelUnits;
        public int usedCargoSlots, availableCargoSlots, freeLandingCorrections;
        public bool laserPowered;
        public List<Issue> issues = new List<Issue>();
        public bool CanLaunch { get { return !issues.Exists(i => i.level == IssueLevel.Error); } }
    }
    public enum LaunchOutcome { Blocked, Ready, ReadyWithWarnings }
    [Serializable] public class LaunchResult
    {
        public LaunchOutcome outcome;
        public string summary;
        public LaunchReport report;
        // Snapshot prepared for the next chapter. The caller decides when to persist it.
        public EarthDesign designSnapshot;
        public string nextMissionId;
    }

    // Pure C#: no Unity, scene, UI, filesystem or global-state dependencies.
    public sealed class EarthLaunchSimulator
    {
        static void Add(LaunchReport r, IssueLevel level, string code, string text, string fix) { r.issues.Add(new Issue(level, code, text, fix)); }
        // This evaluates launch readiness, not the unimplemented flight or landing.
        public LaunchResult Evaluate(EarthScenario s, EarthDesign d)
        {
            var report = Validate(s, d);
            var result = new LaunchResult { report = report };
            if (!report.CanLaunch)
            {
                result.outcome = LaunchOutcome.Blocked;
                result.summary = s.resultMessages != null && !string.IsNullOrEmpty(s.resultMessages.blocked) ? s.resultMessages.blocked : "Launch is on hold. Review the checklist.";
                return result;
            }
            result.outcome = report.issues.Any(i => i.level == IssueLevel.Warning) ? LaunchOutcome.ReadyWithWarnings : LaunchOutcome.Ready;
            result.summary = result.outcome == LaunchOutcome.Ready ? "Launch checks passed." : "Launch checks passed with warnings. Review the backup plan.";
            result.nextMissionId = s.nextMissionId;
            result.designSnapshot = new EarthDesign {
                launcherId = d.launcherId, nozzleId = d.nozzleId, fairingId = d.fairingId, engineId = d.engineId,
                cargoModuleId = d.cargoModuleId, powerSystemId = d.powerSystemId, landingPadId = d.landingPadId,
                cargo = (d.cargo ?? new CargoPlacement[0]).Select(x => new CargoPlacement { cargoId = x.cargoId, startSlot = x.startSlot }).ToArray()
            };
            return result;
        }
        public LaunchReport Validate(EarthScenario s, EarthDesign d)
        {
            if (s == null || d == null) throw new ArgumentNullException(s == null ? "scenario" : "design");
            var r = new LaunchReport { missionId = s.missionId, nextMissionId = s.nextMissionId, landingPadId = d.landingPadId };
            if (s.launchers == null || s.nozzles == null || s.fairings == null || s.engines == null || s.cargoModules == null || s.powerSystems == null || s.cargo == null || s.landingMap == null)
            { Add(r, IssueLevel.Error, "scenario", "Mission data is incomplete.", "Load the complete mission data."); return r; }
            var l = s.launchers.FirstOrDefault(x => x.id == d.launcherId); var n = s.nozzles.FirstOrDefault(x => x.id == d.nozzleId);
            var f = s.fairings.FirstOrDefault(x => x.id == d.fairingId); var e = s.engines.FirstOrDefault(x => x.id == d.engineId);
            var bay = s.cargoModules.FirstOrDefault(x => x.id == d.cargoModuleId); var p = s.powerSystems.FirstOrDefault(x => x.id == d.powerSystemId);
            var pad = s.landingMap.FirstOrDefault(x => x.id == d.landingPadId);
            if (l == null || n == null || f == null || e == null || bay == null || p == null || pad == null)
            { Add(r, IssueLevel.Error, "selection", "One or more required selections are missing or unknown.", "Select a valid launcher, nozzle, cover, engine, cargo bay, power system, and landing pad."); return r; }
            r.costUsd = s.busCostUsd + l.costUsd + n.costUsd + f.costUsd + e.costUsd + bay.costUsd + p.costUsd;
            r.payloadKg = s.busMassKg + e.massKg + bay.massKg + p.massKg;
            r.launchCapacityKg = l.basePayloadKg + n.payloadGainKg - f.payloadLossKg;
            r.availablePowerKw = p.maxPowerKw; r.requiredPowerKw = s.busPowerKw; r.batteryKwh = p.batteryKwh;
            r.fuelUnits = e.fuelUnits; r.plannedLandingFuelUnits = pad.fuelUnits; r.availableCargoSlots = bay.slots; r.freeLandingCorrections = e.freeLandingCorrections;
            if (l.nozzleMounts == null || Array.IndexOf(l.nozzleMounts, n.mount) < 0) Add(r, IssueLevel.Error, "nozzle", "Nozzle mount does not fit the launcher.", "Choose a compatible nozzle or launcher.");
            if (bay.size > f.maxModuleSize) Add(r, IssueLevel.Error, "fairing", "The cargo bay is too large for the launch cover.", "Choose a wider cover or smaller cargo bay.");
            var occupied = new HashSet<int>(); var selected = new HashSet<string>();
            foreach (var placement in d.cargo ?? new CargoPlacement[0])
            {
                if (placement == null) { Add(r, IssueLevel.Error, "cargo", "A cargo placement is missing.", "Choose a cargo item and position."); continue; }
                var item = s.cargo.FirstOrDefault(x => x.id == placement.cargoId);
                if (item == null || !selected.Add(placement.cargoId)) { Add(r, IssueLevel.Error, "cargo", "An unknown or duplicate cargo item was selected.", "Remove the duplicate or select a valid item."); continue; }
                if (item.slots <= 0 || placement.startSlot < 0 || placement.startSlot > bay.slots || item.slots > bay.slots - placement.startSlot)
                { Add(r, IssueLevel.Error, "cargo_slots", item.name + " does not fit in the cargo bay.", "Move the item or select a larger cargo bay."); continue; }
                r.costUsd += item.costUsd; r.payloadKg += item.massKg; r.requiredPowerKw += item.extraPowerKw; r.fuelUnits += item.extraFuelUnits; r.usedCargoSlots += item.slots;
                // A start slot represents a contiguous run; this guarantees the panel's adjacency requirement.
                for (int slot = placement.startSlot; slot < placement.startSlot + item.slots; slot++)
                    if (slot < 0 || slot >= bay.slots || !occupied.Add(slot)) Add(r, IssueLevel.Error, "cargo_slots", item.name + " does not fit or overlaps another item.", "Move the item into free adjacent slots or choose a larger bay.");
            }
            foreach (var item in s.cargo.Where(x => x.required)) if (!selected.Contains(item.id)) Add(r, IssueLevel.Error, "required_cargo", item.name + " is required for Pole-7.", "Load and secure the required item.");
            r.payloadMarginKg = r.launchCapacityKg - r.payloadKg; r.budgetMarginUsd = s.budgetUsd - r.costUsd;
            if (r.payloadMarginKg < 0) Add(r, IssueLevel.Error, "mass", "Payload exceeds launcher capacity by " + (-r.payloadMarginKg) + " kg.", "Reduce mass or select a higher-capacity compatible launcher configuration.");
            if (r.budgetMarginUsd < 0) Add(r, IssueLevel.Error, "budget", "Mission exceeds the budget by $" + (-r.budgetMarginUsd) + ".", "Choose lower-cost equipment.");
            if (p.maxPowerKw < s.busPowerKw) Add(r, IssueLevel.Error, "essential_power", "The power system cannot support the ship's essential load.", "Choose a stronger power system.");
            if (p.batteryKwh < s.minimumBatteryKwh) Add(r, IssueLevel.Error, "battery", "Battery capacity is below the mission minimum.", "Choose a power system with a larger battery.");
            if (r.fuelUnits < s.minimumFuelUnits) Add(r, IssueLevel.Error, "fuel", "Fuel is below the minimum for launch.", "Choose a different engine or carry extra fuel.");
            if (r.fuelUnits < pad.fuelUnits) Add(r, IssueLevel.Warning, "landing_fuel", "The selected pad needs more fuel than is available.", "Add fuel, change engine, or plan the other pad.");
            r.laserPowered = selected.Contains("laser") && r.requiredPowerKw <= r.availablePowerKw;
            if (r.requiredPowerKw > r.availablePowerKw && p.maxPowerKw >= s.busPowerKw) Add(r, IssueLevel.Warning, "optional_power", "Optional equipment exceeds available power; the laser remains off.", "Choose a stronger power system or use hand tools.");
            return r;
        }
    }
}
