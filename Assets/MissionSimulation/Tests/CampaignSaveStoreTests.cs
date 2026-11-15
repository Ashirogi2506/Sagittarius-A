using System;
using System.IO;
using NUnit.Framework;
using MissionSimulation.FarSignal;
using UnityEngine;

namespace MissionSimulation.Tests
{
    public class CampaignSaveStoreTests
    {
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "FarSignalTest-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "save.json");
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
        [Test] public void ReloadedRetrySpendsSpareOnlyOnceAndRejectsConflictingOperation()
        {
            var store = new CampaignSaveStore(path);
            store.Create(new CampaignState { commanderName = "Нара", spares = 1 });
            var first = store.Apply("op-1", "use-spare", CampaignProgress.UseSpareForAntenna);
            Assert.AreEqual(CampaignSaveStatus.Applied, first.status);
            var afterReload = new CampaignSaveStore(path);
            var retry = afterReload.Apply("op-1", "use-spare", CampaignProgress.UseSpareForAntenna);
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, retry.status);
            Assert.AreEqual(0, retry.snapshot.state.spares);
            Assert.AreEqual("Нара", retry.snapshot.state.commanderName);
            Assert.AreEqual(1, retry.snapshot.revision);
            Assert.AreEqual(CampaignSaveStatus.OperationConflict, afterReload.Apply("op-1", "another-action", s => true).status);
        }
        [Test] public void RejectedOperationDoesNotPersistMutationsOrReserveId()
        {
            var store = new CampaignSaveStore(path); store.Create(new CampaignState { spares = 2 });
            var rejected = store.Apply("op-2", "test", s => { s.spares = 0; return false; });
            Assert.AreEqual(CampaignSaveStatus.Rejected, rejected.status);
            Assert.AreEqual(2, store.Load().state.spares);
            Assert.AreEqual(CampaignSaveStatus.Applied, store.Apply("op-2", "test", s => { s.spares--; return true; }).status);
        }
        [Test] public void CorruptSaveIsNotReplaced()
        {
            var store = new CampaignSaveStore(path); store.Create(new CampaignState());
            File.WriteAllText(path, "not json");
            Assert.Throws<InvalidDataException>(() => store.Apply("op", "test", s => true));
            Assert.AreEqual("not json", File.ReadAllText(path));
        }
        [Test] public void PacketRetriesAndAcknowledgementPersistAcrossReloads()
        {
            var store = new CampaignSaveStore(path);
            store.Create(new CampaignState { deployed = true, antennaAligned = true });
            Assert.AreEqual(CampaignSaveStatus.Applied, store.Apply("window-1", "graph-first", s => CampaignProgress.SendWindow(s, new[] { "full-graph", "health", "sample" }).Count == 1).status);
            store = new CampaignSaveStore(path);
            Assert.AreEqual(CampaignSaveStatus.AlreadyApplied, store.Apply("window-1", "graph-first", s => throw new Exception("Must not run twice")).status);
            Assert.IsFalse(CampaignProgress.Acknowledge(store.Load().state));
            Assert.AreEqual(CampaignSaveStatus.Applied, store.Apply("window-2", "required-first", s => CampaignProgress.SendWindow(s, new[] { "health", "sample" }).Count == 2).status);
            Assert.AreEqual(CampaignSaveStatus.Applied, store.Apply("ack-1", "acknowledge", CampaignProgress.Acknowledge).status);
            var loaded = new CampaignSaveStore(path).Load();
            Assert.IsTrue(loaded.state.earthAcknowledged);
            Assert.AreEqual(3, loaded.state.receivedPackets.Count);
        }
        [Test] public void ExampleSavesCoverBasicOptionalBlockedAndInterrupted()
        {
            CampaignSaveEnvelope ReadFixture(string name) => JsonUtility.FromJson<CampaignSaveEnvelope>(File.ReadAllText(Path.Combine(Application.dataPath, "MissionSimulation", "Tests", "Fixtures", name + ".json")));
            var basic = ReadFixture("basic"); Assert.AreEqual("Нара", basic.state.commanderName); Assert.IsFalse(CampaignProgress.CanTravel(basic.state));
            var optional = ReadFixture("optional"); Assert.IsTrue(CampaignProgress.UseSpareForAntenna(optional.state)); Assert.AreEqual(0, optional.state.spares);
            var blocked = ReadFixture("blocked"); Assert.AreEqual(305, CampaignProgress.OrbiterMass(blocked.state.antenna, blocked.state.shield)); Assert.IsFalse(CampaignProgress.CanLoad(blocked.state));
            var interrupted = ReadFixture("interrupted"); Assert.IsFalse(CampaignProgress.Acknowledge(interrupted.state));
            CollectionAssert.AreEqual(new[] { "sample" }, CampaignProgress.SendWindow(interrupted.state, new[] { "health", "sample" }));
            Assert.IsTrue(CampaignProgress.Acknowledge(interrupted.state));
        }
    }
}
