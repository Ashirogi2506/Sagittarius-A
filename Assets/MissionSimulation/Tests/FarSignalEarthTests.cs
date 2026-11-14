using System.Linq;
using NUnit.Framework;
using UnityEngine;
using MissionSimulation.FarSignal;

namespace MissionSimulation.Tests
{
    public class FarSignalEarthTests
    {
        EarthScenario scenario; EarthLaunchSimulator simulator; EarthDesign design;
        [SetUp] public void Setup()
        {
            scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            simulator = new EarthLaunchSimulator();
            design = new EarthDesign { launcherId="l2", nozzleId="n1", fairingId="f2", engineId="e3", cargoModuleId="c2", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel", startSlot=0 }, new CargoPlacement { cargoId="kit", startSlot=2 } } };
        }
        [Test] public void ValidDesignCalculatesTotals()
        {
            var r=simulator.Validate(scenario,design);
            Assert.IsTrue(r.CanLaunch); Assert.AreEqual(98000000L,r.costUsd); Assert.AreEqual(9520f,r.payloadKg); Assert.AreEqual(10780f,r.launchCapacityKg);
        }
        [Test] public void IncorrectNozzleAndCoverBlockLaunch()
        {
            design.nozzleId="n3"; design.fairingId="f1"; var r=simulator.Validate(scenario,design);
            Assert.IsTrue(r.issues.Any(x=>x.code=="nozzle")); Assert.IsTrue(r.issues.Any(x=>x.code=="fairing")); Assert.IsFalse(r.CanLaunch);
        }
        [Test] public void MissingAndOverlappingCargoBlockLaunch()
        {
            design.cargo=new[] {new CargoPlacement {cargoId="panel",startSlot=0},new CargoPlacement {cargoId="spares",startSlot=1}};
            var r=simulator.Validate(scenario,design); Assert.IsTrue(r.issues.Any(x=>x.code=="required_cargo")); Assert.IsTrue(r.issues.Any(x=>x.code=="cargo_slots"));
        }
        [Test] public void OptionalLaserCanBeSwitchedOff()
        {
            design.cargo=new[] {new CargoPlacement {cargoId="panel",startSlot=0},new CargoPlacement {cargoId="kit",startSlot=2},new CargoPlacement {cargoId="laser",startSlot=3}};
            var r=simulator.Validate(scenario,design); Assert.IsTrue(r.CanLaunch); Assert.IsFalse(r.laserPowered); Assert.IsTrue(r.issues.Any(x=>x.code=="optional_power"));
        }
        [Test] public void RidgeFuelShortageIsWarning()
        {
            design.landingPadId="ridge"; var r=simulator.Validate(scenario,design);
            Assert.IsTrue(r.CanLaunch); Assert.IsTrue(r.issues.Any(x=>x.code=="landing_fuel"));
        }
        [Test] public void BlockedDesignDoesNotAdvance()
        {
            design.nozzleId="n3"; var r=simulator.Evaluate(scenario,design);
            Assert.AreEqual(LaunchOutcome.Blocked,r.outcome); Assert.IsNull(r.nextMissionId); Assert.IsNull(r.designSnapshot);
        }
        [Test] public void ReadyDesignProducesIndependentSnapshot()
        {
            var r=simulator.Evaluate(scenario,design);
            Assert.AreEqual(LaunchOutcome.Ready,r.outcome); Assert.AreEqual("pole-7",r.nextMissionId);
            design.cargo[0].startSlot=2; Assert.AreEqual(0,r.designSnapshot.cargo[0].startSlot);
        }
        [Test] public void WarningsArePreservedInReadinessResult()
        {
            design.landingPadId="ridge"; var r=simulator.Evaluate(scenario,design);
            Assert.AreEqual(LaunchOutcome.ReadyWithWarnings,r.outcome); Assert.IsTrue(r.report.issues.Any(x=>x.code=="landing_fuel"));
        }
        [Test] public void OverBudgetAndOverweightAreBlocking()
        {
            scenario.budgetUsd=1; scenario.busMassKg=50000;
            var r=simulator.Validate(scenario,design); Assert.IsFalse(r.CanLaunch);
            Assert.IsTrue(r.issues.Any(x=>x.code=="mass")); Assert.IsTrue(r.issues.Any(x=>x.code=="budget"));
        }
        [Test] public void InvalidSlotsFailWithoutArithmeticOverflow()
        {
            design.cargo[0].startSlot=int.MaxValue;
            Assert.IsTrue(simulator.Validate(scenario,design).issues.Any(x=>x.code=="cargo_slots"));
        }
        [Test] public void MissingMissionOrDesignReturnsBlockingIssue()
        {
            Assert.AreEqual(LaunchOutcome.Blocked, simulator.Evaluate(null, design).outcome);
            Assert.AreEqual(LaunchOutcome.Blocked, simulator.Evaluate(scenario, null).outcome);
        }
        [Test] public void InvalidScenarioEntriesAreRejected()
        {
            scenario.cargo[0] = null;
            Assert.IsTrue(simulator.Validate(scenario,design).issues.Any(x => x.code == "scenario"));
        }
        [Test] public void DuplicateIdsAreRejected()
        {
            scenario.cargo[1].id = scenario.cargo[0].id;
            Assert.IsTrue(simulator.Validate(scenario,design).issues.Any(x => x.code == "scenario"));
        }
        [Test] public void NonFiniteAndNegativeValuesAreRejected()
        {
            scenario.busPowerKw = float.NaN;
            Assert.IsFalse(simulator.Validate(scenario, design).CanLaunch);
            scenario.busPowerKw = 3; scenario.engines[0].massKg = -1;
            Assert.IsFalse(simulator.Validate(scenario, design).CanLaunch);
        }
        [Test] public void CostOverflowReturnsIssueInsteadOfWrapping()
        {
            scenario.busCostUsd = long.MaxValue;
            Assert.IsTrue(simulator.Validate(scenario,design).issues.Any(x => x.code == "scenario"));
        }
        [Test] public void InsufficientLandingFuelRequiresRestart()
        {
            var result = simulator.CheckLandingFuel(scenario, "ridge", 12);
            Assert.AreEqual(LandingFuelOutcome.InsufficientFuel, result.outcome);
            Assert.IsTrue(result.restartRequired);
        }
        [Test] public void ExactLandingFuelIsSufficient()
        {
            var result = simulator.CheckLandingFuel(scenario, "ridge", 13);
            Assert.AreEqual(LandingFuelOutcome.SufficientFuel, result.outcome);
            Assert.IsFalse(result.restartRequired);
        }
        [Test] public void InvalidFuelOrPadDoesNotPretendMissionFailed()
        {
            Assert.AreEqual(LandingFuelOutcome.InvalidInput, simulator.CheckLandingFuel(scenario, "ridge", float.NaN).outcome);
            var result = simulator.CheckLandingFuel(scenario, "unknown", 12);
            Assert.AreEqual(LandingFuelOutcome.InvalidInput, result.outcome); Assert.IsFalse(result.restartRequired);
        }
    }
}
