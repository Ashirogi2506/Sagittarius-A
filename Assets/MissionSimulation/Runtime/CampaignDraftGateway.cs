using System;
using System.Globalization;
using System.Text;

namespace MissionSimulation.FarSignal
{
    // Local integration draft. The gameplay owner decides when and where to call this gateway.
    // Every mutating action is persisted before it is acknowledged to a caller.
    public sealed class CampaignDraftGateway
    {
        private readonly CampaignSaveStore store;
        public CampaignDraftGateway(CampaignSaveStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }
        public static CampaignDraftGateway ForSlot(string slotId) => new CampaignDraftGateway(CampaignSaveStore.ForSlot(slotId));
        public void CreateFromEarthCheckpoint(EarthScenario scenario, PreLaunchCheckpoint checkpoint)
        { store.Create(CampaignProgress.CreateFromCheckpoint(scenario, checkpoint)); }
        public CampaignSaveEnvelope Load() => store.Load();

        // Base64 fields and invariant numbers avoid ambiguous operation-key concatenation.
        private static string Field(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        private static string FloatKey(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private CampaignSaveResult Apply(string id, string key, Func<CampaignState, bool> action) => store.Apply(id, key, action);
        private static bool AtMars(CampaignState s) => CampaignProgress.CanContinueToMars(s) && CampaignProgress.MarsArrivalConfirmed(s);

        public CampaignSaveResult ConfirmDescent(string id, EarthScenario mission, string padId, float arrivalMinute)
            => Apply(id, "descent:" + Field(padId) + ":" + FloatKey(arrivalMinute), s => s.earthCheckpoint != null && new PreLaunchCheckpointService().Restore(s.runId, mission, s.earthCheckpoint).success && CampaignProgress.CommitLunarDescent(s, mission, padId, arrivalMinute, id));
        public CampaignSaveResult MarkRoute(string id) => Apply(id, "pole7:route", CampaignProgress.MarkLunarRouteChecked);
        public CampaignSaveResult DeliverSupplies(string id) => Apply(id, "pole7:deliver", CampaignProgress.DeliverPole7Supplies);
        public CampaignSaveResult RestoreRelay(string id, bool lineIsolated)
            => Apply(id, "pole7:relay:" + lineIsolated, s => CampaignProgress.RestorePole7Relay(s, lineIsolated));
        public CampaignSaveResult UploadRoute(string id) => Apply(id, "pole7:upload", CampaignProgress.UploadLunarRoute);
        public CampaignSaveResult ReceiveMarsApproach(string id) => Apply(id, "pole7:mars-file", CampaignProgress.ReceiveMarsApproach);
        public CampaignSaveResult ReturnToShip(string id) => Apply(id, "pole7:return", CampaignProgress.ReturnToShip);
        public CampaignSaveResult LeaveBattery(string id) => Apply(id, "pole7:battery", CampaignProgress.LeaveBatteryAtBeacon);
        public CampaignSaveResult RecoverAntenna(string id, bool useSpare)
            => Apply(id, "mars:antenna:" + useSpare, s => CampaignProgress.CanContinueToMars(s) && (useSpare ? CampaignProgress.UseSpareForAntenna(s) : CampaignProgress.RecoverAntennaManually(s)));
        public CampaignSaveResult SendMarsWindow(string id, string[] packetIds, bool archiveAvailable)
        {
            string[] queue = CopyQueue(packetIds);
            return Apply(id, "mars:window:" + archiveAvailable + ":" + QueueKey(queue), s => CampaignProgress.CanContinueToMars(s) && CampaignProgress.SendMarsTransitWindow(s, queue, archiveAvailable).Count > 0);
        }
        public CampaignSaveResult SelectOrbiterModules(string id, string antenna, string shield, string sensorMode)
            => Apply(id, "orbiter:modules:" + Field(antenna) + ":" + Field(shield) + ":" + Field(sensorMode), s => AtMars(s) && CampaignProgress.SelectModules(s, antenna, shield, sensorMode));
        public CampaignSaveResult SetBenchConnections(string id, bool powerConnected, bool dataConnected)
            => Apply(id, "orbiter:cables:" + powerConnected + ":" + dataConnected, s => AtMars(s) && CampaignProgress.SetBenchConnections(s, powerConnected, dataConnected));
        public CampaignSaveResult CompleteBenchTest(string id, bool powerConnected, bool dataConnected)
            => Apply(id, "orbiter:bench:" + powerConnected + ":" + dataConnected, s => AtMars(s) && CampaignProgress.TryBenchTest(s, powerConnected, dataConnected));
        public CampaignSaveResult CompleteBenchTestWithGraph(string id, bool powerConnected, bool dataConnected, int[] simulatedParticleCounts)
        {
            if (simulatedParticleCounts == null) throw new ArgumentNullException(nameof(simulatedParticleCounts));
            var samples = (int[])simulatedParticleCounts.Clone();
            var keys = new string[samples.Length];
            for (int i = 0; i < samples.Length; i++) keys[i] = samples[i].ToString(CultureInfo.InvariantCulture);
            return Apply(id, "orbiter:bench-graph:" + powerConnected + ":" + dataConnected + ":" + string.Join(":", keys),
                s => AtMars(s) && CampaignProgress.TryBenchTest(s, powerConnected, dataConnected) && CampaignOptionalRecords.SaveBenchGraph(s, samples));
        }
        public CampaignSaveResult SavePassingPracticeQueue(string id, string[] packetIds)
        {
            var queue = CopyQueue(packetIds);
            return Apply(id, "mars:practice:" + QueueKey(queue), s => AtMars(s) && CampaignOptionalRecords.SavePassingPracticeQueue(s, queue));
        }
        public CampaignSaveResult SeatOrbiter(string id) => Apply(id, "orbiter:seat", CampaignProgress.SeatOrbiter);
        public CampaignSaveResult SetClamp(string id, int index, bool locked)
            => Apply(id, "orbiter:clamp:" + index.ToString(CultureInfo.InvariantCulture) + ":" + locked, s => CampaignProgress.SetClamp(s, index, locked));
        public CampaignSaveResult ReceiveDeploymentPlan(string id) => Apply(id, "orbiter:plan", CampaignProgress.ReceiveDeploymentPlan);
        public CampaignSaveResult LandOnGanymede(string id) => Apply(id, "ganymede:land", CampaignProgress.LandOnGanymede);
        public CampaignSaveResult SaveReading(string id, string siteId, long missionTimestampSeconds, string imageId)
            => Apply(id, "survey:" + Field(siteId) + ":" + missionTimestampSeconds.ToString(CultureInfo.InvariantCulture) + ":" + Field(imageId), s =>
            {
                var previous = s.surveyRecords == null ? null : s.surveyRecords.Find(r => r != null && r.siteId == siteId);
                if (previous != null && (previous.missionTimestampSeconds != missionTimestampSeconds || previous.imageId != imageId)) return false;
                return CampaignProgress.SaveReading(s, siteId, missionTimestampSeconds, imageId);
            });
        public CampaignSaveResult AcceptComparison(string id, string conclusion)
            => Apply(id, "survey:comparison:" + Field(conclusion), s => CampaignProgress.AcceptComparison(s, conclusion));
        public CampaignSaveResult SaveTerrainPhoto(string id, string imageId, string darkSiteId, string brightSiteId)
            => Apply(id, "survey:photo:" + Field(imageId) + ":" + Field(darkSiteId) + ":" + Field(brightSiteId),
                s => CampaignOptionalRecords.SaveTerrainPhoto(s, imageId, darkSiteId, brightSiteId));
        public CampaignSaveResult ReturnToOrbit(string id) => Apply(id, "ganymede:orbit", CampaignProgress.ReturnToOrbit);
        public CampaignSaveResult Deploy(string id, bool crewReady, bool atReleasePoint)
            => Apply(id, "orbiter:deploy:" + crewReady + ":" + atReleasePoint, s => CampaignProgress.Deploy(s, crewReady, atReleasePoint));
        // The gameplay controller must establish a continuous in-target hold before calling.
        public CampaignSaveResult AlignDirectionalAntenna(string id, float continuousHoldSeconds)
            => Apply(id, "orbiter:align:" + FloatKey(continuousHoldSeconds), s => continuousHoldSeconds >= 1f && !float.IsInfinity(continuousHoldSeconds) && CampaignProgress.HoldAntennaAlignment(s, true, continuousHoldSeconds));
        public CampaignSaveResult SendFirstSignalWindow(string id, string[] packetIds)
        {
            string[] queue = CopyQueue(packetIds);
            return Apply(id, "signal:window:" + QueueKey(queue), s => CampaignProgress.SendWindow(s, queue).Count > 0);
        }
        public CampaignSaveResult Acknowledge(string id) => Apply(id, "signal:ack", CampaignProgress.Acknowledge);
        private static string[] CopyQueue(string[] ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            return (string[])ids.Clone();
        }
        private static string QueueKey(string[] ids)
        {
            var encoded = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) encoded[i] = Field(ids[i]);
            return string.Join(":", encoded);
        }
    }
}
