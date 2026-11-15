using System;
using System.Collections.Generic;
using System.IO;
using MissionSimulation.FarSignal;
using NUnit.Framework;

namespace MissionSimulation.Tests
{
    public class CampaignOptionalRecordsTests
    {
        string folder, path;
        CampaignDraftGateway gateway;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "FarSignalRecords-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "save.json");
            var store = new CampaignSaveStore(path);
            store.Create(new CampaignState { suppliesDelivered = true, pole7RelayRestored = true, routeUploaded = true,
                marsApproachReceived = true, returnedToShip = true, marsTransitPackets = new List<string> { "arrival-update" } });
            gateway = new CampaignDraftGateway(store);
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
        [Test] public void BenchGraphIsAtomicAndInvalidatedByModuleOrCableChanges()
        {
            var samples = new[] { 2, 3, 5, 8, 13, 21 };
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.CompleteBenchTestWithGraph("bad", true, false, samples).status);
            Assert.IsFalse(gateway.Load().state.benchPassed);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.CompleteBenchTestWithGraph("short", true, true, new[] { 1 }).status);
            Assert.IsFalse(gateway.Load().state.benchPassed);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.CompleteBenchTestWithGraph("graph", true, true, samples).status);
            samples[0] = 999;
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            CollectionAssert.AreEqual(new[] { 2, 3, 5, 8, 13, 21 }, gateway.Load().state.marsBenchSamples);
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.CompleteBenchTestWithGraph("graph", true, true, new[] { 2, 3, 5, 8, 13, 21 }).status);
            Assert.AreEqual(CampaignSaveStatus.OperationConflict, gateway.CompleteBenchTestWithGraph("graph", true, true, samples).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SelectOrbiterModules("frequent", "light", "basic", "frequent").status);
            var cleared = gateway.Load().state; Assert.IsTrue(cleared.marsBenchSamples == null || cleared.marsBenchSamples.Length == 0); Assert.IsFalse(cleared.benchPassed);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.CompleteBenchTestWithGraph("wrong-count", true, true, new[] { 1, 2, 3, 4, 5, 6 }).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.CompleteBenchTestWithGraph("frequent-test", true, true, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SetBenchConnections("unplug", true, false).status);
            cleared = gateway.Load().state; Assert.IsTrue(cleared.marsBenchSamples == null || cleared.marsBenchSamples.Length == 0); Assert.IsFalse(cleared.benchPassed);
        }
        [Test] public void PassingPracticeQueuePersistsAndIsDefensivelyCopied()
        {
            CollectionAssert.AreEqual(new[] { "health", "sample", "full-graph" }, CampaignOptionalRecords.SuggestedFirstContactQueue(gateway.Load().state));
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SavePassingPracticeQueue("fail", new[] { "full-graph", "health", "sample" }).status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SavePassingPracticeQueue("duplicate", new[] { "health", "sample", "health" }).status);
            var queue = new[] { "sample", "health", "full-graph" };
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SavePassingPracticeQueue("pass", queue).status);
            queue[0] = "full-graph";
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            var suggestion = CampaignOptionalRecords.SuggestedFirstContactQueue(gateway.Load().state);
            CollectionAssert.AreEqual(new[] { "sample", "health", "full-graph" }, suggestion);
            suggestion[0] = "health";
            Assert.AreEqual("sample", gateway.Load().state.marsPracticeQueue[0]);
        }
        [Test] public void TerrainPhotoRequiresCorrectLabelsAndPersistsOnce()
        {
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SaveTerrainPhoto("early", "photo", "crater-plain", "grooved-rise").status);
            // Use the save store as a test fixture to model a completed landing.
            var store = new CampaignSaveStore(path);
            Assert.AreEqual(CampaignSaveStatus.Applied, store.Apply("land-fixture", "test-land", s => { s.ganymedeLanded = true; return true; }).status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SaveTerrainPhoto("wrong", "photo", "grooved-rise", "crater-plain").status);
            Assert.AreEqual(CampaignSaveStatus.Applied, gateway.SaveTerrainPhoto("photo-1", "photo", "crater-plain", "grooved-rise").status);
            gateway = new CampaignDraftGateway(new CampaignSaveStore(path));
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, gateway.SaveTerrainPhoto("photo-1", "photo", "crater-plain", "grooved-rise").status);
            Assert.AreEqual(CampaignSaveStatus.Rejected, gateway.SaveTerrainPhoto("another", "different", "crater-plain", "grooved-rise").status);
            Assert.AreEqual("photo", gateway.Load().state.terrainPhotoId);
        }
    }
}
