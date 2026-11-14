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
            packing = new CargoPackingMinigame("c1",3); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("panel"); packing.Lock("kit");
            power = new EarthPowerTest("p1",5f,false); power.SetCabin(true); power.SetRadio(true);
        }
        LaunchReport Check(float minute) { return new EarthLaunchChecklist().Validate(scenario, design, packing.Snapshot(), power.Snapshot(), new LandingPlan { padId="main",hasArrivalMinute=true,arrivalMinute=minute }); }
        [Test] public void BasicBuildHasExpectedTotalsAndPasses()
        {
            var report = Check(3); Assert.IsTrue(report.CanLaunch); Assert.AreEqual(93000000L, report.costUsd); Assert.AreEqual(9570f, report.payloadKg); Assert.AreEqual(10100f, report.launchCapacityKg);
        }
        [Test] public void MissingPowerTestAndUnlockedCargoBlockLaunch()
        {
            power.SetRadio(false); packing = new CargoPackingMinigame("c1",3); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("kit");
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
        [Test] public void ReplacingPowerSystemRequiresFreshTest()
        {
            design.powerSystemId = "p2";
            var r = Check(3); Assert.IsFalse(r.CanLaunch); Assert.IsTrue(r.issues.Any(x=>x.code=="power_test"));
        }
        [Test] public void SmallPayloadMarginProducesWarning()
        {
            scenario.launchers.First(x=>x.id=="l1").basePayloadKg = 9670f;
            var r = Check(3); Assert.AreEqual(100f,r.payloadMarginKg); Assert.IsTrue(r.issues.Any(x=>x.code=="mass_margin"));
        }
        [Test] public void PackingFromDifferentBayWithSameCapacityIsRejected()
        {
            design.cargoModuleId = "c4"; design.fairingId = "f3";
            packing = new CargoPackingMinigame("c3",6); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("panel"); packing.Lock("kit");
            var report = Check(3); Assert.IsTrue(report.issues.Any(x=>x.code=="cargo_packing")); Assert.IsFalse(report.CanLaunch);
        }
        [Test] public void LaserCannotBeReportedPoweredBeforeItIsTested()
        {
            design.powerSystemId="p2"; design.cargoModuleId="c2"; design.fairingId="f2";
            design.cargo = new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 }, new CargoPlacement { cargoId="laser",startSlot=3 } };
            packing = new CargoPackingMinigame("c2",4); packing.Place("panel",0); packing.Place("kit",2); packing.Place("laser",3); packing.Lock("panel"); packing.Lock("kit"); packing.Lock("laser");
            power = new EarthPowerTest("p2",8f,true); power.SetCabin(true); power.SetRadio(true);
            Assert.IsFalse(Check(3).laserPowered);
            Assert.IsTrue(power.TryLaser()); Assert.IsTrue(Check(3).laserPowered);
        }
        [Test] public void ImpossibleSavedLaserStateBlocksLaunch()
        {
            var impossible = power.Snapshot(); impossible.laserOn = true;
            var result = new EarthLaunchChecklist().Validate(scenario,design,packing.Snapshot(),impossible,new LandingPlan { padId="main", hasArrivalMinute=true, arrivalMinute=3 });
            Assert.IsTrue(result.issues.Any(x=>x.code=="power_test_state")); Assert.IsFalse(result.CanLaunch); Assert.IsFalse(result.laserPowered);
        }
        [Test] public void PowerTestRestoresWithoutSharingSnapshot()
        {
            var saved = power.Snapshot(); EarthPowerTest restored;
            Assert.IsTrue(EarthPowerTest.TryRestore(scenario, design, saved, out restored));
            saved.radioOn = false; Assert.IsTrue(restored.EssentialsPassed);
        }
        [Test] public void PowerTestRejectsChangedSystemAndImpossibleLaser()
        {
            var saved = power.Snapshot(); EarthPowerTest restored;
            design.powerSystemId = "p2"; Assert.IsFalse(EarthPowerTest.TryRestore(scenario, design, saved, out restored));
            design.powerSystemId = "p1"; saved.laserOn = true;
            Assert.IsFalse(EarthPowerTest.TryRestore(scenario, design, saved, out restored)); Assert.IsNull(restored);
        }
        [Test] public void PowerTestRestoresLaserTripWhenEquipmentMatches()
        {
            design.cargoModuleId = "c2"; design.fairingId = "f2";
            design.cargo = new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 }, new CargoPlacement { cargoId="laser",startSlot=3 } };
            var test = new EarthPowerTest("p1",5f,true); test.SetCabin(true); test.SetRadio(true); Assert.IsFalse(test.TryLaser());
            EarthPowerTest restored; Assert.IsTrue(EarthPowerTest.TryRestore(scenario, design, test.Snapshot(), out restored));
            Assert.IsTrue(restored.Snapshot().laserTripped); Assert.IsTrue(restored.EssentialsPassed);
        }
        [Test] public void LoadedLaserWarnsOnlyAfterAnOverloadTest()
        {
            design.launcherId="l2"; design.cargoModuleId="c2"; design.fairingId="f2";
            design.cargo = new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 }, new CargoPlacement { cargoId="laser",startSlot=3 } };
            packing = new CargoPackingMinigame("c2",4); packing.Place("panel",0); packing.Place("kit",2); packing.Place("laser",3); packing.Lock("panel"); packing.Lock("kit"); packing.Lock("laser");
            power = new EarthPowerTest("p1",5f,true); power.SetCabin(true); power.SetRadio(true);
            var before = Check(3); Assert.IsFalse(before.issues.Any(x=>x.code=="optional_power")); Assert.AreEqual(3f,before.requiredPowerKw);
            Assert.IsFalse(power.TryLaser()); var after = Check(3);
            Assert.IsTrue(after.CanLaunch); Assert.IsTrue(after.issues.Any(x=>x.code=="optional_power")); Assert.IsFalse(after.laserPowered); Assert.AreEqual(3f,after.requiredPowerKw);
        }
    }
}
