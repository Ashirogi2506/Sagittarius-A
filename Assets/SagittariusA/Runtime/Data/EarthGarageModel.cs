using System;
using System.Collections.Generic;
using System.Linq;

namespace SagittariusA.Gameplay.Data
{
    [Serializable] public sealed class EarthPart
    {
        public string id, name, mount;
        public string[] nozzleMounts;
        public int costUsd, basePayloadKg, payloadGainKg, payloadLossKg, maxModuleSize;
        public int massKg, fuelUnits, freeLandingCorrections, size, slots;
        public int maxPowerKw, batteryKwh, extraPowerKw, extraFuelUnits;
        public bool required, adjacentSlots;
    }
    [Serializable] public sealed class EarthPad
    {
        public string id, name, description;
        public int openMinute, closeMinute, fuelUnits;
    }
    [Serializable] public sealed class EarthDialogue { public string speaker, line; }
    [Serializable] public sealed class EarthCatalog
    {
        public string missionId, nextMissionId, title, briefing;
        public int budgetUsd, busCostUsd, busMassKg, busPowerKw, minimumBatteryKwh, minimumFuelUnits;
        public EarthPart[] launchers, nozzles, fairings, engines, cargoModules, powerSystems, cargo;
        public EarthPad[] landingMap;
        public EarthDialogue[] opening;
        public EarthPart[] Group(int group) => new[] { launchers, nozzles, fairings, engines, cargoModules, powerSystems, cargo }[group];
    }
    [Serializable] public sealed class EarthBuild
    {
        public string[] parts = { "l1", "n1", "f1", "e1", "c1", "p1" };
        public List<string> cargo = new List<string> { "panel", "kit" };
        public EarthBuild Copy() => new EarthBuild { parts = (string[])parts.Clone(), cargo = new List<string>(cargo) };
    }
    [Serializable] public sealed class CargoPlacement
    {
        public string itemId;
        public int firstSlot;
        public bool locked;
    }
    [Serializable] public sealed class EarthChecks
    {
        public List<CargoPlacement> placements = new List<CargoPlacement>();
        public bool powerTestCompleted, cabinOn, radioOn, laserTested;
        public string padId;
        public float arrivalMinute;
    }
    public sealed class EarthIssue
    {
        public string code, message;
        public bool blocksLaunch;
        public int category;
    }
    public sealed class EarthAssessment
    {
        public int costUsd, massKg, payloadKg, slotsUsed, slotsAvailable, supplyKw, batteryKwh, fuelUnits;
        public readonly List<EarthIssue> issues = new List<EarthIssue>();
        public bool CanLaunch => !issues.Any(x => x.blocksLaunch);
        public int MassMarginKg => payloadKg - massKg;
    }
    // The simulation teammate replaces this adapter. Views never calculate limits.
    public interface IEarthSimulationAdapter
    {
        EarthAssessment Assess(EarthCatalog catalog, EarthBuild build, EarthChecks checks);
    }
    public sealed class DraftEarthSimulationAdapter : IEarthSimulationAdapter
    {
        public EarthAssessment Assess(EarthCatalog c, EarthBuild b, EarthChecks checks)
        {
            var result = new EarthAssessment();
            void Issue(string code, string message, int category, bool blocks = true) => result.issues.Add(new EarthIssue { code = code, message = message, category = category, blocksLaunch = blocks });
            if (checks == null || b?.parts == null || b.parts.Length != 6 || b.cargo == null || b.cargo.Distinct().Count() != b.cargo.Count)
            { Issue("invalid-build", "Invalid ship configuration. Load a starter build.", 0); return result; }
            var parts = new EarthPart[6];
            for (int i = 0; i < 6; i++)
            {
                parts[i] = c.Group(i).FirstOrDefault(p => p.id == b.parts[i]);
                if (parts[i] == null) { Issue("unknown-part", "Unknown part. Choose an available part.", i); return result; }
            }
            var cargo = b.cargo.Select(id => c.cargo.FirstOrDefault(p => p.id == id)).ToArray();
            if (cargo.Any(p => p == null)) { Issue("unknown-cargo", "Unknown cargo. Rebuild the cargo manifest.", 6); return result; }
            var launcher = parts[0]; var nozzle = parts[1]; var cover = parts[2];
            var engine = parts[3]; var bay = parts[4]; var power = parts[5];
            bool mountFits = launcher.nozzleMounts.Contains(nozzle.mount);
            result.costUsd = c.busCostUsd + parts.Sum(p => p.costUsd) + cargo.Sum(p => p.costUsd);
            result.massKg = c.busMassKg + engine.massKg + bay.massKg + power.massKg + cargo.Sum(p => p.massKg);
            result.payloadKg = launcher.basePayloadKg + (mountFits ? nozzle.payloadGainKg : 0) - cover.payloadLossKg;
            result.slotsUsed = cargo.Sum(p => p.slots); result.slotsAvailable = bay.slots;
            result.supplyKw = power.maxPowerKw; result.batteryKwh = power.batteryKwh;
            result.fuelUnits = engine.fuelUnits + cargo.Sum(p => p.extraFuelUnits);
            if (result.costUsd > c.budgetUsd) Issue("budget", $"Over budget by ${result.costUsd - c.budgetUsd:N0}. Pick a cheaper part or remove optional cargo.", 6);
            if (!mountFits) Issue("mount", $"This nozzle will not fit. Pick mount {string.Join(" or ", launcher.nozzleMounts)}.", 1);
            if (bay.size > cover.maxModuleSize) Issue("cover", "The bay will not fit under this cover. Choose a wider cover or smaller bay.", 2);
            if (result.MassMarginKg < 0) Issue("mass", $"Over launch limit by {-result.MassMarginKg:N0} kg. Remove cargo or choose more lift capacity.", 1);
            else if (result.MassMarginKg <= 100) Issue("margin", $"Only {result.MassMarginKg} kg of launch capacity remains. Recheck before adding cargo.", 6, false);
            if (result.slotsUsed > bay.slots) Issue("slots", $"You need {result.slotsUsed - bay.slots} more cargo spaces. Choose a bigger bay and check the cover.", 4);
            foreach (var item in c.cargo.Where(p => p.required && !b.cargo.Contains(p.id))) Issue("required-" + item.id, $"Pole-7 needs {item.name}. Add it to the cargo.", 6);
            if (power.maxPowerKw < c.busPowerKw) Issue("base-power", $"Cabin and radio need {c.busPowerKw} kW. Choose more output.", 5);
            if (power.batteryKwh < c.minimumBatteryKwh) Issue("battery", $"The ship needs {c.minimumBatteryKwh} kWh of battery storage.", 5);
            if (result.fuelUnits < c.minimumFuelUnits) Issue("fuel", $"The ship needs {c.minimumFuelUnits} fuel units. Choose more fuel.", 3);
            if (cargo.Any(p => p.extraPowerKw + c.busPowerKw > power.maxPowerKw))
                Issue("laser", $"Cabin, radio and laser need 6 kW; output is {power.maxPowerKw} kW. Upgrade power or use hand tools.", 5, false);
            if (!ValidCargo(c, b, checks, bay.slots)) Issue("cargo-check", "Pack and lock every loaded item in the cargo minigame.", 6);
            if (!checks.powerTestCompleted || !checks.cabinOn || !checks.radioOn || (b.cargo.Contains("laser") && !checks.laserTested))
                Issue("power-check", "Complete the cabin and radio power test; test the laser if carried.", 5);
            var pad = c.landingMap.FirstOrDefault(p => p.id == checks.padId);
            if (pad == null || float.IsNaN(checks.arrivalMinute) || float.IsInfinity(checks.arrivalMinute) || checks.arrivalMinute < 0 || checks.arrivalMinute > 10)
                Issue("landing-check", "Choose a landing pad and time in the landing minigame.", 6);
            else
            {
                if (checks.arrivalMinute < pad.openMinute || checks.arrivalMinute >= pad.closeMinute)
                    Issue("shadow", "The planned pad is in shadow. Choose an earlier time or another pad before descent.", 6, false);
                if (result.fuelUnits < pad.fuelUnits) Issue("pad-fuel", $"{pad.name} needs {pad.fuelUnits} fuel; you have {result.fuelUnits}. Revise the plan before descent.", 6, false);
            }
            return result;
        }
        private static bool ValidCargo(EarthCatalog c, EarthBuild b, EarthChecks checks, int capacity)
        {
            if (checks.placements == null || checks.placements.Count != b.cargo.Count) return false;
            var occupied = new HashSet<int>(); var ids = new HashSet<string>();
            foreach (var placement in checks.placements)
            {
                if (placement == null || !placement.locked || !b.cargo.Contains(placement.itemId) || !ids.Add(placement.itemId)) return false;
                var item = c.cargo.First(p => p.id == placement.itemId);
                if (placement.firstSlot < 0 || placement.firstSlot >= capacity) return false;
                int end = placement.firstSlot + item.slots - 1;
                if (placement.firstSlot < 0 || end >= capacity) return false;
                if (item.adjacentSlots && capacity == 6 && placement.firstSlot / 3 != end / 3) return false;
                for (int slot = placement.firstSlot; slot <= end; slot++) if (!occupied.Add(slot)) return false;
            }
            return true;
        }
    }
}
