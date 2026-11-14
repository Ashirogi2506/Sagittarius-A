using NUnit.Framework;
using UnityEngine;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;

namespace MissionSimulation.Tests
{
    public class PreLaunchCheckpointTests
    {
        EarthScenario mission; EarthDesign design; CargoPackingMinigame cargo; EarthPowerTest power; LandingPlan plan; CommanderIdentity commander;
        [SetUp] public void Setup()
        {
            mission = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            design = new EarthDesign { launcherId="l1", nozzleId="n1", fairingId="f1", engineId="e1", cargoModuleId="c1", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 } } };
            cargo = new CargoPackingMinigame("c1",3); cargo.Place("panel",0); cargo.Place("kit",2); cargo.Lock("panel"); cargo.Lock("kit");
            power = new EarthPowerTest("p1",5f,false); power.SetCabin(true); power.SetRadio(true);
            plan = new LandingPlan { padId="main", hasArrivalMinute=true, arrivalMinute=3 };
            commander = new CommanderIdentity { commanderId="commander_female", playerName="????", portraitReference="portrait" };
        }
        CheckpointResult Create() { return new PreLaunchCheckpointService().Create("run-1",commander,mission,design,cargo.Snapshot(),power.Snapshot(),plan); }
        [Test] public void ValidCheckpointCopiesInputsAndRestoresIdentityAndFuel()
        {
            var saved=Create(); Assert.IsTrue(saved.success); Assert.AreEqual(15f,saved.checkpoint.fuelAtLaunchUnits);
            design.cargo[0].startSlot=2; commander.playerName="Changed"; plan.arrivalMinute=9;
            var restored=new PreLaunchCheckpointService().Restore("run-1",mission,saved.checkpoint);
            Assert.IsTrue(restored.success); Assert.AreEqual("????",restored.checkpoint.commander.playerName);
            Assert.AreEqual(0,restored.checkpoint.design.cargo[0].startSlot); Assert.AreEqual(3f,restored.checkpoint.plan.arrivalMinute);
        }
        [Test] public void IncompleteChecklistCannotProduceCheckpoint()
        {
            power.SetRadio(false); var r=Create(); Assert.IsFalse(r.success); Assert.IsNull(r.checkpoint);
        }
        [Test] public void DifferentRunOrModifiedResourcesAreRejected()
        {
            var saved=Create().checkpoint; var service=new PreLaunchCheckpointService();
            Assert.IsFalse(service.Restore("another-run",mission,saved).success);
            saved.fuelAtLaunchUnits=999; Assert.IsFalse(service.Restore("run-1",mission,saved).success);
        }
        [Test] public void ChangedMissionValuesDoNotSilentlyChangeSavedFuel()
        {
            var saved=Create().checkpoint;
            foreach (var engine in mission.engines) if (engine.id=="e1") engine.fuelUnits++;
            Assert.IsFalse(new PreLaunchCheckpointService().Restore("run-1",mission,saved).success);
        }
        [Test] public void ChangedMissionCostRejectsOldCheckpoint()
        {
            var saved=Create().checkpoint;
            foreach (var engine in mission.engines) if (engine.id=="e1") engine.costUsd++;
            Assert.IsFalse(new PreLaunchCheckpointService().Restore("run-1",mission,saved).success);
        }
        [Test] public void ReorderingMissionOptionsDoesNotInvalidateCheckpoint()
        {
            var saved=Create().checkpoint;
            System.Array.Reverse(mission.engines);
            Assert.IsTrue(new PreLaunchCheckpointService().Restore("run-1",mission,saved).success);
        }
        [Test] public void LegacyCheckpointRequiresExplicitMigration()
        {
            var saved=Create().checkpoint; saved.version=1;
            Assert.IsFalse(new PreLaunchCheckpointService().Restore("run-1",mission,saved).success);
        }
        [Test] public void FuelFailureCanRestorePreLaunchSnapshot()
        {
            var checkpoint=Create().checkpoint;
            var landing=new LunarDescent().Preview(mission,new LunarDescentState { runId="run-1", remainingFuelUnits=12 },"ridge",3);
            Assert.AreEqual(RestartDestination.PreLaunchCheckpoint,landing.restartDestination);
            var restored=new PreLaunchCheckpointService().Restore("run-1",mission,checkpoint);
            Assert.IsTrue(restored.success); Assert.AreEqual(15f,restored.checkpoint.fuelAtLaunchUnits);
            Assert.AreEqual("????",restored.checkpoint.commander.playerName);
        }
    }
}
