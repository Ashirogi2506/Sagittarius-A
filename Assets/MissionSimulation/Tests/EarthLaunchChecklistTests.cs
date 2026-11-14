using System.Linq;
using NUnit.Framework;
using UnityEngine;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;

namespace MissionSimulation.Tests
{
    public class EarthLaunchChecklistTests
    {
        EarthScenario scenario; EarthDesign design; CargoPackingMinigame packing; EarthPowerTest power;
        [SetUp] public void SetUp()
        {
            scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            design = new EarthDesign { launcherId="l1", nozzleId="n1", fairingId="f1", engineId="e1", cargoModuleId="c1", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel",startSlot=0 },new CargoPlacement { cargoId="kit",startSlot=2 } } };
            packing = new CargoPackingMinigame(3); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("panel"); packing.Lock("kit");
            power = new EarthPowerTest("p1",5f,false); power.SetCabin(true); power.SetRadio(true);
        }
        LaunchReport Check(float minute) { return new EarthLaunchChecklist().Validate(scenario, design, packing.Snapshot(), power.Snapshot(), new LandingPlan { padId="main",hasArrivalMinute=true,arrivalMinute=minute }); }
        [Test] public void BasicBuildHasExpectedTotalsAndPasses()
        {
            var report = Check(3); Assert.IsTrue(report.CanLaunch); Assert.AreEqual(93000000L, report.costUsd); Assert.AreEqual(9570f, report.payloadKg); Assert.AreEqual(10100f, report.launchCapacityKg);
        }
        [Test] public void MissingPowerTestAndUnlockedCargoBlockLaunch()
        {
            power.SetRadio(false); packing = new CargoPackingMinigame(3); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("kit");
            var report = new EarthLaunchChecklist().Validate(scenario, design, packing.Snapshot(), power.Snapshot(), new LandingPlan { padId="main",hasArrivalMinute=true,arrivalMinute=3 });
            Assert.IsFalse(report.CanLaunch); Assert.IsTrue(report.issues.Any(x=>x.code=="power_test")); Assert.IsTrue(report.issues.Any(x=>x.code=="cargo_unlocked"));
        }
        [Test] public void BoundaryOfSunlightIsExclusive()
        {
            Assert.IsFalse(Check(3).issues.Any(x=>x.code=="landing_shadow")); Assert.IsTrue(Check(4).issues.Any(x=>x.code=="landing_shadow"));
        }
        [Test] public void MissingPlanBlocksLaunch()
        {
            var r = new EarthLaunchChecklist().Validate(scenario,design,packing.Snapshot(),power.Snapshot(),null);
            Assert.IsFalse(r.CanLaunch); Assert.IsTrue(r.issues.Any(x=>x.code=="landing_plan"));
        }
        [Test] public void InsufficientLaserPowerTripsOnlyLaser()
        {
            var p = new EarthPowerTest("p1",5f,true); p.SetRadio(true); p.SetCabin(true);
            Assert.IsFalse(p.TryLaser()); var s=p.Snapshot(); Assert.IsTrue(s.cabinOn); Assert.IsTrue(s.radioOn); Assert.IsTrue(s.laserTripped); Assert.IsFalse(s.laserOn);
            var upgraded = new EarthPowerTest("p2",8f,true); upgraded.SetCabin(true); upgraded.SetRadio(true); Assert.IsTrue(upgraded.TryLaser());
        }
    }
}
