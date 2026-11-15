using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;
using UnityEngine;

namespace MissionSimulation.Tests
{
    public class CampaignDraftGatewayTests
    {
        string folder, path;
        CampaignSaveStore store;
        CampaignDraftGateway gateway;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "FarSignalGateway-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "save.json"); store = new CampaignSaveStore(path); gateway = new CampaignDraftGateway(store);
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
        [Test] public void RetryCannotRepeatOrChangeAnOperation()
        {
            store.Create(new CampaignState { spares = 1, inventory = new List<string> { "spares" }, suppliesDelivered = true, pole7RelayRestored = true, routeUploaded = true, marsApproachReceived = true, returnedToShip = true });
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.RecoverAntenna("antenna-1", true).status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.RecoverAntenna("antenna-1", true).status);
            Assert.AreEqual(CampaignSaveStatus.OperationConflict, gateway.RecoverAntenna("antenna-1", false).status);
            Assert.AreEqual(0, gateway.Load().state.spares);
            Assert.IsFalse(gateway.Load().state.inventory.Contains("spares"));
        }
        [Test] public void EarthCheckpointAndDescentSurviveReloadWithoutDoubleFuelDeduction()
        {
            var scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            var design = new EarthDesign { launcherId="l1", nozzleId="n1", fairingId="f1", engineId="e1", cargoModuleId="c1", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 } } };
            var packing = new CargoPackingMinigame("c1", 3); packing.Place("panel", 0); packing.Place("kit", 2); packing.Lock("panel"); packing.Lock("kit");
            var power = new EarthPowerTest("p1", 5f, false); power.SetCabin(true); power.SetRadio(true);
            var checkpoint = new PreLaunchCheckpointService().Create("run-1", new CommanderIdentity { commanderId="commander_female", playerName="Нара", portraitReference="portrait" }, scenario, design, packing.Snapshot(), power.Snapshot(), new LandingPlan { padId="main", hasArrivalMinute=true, arrivalMinute=3 });
            Assert.IsTrue(checkpoint.success);
            gateway.CreateFromEarthCheckpoint(scenario, checkpoint.checkpoint);
            Assert.Throws<InvalidOperationException>(() => gateway.CreateFromEarthCheckpoint(scenario, checkpoint.checkpoint));
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ConfirmDescent("descent-1", scenario, "main", 3).status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.ConfirmDescent("descent-1", scenario, "main", 3).status);
            Assert.AreEqual(CampaignSaveStatus.OperationConflict, gateway.ConfirmDescent("descent-1", scenario, "ridge", 3).status);
            Assert.AreEqual(5f, gateway.Load().state.lunarDescent.remainingFuelUnits);
            Assert.AreEqual("Нара", gateway.Load().state.commanderName);
        }
        [Test] public void BasicRoutePersistsAndResumesAcrossTheRequiredMilestones()
        {
            var scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            var design = new EarthDesign { launcherId="l1", nozzleId="n1", fairingId="f1", engineId="e1", cargoModuleId="c1", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 } } };
            var packing = new CargoPackingMinigame("c1", 3); packing.Place("panel", 0); packing.Place("kit", 2); packing.Lock("panel"); packing.Lock("kit");
            var power = new EarthPowerTest("p1", 5f, false); power.SetCabin(true); power.SetRadio(true);
            var checkpoint = new PreLaunchCheckpointService().Create("basic-run", new CommanderIdentity { commanderId="commander_female", playerName="Нара", portraitReference="portrait" }, scenario, design, packing.Snapshot(), power.Snapshot(), new LandingPlan { padId="main", hasArrivalMinute=true, arrivalMinute=3 });
            Assert.IsTrue(checkpoint.success);
            gateway.CreateFromEarthCheckpoint(scenario, checkpoint.checkpoint);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ConfirmDescent("descent", scenario, "main", 3).status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.ConfirmDescent("descent", scenario, "main", 3).status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.RecoverAntenna("too-early", false).status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.CompleteBenchTest("early-bench", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.MarkRoute("route").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.DeliverSupplies("supply").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.RestoreRelay("bad-relay", false).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.RestoreRelay("relay", true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.UploadRoute("upload").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ReceiveMarsApproach("mars-file").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ReturnToShip("ship").status);
            Assert.IsTrue(CampaignProgress.CanContinueToMars(gateway.Load().state));
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.RecoverAntenna("manual", false).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SendMarsWindow("mars-window", new[] { "arrival-update" }, false).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetBenchConnections("cables", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.CompleteBenchTest("bench", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetBenchConnections("unplug", true, false).status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SeatOrbiter("seat-too-early").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetBenchConnections("reconnect", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.CompleteBenchTest("bench-again", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SelectOrbiterModules("module-change", "directional", "basic", "standard").status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.SelectOrbiterModules("module-change", "directional", "basic", "standard").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SeatOrbiter("seat-after-module-change").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SelectOrbiterModules("module-basic", "light", "basic", "standard").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.CompleteBenchTest("bench-final", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SeatOrbiter("seat").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetClamp("clamp-1", 1, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetClamp("clamp-2", 2, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ReceiveDeploymentPlan("plan").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.LandOnGanymede("land").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SaveReading("reading-b", "grooved-rise", 5, "site-b").status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.SaveReading("reading-b", "grooved-rise", 5, "site-b").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SaveReading("other-reading-b", "grooved-rise", 6, "different-image").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SaveReading("reading-a", "crater-plain", 8, "site-a").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.AcceptComparison("unsupported", "site-b-always-safe").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.AcceptComparison("comparison", "site-b-lower-when-measured").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.ReturnToOrbit("orbit").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.Deploy("early", true, false).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.Deploy("deploy", true, true).status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.Deploy("deploy", true, true).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SendFirstSignalWindow("window-1", new[] { "full-graph", "health", "sample" }).status);
            Assert.IsFalse(CampaignProgress.Acknowledge(gateway.Load().state));
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.SendFirstSignalWindow("window-1", new[] { "full-graph", "health", "sample" }).status);
            Assert.AreEqual(CampaignSaveStatus.OperationConflict, gateway.SendFirstSignalWindow("window-1", new[] { "health", "sample" }).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SendFirstSignalWindow("window-2", new[] { "health", "sample" }).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.Acknowledge("ack").status);
            var final = gateway.Load().state;
            Assert.IsTrue(final.earthAcknowledged); Assert.AreEqual("Нара", final.commanderName);
            Assert.AreEqual(3, final.receivedPackets.Count);
            Assert.IsTrue(final.inventory.Contains("kit")); Assert.IsFalse(final.inventory.Contains("panel"));
        }
        [Test] public void BatteryDeliveryIsOnlyAppliedOnceAcrossReload()
        {
            store.Create(new CampaignState { lunarDescent = new LunarDescentState { committed = true }, inventory = new List<string> { "battery" } });
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.LeaveBattery("battery").status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.LeaveBattery("battery").status);
            Assert.IsTrue(gateway.Load().state.batteryLeftAtBeacon);
            Assert.IsFalse(gateway.Load().state.inventory.Contains("battery"));
        }
    }
}
