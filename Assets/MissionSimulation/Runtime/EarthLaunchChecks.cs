using System;
using System.Collections.Generic;
using System.Linq;
using MissionSimulation.Minigames;

namespace MissionSimulation.FarSignal
{
    // Draft game values from the campaign brief, not real spacecraft ratings.
    [Serializable] public class EarthPowerTestState
    {
        public string powerSystemId;
        public bool cabinOn, radioOn, laserOn, laserTripped;
    }
    public sealed class EarthPowerTest
    {
        const float CabinKw = 2f, RadioKw = 1f, LaserKw = 3f;
        readonly float capacityKw;
        readonly bool laserLoaded;
        readonly EarthPowerTestState state;
        public EarthPowerTest(string powerSystemId, float capacityKw, bool laserLoaded)
        {
            if (string.IsNullOrWhiteSpace(powerSystemId) || float.IsNaN(capacityKw) || float.IsInfinity(capacityKw) || capacityKw < 0) throw new ArgumentException("A valid power system and capacity are required.");
            this.capacityKw = capacityKw; this.laserLoaded = laserLoaded;
            state = new EarthPowerTestState { powerSystemId = powerSystemId };
        }
        public void SetCabin(bool on) { state.cabinOn = on; if (!on) state.laserOn = false; }
        public void SetRadio(bool on) { state.radioOn = on; if (!on) state.laserOn = false; }
        public bool EssentialsPassed { get { return state.cabinOn && state.radioOn && capacityKw >= CabinKw + RadioKw; } }
        public bool TryLaser()
        {
            if (!laserLoaded || !EssentialsPassed) return false;
            if (capacityKw < CabinKw + RadioKw + LaserKw) { state.laserOn = false; state.laserTripped = true; return false; }
            state.laserTripped = false; state.laserOn = true; return true;
        }
        public EarthPowerTestState Snapshot() { return new EarthPowerTestState { powerSystemId = state.powerSystemId, cabinOn = state.cabinOn, radioOn = state.radioOn, laserOn = state.laserOn, laserTripped = state.laserTripped }; }
    }

    [Serializable] public class LandingPlan { public string padId; public bool hasArrivalMinute; public float arrivalMinute; }
    public sealed class EarthLaunchChecklist
    {
        const float MaxPlanningMinute = 10f, LowMassMarginKg = 100f;
        static void Issue(LaunchReport r, IssueLevel level, string code, string explanation, string fix) { r.issues.Add(new Issue(level, code, explanation, fix)); }
        // Preview only: does not spend fuel, persist a save, or initiate launch.
        public LaunchReport Validate(EarthScenario scenario, EarthDesign design, CargoPackingState cargo, EarthPowerTestState power, LandingPlan plan)
        {
            var result = new EarthLaunchSimulator().Validate(scenario, design);
            if (scenario == null || design == null || scenario.cargoModules == null || scenario.landingMap == null) return result;
            var bay = scenario.cargoModules.FirstOrDefault(x => x != null && x.id == design.cargoModuleId);
            var pad = plan == null ? null : scenario.landingMap.FirstOrDefault(x => x != null && x.id == plan.padId);
            if (plan == null || pad == null || !plan.hasArrivalMinute || float.IsNaN(plan.arrivalMinute) || float.IsInfinity(plan.arrivalMinute) || plan.arrivalMinute < 0 || plan.arrivalMinute > MaxPlanningMinute || design.landingPadId != plan.padId)
                Issue(result, IssueLevel.Error, "landing_plan", "Choose a landing pad and an arrival minute from 0 to 10.", "Select a matching pad and time on the map.");
            else if (plan.arrivalMinute < pad.openMinute || plan.arrivalMinute >= pad.closeMinute)
                Issue(result, IssueLevel.Warning, "landing_shadow", "This pad is in shadow at the selected time.", "Choose an earlier time or another pad.");
            if (power == null || power.powerSystemId != design.powerSystemId || !power.cabinOn || !power.radioOn || result.availablePowerKw < 3f)
                Issue(result, IssueLevel.Error, "power_test", "The cabin and radio power test is incomplete for the selected power system.", "Turn on the cabin and radio and complete the test for this system.");
            // Report the tested state, not merely whether the selected hardware could power the laser.
            bool laserLoaded = design.cargo != null && design.cargo.Any(x => x != null && x.cargoId == "laser");
            bool invalidLaserState = power != null && ((power.laserOn && power.laserTripped) ||
                ((power.laserOn || power.laserTripped) && !laserLoaded) ||
                (power.laserOn && (!power.cabinOn || !power.radioOn || result.availablePowerKw < 6f)) ||
                (power.laserTripped && result.availablePowerKw >= 6f));
            if (invalidLaserState)
                Issue(result, IssueLevel.Error, "power_test_state", "The saved laser test does not match the selected equipment or power system.", "Repeat the power test with the current cargo and power system.");
            result.laserPowered = power != null && !invalidLaserState && power.powerSystemId == design.powerSystemId && power.laserOn;
            if (bay == null || cargo == null || cargo.items == null || cargo.bayId != design.cargoModuleId || cargo.slotCount != bay.slots || design.cargo == null)
                Issue(result, IssueLevel.Error, "cargo_packing", "Cargo packing is missing or belongs to a different bay.", "Repack and lock the cargo in the selected bay.");
            else
            {
                var ids = new HashSet<string>(); var occupied = new HashSet<int>();
                foreach (var item in cargo.items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id)) { Issue(result, IssueLevel.Error, "cargo_packing", "Cargo contains an invalid or duplicate item.", "Repack the cargo."); continue; }
                    var definition = scenario.cargo == null ? null : scenario.cargo.FirstOrDefault(x => x != null && x.id == item.id);
                    var selection = design.cargo.FirstOrDefault(x => x != null && x.cargoId == item.id);
                    if (definition == null || definition.slots <= 0 || item.startSlot < 0 || item.startSlot >= cargo.slotCount || definition.slots > cargo.slotCount - item.startSlot || (cargo.slotCount == 6 && definition.slots == 2 && item.startSlot / 3 != (item.startSlot + 1) / 3))
                    { Issue(result, IssueLevel.Error, "cargo_packing", "A cargo item does not fit in the bay.", "Repack the cargo using free adjacent slots."); continue; }
                    if (selection == null || selection.startSlot != item.startSlot) Issue(result, IssueLevel.Error, "cargo_mismatch", "Packed cargo does not match the ship design.", "Update the selected cargo and packing together.");
                    if (!item.locked) Issue(result, IssueLevel.Error, "cargo_unlocked", item.id + " is loose.", "Lock it before launch.");
                    for (int i = item.startSlot; i < item.startSlot + definition.slots; i++) if (!occupied.Add(i)) Issue(result, IssueLevel.Error, "cargo_overlap", "Cargo items overlap.", "Move an item to free slots.");
                }
                if (design.cargo.Any(x => x == null || !ids.Contains(x.cargoId))) Issue(result, IssueLevel.Error, "cargo_mismatch", "The ship design includes cargo that is not packed.", "Pack and lock every selected item.");
                if (!ids.Contains("panel") || !ids.Contains("kit")) Issue(result, IssueLevel.Error, "required_cargo", "The panel and insulated repair kit are required.", "Pack and lock both required items.");
            }
            if (result.payloadMarginKg >= 0 && result.payloadMarginKg <= LowMassMarginKg && result.launchCapacityKg > 0)
                Issue(result, IssueLevel.Warning, "mass_margin", "Only " + result.payloadMarginKg + " kg of launch capacity remains.", "Recheck the total before adding cargo.");
            return result;
        }
    }
}
