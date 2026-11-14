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
    }
}
