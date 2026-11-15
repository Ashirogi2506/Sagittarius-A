using System;
using System.Collections.Generic;

namespace MissionSimulation.FarSignal
{
    [Serializable] public sealed class SurveyRecord
    {
        public string siteId, imageId;
        public long missionTimestampSeconds;
        public int simulatedDoseRateUsvPerHour;
    }
    // Draft campaign values. Persist the complete state atomically after each successful mutation.
    [Serializable] public sealed class CampaignState
    {
        public string runId, commanderId, commanderName, portraitReference, antenna = "light", shield = "basic", sensorMode = "standard";
        public PreLaunchCheckpoint earthCheckpoint;
        public LunarDescentState lunarDescent;
        public List<string> inventory = new List<string>();
        public int spares;
        public bool antennaRecovered, usedSpare, benchPassed, seated, clamp1, clamp2, planReceived, powerCableConnected, dataCableConnected;
        public bool routeChecked, suppliesDelivered, pole7RelayRestored, routeUploaded, marsApproachReceived, returnedToShip, batteryLeftAtBeacon;
        public float alignmentHoldSeconds;
        public bool ganymedeLanded, craterPlainSaved, groovedRiseSaved, comparisonAccepted, returnedToOrbit, deployed, antennaAligned, earthAcknowledged;
        public List<string> receivedPackets = new List<string>();
        public List<string> marsTransitPackets = new List<string>();
        public List<SurveyRecord> surveyRecords = new List<SurveyRecord>();
    }

    public sealed class CampaignProgress
    {
        public const int CradleCapacityKg = 300;
        public static CampaignState CreateFromCheckpoint(EarthScenario scenario, PreLaunchCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            var verified = new PreLaunchCheckpointService().Restore(checkpoint.runId, scenario, checkpoint);
            if (!verified.success) throw new ArgumentException("A valid Earth launch checkpoint is required: " + verified.explanation);
            var saved = verified.checkpoint;
            var state = new CampaignState {
                runId = saved.runId, commanderId = saved.commander.commanderId,
                commanderName = saved.commander.playerName, portraitReference = saved.commander.portraitReference,
                earthCheckpoint = saved,
                lunarDescent = new LunarDescentState { runId = saved.runId, remainingFuelUnits = saved.fuelAtLaunchUnits }
            };
            foreach (var cargo in saved.cargo.items) state.inventory.Add(cargo.id);
            state.spares = state.inventory.Contains("spares") ? 1 : 0;
            return state;
        }
        public static bool CommitLunarDescent(CampaignState s, EarthScenario scenario, string padId, float arrivalMinute, string operationId)
        {
            if (s == null || s.lunarDescent == null || s.runId != s.lunarDescent.runId) return false;
            var result = new LunarDescent().Confirm(scenario, s.lunarDescent, padId, arrivalMinute, operationId);
            if (result.status != DescentStatus.Committed && result.status != DescentStatus.AlreadyCommitted) return false;
            if (result.nextState != null) s.lunarDescent = result.nextState;
            return true;
        }
        public static bool MarkLunarRouteChecked(CampaignState s)
        { if (s == null || s.lunarDescent == null || !s.lunarDescent.committed) return false; s.routeChecked = true; return true; }
        public static bool DeliverPole7Supplies(CampaignState s)
        {
            if (s == null) return false;
            if (s.suppliesDelivered) return true;
            if (!s.routeChecked || s.inventory == null || !s.inventory.Contains("panel") || !s.inventory.Contains("kit")) return false;
            s.inventory.Remove("panel"); s.suppliesDelivered = true; return true;
        }
        public static bool RestorePole7Relay(CampaignState s, bool lineIsolated)
        { if (s == null || !s.suppliesDelivered || !lineIsolated) return false; s.pole7RelayRestored = true; return true; }
        public static bool UploadLunarRoute(CampaignState s)
        { if (s == null || !s.routeChecked || !s.pole7RelayRestored) return false; s.routeUploaded = true; return true; }
        public static bool ReceiveMarsApproach(CampaignState s)
        { if (s == null || !s.routeUploaded || !s.pole7RelayRestored) return false; s.marsApproachReceived = true; return true; }
        public static bool LeaveBatteryAtBeacon(CampaignState s)
        {
            if (s == null) return false;
            if (s.batteryLeftAtBeacon) return true;
            if (s.lunarDescent == null || !s.lunarDescent.committed || s.inventory == null || !s.inventory.Contains("battery")) return false;
            s.inventory.Remove("battery"); s.batteryLeftAtBeacon = true; return true;
        }
        public static bool ReturnToShip(CampaignState s)
        { if (s == null || !s.marsApproachReceived) return false; s.returnedToShip = true; return true; }
        public static bool CanContinueToMars(CampaignState s) => s != null && s.suppliesDelivered && s.pole7RelayRestored && s.routeUploaded && s.marsApproachReceived && s.returnedToShip;
        public static int OrbiterMass(string antenna, string shield)
        {
            if (antenna != "light" && antenna != "directional") throw new ArgumentException("Select a valid antenna.");
            if (shield != "basic" && shield != "reinforced") throw new ArgumentException("Select a valid shield.");
            return 210 + (antenna == "light" ? 20 : 45) + (shield == "basic" ? 20 : 50);
        }
        public static bool SelectModules(CampaignState s, string antenna, string shield, string sensorMode)
        {
            if (s == null || s.deployed || (sensorMode != "standard" && sensorMode != "frequent")) return false;
            try { OrbiterMass(antenna, shield); } catch (ArgumentException) { return false; }
            if (s.antenna == antenna && s.shield == shield && s.sensorMode == sensorMode) return true;
            s.antenna = antenna; s.shield = shield; s.sensorMode = sensorMode;
            s.benchPassed = s.seated = s.clamp1 = s.clamp2 = s.planReceived = false;
            return true;
        }
        public static bool UseSpareForAntenna(CampaignState s)
        {
            if (s == null) return false;
            if (s.usedSpare) return true;
            if (s.antennaRecovered || s.spares < 1) return false;
            s.spares--; if (s.inventory != null) s.inventory.Remove("spares"); s.usedSpare = s.antennaRecovered = true; return true;
        }
        public static bool RecoverAntennaManually(CampaignState s)
        { if (s == null) return false; s.antennaRecovered = true; return true; }
        public static bool TryBenchTest(CampaignState s, bool powerConnected, bool dataConnected)
        {
            if (!SetBenchConnections(s, powerConnected, dataConnected) || !powerConnected || !dataConnected) return false;
            try { OrbiterMass(s.antenna, s.shield); } catch (ArgumentException) { return false; }
            if (s.sensorMode != "standard" && s.sensorMode != "frequent") return false;
            s.benchPassed = true; return true;
        }
        public static bool SetBenchConnections(CampaignState s, bool powerConnected, bool dataConnected)
        {
            if (s == null || s.deployed) return false;
            if (s.powerCableConnected == powerConnected && s.dataCableConnected == dataConnected) return true;
            s.powerCableConnected = powerConnected; s.dataCableConnected = dataConnected;
            s.benchPassed = s.seated = s.clamp1 = s.clamp2 = s.planReceived = false;
            return true;
        }
        public static bool CanLoad(CampaignState s)
        {
            if (s == null || !s.benchPassed) return false;
            try { return OrbiterMass(s.antenna, s.shield) <= CradleCapacityKg; }
            catch (ArgumentException) { return false; }
        }
        public static bool CanTravel(CampaignState s) => CanLoad(s) && s.seated && s.clamp1 && s.clamp2 && s.planReceived;
        public static bool SeatOrbiter(CampaignState s)
        { if (!CanLoad(s) || s.deployed) return false; s.seated = true; return true; }
        public static bool SetClamp(CampaignState s, int index, bool locked)
        {
            if (s == null || s.deployed || !s.seated || (index != 1 && index != 2)) return false;
            if (index == 1) s.clamp1 = locked; else s.clamp2 = locked;
            if (!locked) s.planReceived = false;
            return true;
        }
        public static bool ReceiveDeploymentPlan(CampaignState s)
        { if (s == null || !CanLoad(s) || !s.seated || !s.clamp1 || !s.clamp2) return false; s.planReceived = true; return true; }
        public static bool CanEnableTransitOption(bool cameraOn, bool checkoutOn)
        { return 3 + (cameraOn ? 2 : 0) + (checkoutOn ? 2 : 0) <= 5; }
        public static List<string> SendMarsTransitWindow(CampaignState s, IEnumerable<string> queue, bool archiveAvailable)
        {
            var sent = new List<string>();
            if (s == null || !s.antennaRecovered || queue == null) return sent;
            if (s.marsTransitPackets == null) s.marsTransitPackets = new List<string>();
            int left = 8;
            foreach (var id in queue)
            {
                if (s.marsTransitPackets.Contains(id)) continue;
                int duration = id == "arrival-update" ? 3 : id == "pole-7-archive" && archiveAvailable ? 7 : -1;
                if (duration < 0 || duration > left) break;
                left -= duration; s.marsTransitPackets.Add(id); sent.Add(id);
            }
            return sent;
        }
        public static bool MarsArrivalConfirmed(CampaignState s) => s != null && s.marsTransitPackets != null && s.marsTransitPackets.Contains("arrival-update");
        public static bool LandOnGanymede(CampaignState s)
        {
            if (s == null) return false;
            if (s.ganymedeLanded) return true;
            if (!CanTravel(s)) return false;
            s.ganymedeLanded = true; return true;
        }
        public static bool SaveReading(CampaignState s, string siteId, long missionTimestampSeconds, string imageId)
        {
            if (s == null || !s.ganymedeLanded || s.returnedToOrbit || missionTimestampSeconds < 0 || string.IsNullOrWhiteSpace(imageId)) return false;
            if (siteId != "crater-plain" && siteId != "grooved-rise") return false;
            if (s.surveyRecords == null) s.surveyRecords = new List<SurveyRecord>();
            if (s.surveyRecords.Exists(r => r != null && r.siteId == siteId)) return true;
            s.surveyRecords.Add(new SurveyRecord { siteId = siteId, imageId = imageId, missionTimestampSeconds = missionTimestampSeconds, simulatedDoseRateUsvPerHour = siteId == "crater-plain" ? 24 : 16 });
            if (siteId == "crater-plain") s.craterPlainSaved = true;
            else s.groovedRiseSaved = true;
            return true;
        }
        public static bool AcceptComparison(CampaignState s, string conclusion)
        {
            if (s == null || s.surveyRecords == null ||
                !s.surveyRecords.Exists(r => r != null && r.siteId == "crater-plain") ||
                !s.surveyRecords.Exists(r => r != null && r.siteId == "grooved-rise") ||
                conclusion != "site-b-lower-when-measured") return false;
            s.comparisonAccepted = true; return true;
        }
        public static bool ReturnToOrbit(CampaignState s)
        {
            if (s == null || !s.comparisonAccepted) return false;
            s.returnedToOrbit = true; return true;
        }
        public static bool Deploy(CampaignState s, bool crewReady, bool atReleasePoint)
        {
            if (s == null) return false;
            if (s.deployed) return true;
            if (!CanTravel(s) || !s.returnedToOrbit || !crewReady || !atReleasePoint) return false;
            s.deployed = true; s.antennaAligned = s.antenna == "light"; return true;
        }
        public static bool HoldAntennaAlignment(CampaignState s, bool inTarget, float elapsedSeconds)
        {
            if (s == null || !s.deployed || s.antenna != "directional" || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) return false;
            if (s.antennaAligned) return true;
            if (!inTarget) { s.alignmentHoldSeconds = 0; return false; }
            s.alignmentHoldSeconds = Math.Min(1f, s.alignmentHoldSeconds + elapsedSeconds);
            if (s.alignmentHoldSeconds < 1f) return false;
            s.antennaAligned = true; return true;
        }
        // Completed packets persist; a packet that doesn't fit remains queued for the next window.
        public static List<string> SendWindow(CampaignState s, IEnumerable<string> queue)
        {
            var sent = new List<string>();
            if (s == null || !s.deployed || !s.antennaAligned || queue == null) return sent;
            if (s.receivedPackets == null) s.receivedPackets = new List<string>();
            int left = s.antenna == "directional" ? 14 : 10;
            foreach (var id in queue)
            {
                if (s.receivedPackets.Contains(id)) continue;
                int duration = id == "health" ? 2 : id == "sample" ? 4 : id == "full-graph" ? (s.sensorMode == "frequent" ? 12 : 9) : -1;
                if (duration < 0 || duration > left) break;
                left -= duration; s.receivedPackets.Add(id); sent.Add(id);
            }
            return sent;
        }
        public static bool Acknowledge(CampaignState s)
        {
            if (s == null || s.receivedPackets == null || !s.receivedPackets.Contains("health") || !s.receivedPackets.Contains("sample")) return false;
            s.earthAcknowledged = true; return true;
        }
    }
}
