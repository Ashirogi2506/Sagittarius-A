using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Mars;
using UnityEngine;

namespace SagittariusA.Gameplay.Ganymede
{
    public enum GanymedePhase { Transit, Surface, Orbit, Contact, Acknowledged, Complete }

    [Serializable]
    public sealed class SurfaceRecord
    {
        public string siteId, location, thumbnailId, timestamp;
        public float missionSeconds;
        public int simulatedDoseRate;
    }

    [Serializable]
    public sealed class GanymedeSave
    {
        public int version = 1, revision, deploymentCount;
        public string runId, handoffJson;
        public MarsGanymedeHandoff handoff;
        public GanymedePhase phase;
        public float missionSeconds;
        public bool cameraOn = true, checkoutComplete, dosimeterCollected;
        public string placedSite;
        public float levelX = .5f, levelY = -.4f;
        public bool instrumentLeveled, comparisonSaved, photoSaved;
        public bool radiationStationBuilt, iceStationBuilt, uplinkBuilt;
        public List<string> installedParts = new List<string>();
        public List<SurfaceRecord> records = new List<SurfaceRecord>();
        public bool crewReady, deploymentPointReached, deployed, antennaLocked;
        public float antennaPosition = -.65f;
        public List<string> queue = new List<string>();
        public List<string> received = new List<string>();
        public float windowRemaining;
        public bool earthAcknowledged, logOpened;
    }

    public interface IGanymedeStore { string Load(); void Save(string json); }
    public sealed class MemoryGanymedeStore : IGanymedeStore
    {
        public string Json { get; private set; } = "";
        public string Load() => Json;
        public void Save(string json) => Json = json;
    }
    public sealed class PlayerPrefsGanymedeStore : IGanymedeStore
    {
        public const string Key = "FarSignal.GanymedeDraft.v1";
        public string Load() => PlayerPrefs.GetString(Key, "");
        public void Save(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
        public static MarsGanymedeHandoff SavedHandoff()
        {
            try { return JsonUtility.FromJson<GanymedeSave>(PlayerPrefs.GetString(Key, ""))?.handoff; }
            catch (ArgumentException) { return null; }
        }
    }

    // The scene renders this model. Inventory, deployment and receipts are committed here only.
    public sealed class GanymedeMission
    {
        public const string SiteA = "crater-plain", SiteB = "grooved-rise";
        public const string Relay = "uplink-relay";
        public const float SampleDuration = 3f;
        private readonly IGanymedeStore store;
        public GanymedeSave State { get; private set; }
        public string Notice { get; private set; }
        public bool CheckoutRunning { get; private set; }
        public bool Sampling { get; private set; }
        public bool ContactRunning { get; private set; }
        public float CheckoutProgress { get; private set; }
        public float SampleProgress { get; private set; }
        public float AlignmentProgress { get; private set; }
        public float PacketProgress { get; private set; }
        public int PowerLoad => 3 + (State.cameraOn ? 2 : 0) + (CheckoutRunning ? 2 : 0);
        public int WindowSeconds => State.handoff.antenna == "directional" ? 14 : 10;
        public bool BothRecords => Record(SiteA) != null && Record(SiteB) != null;
        public bool RequiredPackets => State.received.Contains("health") && State.received.Contains("sample");
        public bool AllStationsBuilt => State.radiationStationBuilt && State.iceStationBuilt && State.uplinkBuilt;
        public bool CanAscend => AllStationsBuilt && BothRecords && State.comparisonSaved && string.IsNullOrEmpty(State.placedSite);
        public event Action Changed;

        public GanymedeMission(MarsGanymedeHandoff handoff, IGanymedeStore storage)
        {
            if (!ValidHandoff(handoff)) throw new ArgumentException("Complete the Mars test, both clamps and deployment plan before Ganymede.");
            store = storage ?? throw new ArgumentNullException(nameof(storage));
            string signature = JsonUtility.ToJson(handoff);
            GanymedeSave saved = null;
            try { saved = JsonUtility.FromJson<GanymedeSave>(store.Load()); }
            catch (ArgumentException) { }
            if (saved != null && saved.version == 1 && saved.handoffJson == signature && saved.records != null && saved.received != null && saved.queue != null)
            {
                State = saved;
                if(State.installedParts == null) State.installedParts = new List<string>();
                Notice = "Progress restored. Unfinished samples and packets can be retried locally.";
            }
            else
            {
                State = new GanymedeSave { runId = Guid.NewGuid().ToString("N"), handoffJson = signature,
                    handoff = JsonUtility.FromJson<MarsGanymedeHandoff>(signature), phase = GanymedePhase.Transit };
                foreach (string id in handoff.practicePassed && handoff.practiceOrder != null ? handoff.practiceOrder : Array.Empty<string>())
                    if (KnownPacket(id) && !State.queue.Contains(id)) State.queue.Add(id);
                foreach (string id in new[] { "health", "sample", "full-graph" }) if (!State.queue.Contains(id)) State.queue.Add(id);
                Save("Sam: That's Ganymede, Commander " + handoff.playerName + ". Test the orbiter before we go down.");
            }
        }

        public static bool ValidHandoff(MarsGanymedeHandoff h) => h != null && !string.IsNullOrEmpty(h.sourceRunId) &&
            h.nextMissionId == "ganymede" && h.build != null && h.orbiterSeated && h.leftClamp && h.rightClamp && h.deploymentPlanReceived &&
            (h.antenna == "light" || h.antenna == "directional") && (h.shield == "basic" || h.shield == "reinforced") &&
            (h.sensorMode == "standard" || h.sensorMode == "frequent") && h.orbiterMassKg <= 300 && h.orbiterMassKg > 0 &&
            h.benchGraph != null && h.benchGraph.samples != null && h.benchGraph.samples.Length == (h.sensorMode == "frequent" ? 12 : 6);

        public SurfaceRecord Record(string id) => State.records.Find(r => r.siteId == id);
        public static string SiteName(string id) => id == SiteA ? "Site A · Crater Plain" : id == SiteB ? "Site B · Grooved Rise" : "Unknown site";
        public static bool KnownSite(string id) => id == SiteA || id == SiteB;
        public static bool KnownStation(string id) => KnownSite(id) || id == Relay;
        public bool StationBuilt(string id) => id == SiteA ? State.radiationStationBuilt : id == SiteB ? State.iceStationBuilt : id == Relay && State.uplinkBuilt;
        public static string StationName(string id) => id == SiteA ? "Radiation station" : id == SiteB ? "Ice survey station" : "Uplink relay";
        public bool FitStationPart(string site, string part, string socket)
        {
            if(State.phase != GanymedePhase.Surface || !State.dosimeterCollected || !KnownStation(site) || StationBuilt(site)) return Reject("Collect the equipment case and approach an unfinished station.");
            if(part != "base" && part != "head" && part != "power") return Reject("Select a station component.");
            if(part != socket) return Reject("Wrong mount. Match the component's shape and label to its socket.");
            if(part != "base" && !State.installedParts.Contains(site+"/base")) return Reject("Secure the base before fitting the head and power connector.");
            string key=site+"/"+part;if(State.installedParts.Contains(key))return Reject("That component is already fitted.");
            State.installedParts.Add(key);return Save(part=="power"?"Power connected. Commission the station when all three checks are ready.":"Component secured. Fit the remaining parts.");
        }
        public bool CommissionStation(string site)
        {
            if(State.phase != GanymedePhase.Surface || !KnownStation(site) || StationBuilt(site))return Reject("Station already commissioned or unavailable.");
            if(!new[]{"base","head","power"}.All(p=>State.installedParts.Contains(site+"/"+p)))return Reject("Fit the base, keyed instrument head and power connector first.");
            if(site==SiteA)State.radiationStationBuilt=true;else if(site==SiteB)State.iceStationBuilt=true;else State.uplinkBuilt=true;
            return Save(StationName(site)+" commissioned. "+(site==Relay?"Surface uplink is ready.":"Use the portable dosimeter here to save a local reading."));
        }
        public static bool KnownPacket(string id) => id == "health" || id == "sample" || id == "full-graph";
        public static string PacketName(string id) => id == "health" ? "Health report" : id == "sample" ? "Small science reading" : "Full particle-count graph";
        public int PacketSeconds(string id) => id == "health" ? 2 : id == "sample" ? 4 : State.handoff.sensorMode == "frequent" ? 12 : 9;

        public bool SetCamera(bool enabled)
        {
            if (State.phase != GanymedePhase.Transit) return Reject("Transit systems are closed.");
            if (enabled && CheckoutRunning) return Reject("One optional system at a time. Stop checkout before turning on the camera.");
            State.cameraOn = enabled; return Save(enabled ? "Exterior camera on · 5 / 5 power units." : "Exterior camera off · 3 / 5 power units.");
        }
        public bool BeginCheckout()
        {
            if (State.phase != GanymedePhase.Transit || State.checkoutComplete || CheckoutRunning) return Reject("Checkout already running or complete.");
            if (State.cameraOn) return Reject("One optional system at a time. Turn off the camera to test the orbiter.");
            CheckoutRunning = true; CheckoutProgress = 0; Notice = "Orbiter checkout running. Cabin and radio remain on."; return true;
        }
        public bool Land()
        {
            if (State.phase != GanymedePhase.Transit || !State.checkoutComplete) return Reject("Complete the orbiter checkout first.");
            State.phase = GanymedePhase.Surface; return Save("Collect the dosimeter at the lander. Survey both marked sites, then compare the records.");
        }
        public bool Collect()
        {
            if (State.phase != GanymedePhase.Surface || State.dosimeterCollected) return Reject("Dosimeter already collected or unavailable.");
            State.dosimeterCollected = true; return Save("Equipment collected. Follow the labelled A, B and C markers to build the stations.");
        }
        public bool Place(string id, bool insideFlatPatch)
        {
            if (State.phase != GanymedePhase.Surface || !State.dosimeterCollected || !KnownSite(id)) return Reject("Collect the dosimeter from the lander first.");
            if(!StationBuilt(id))return Reject("Assemble and commission this station before surveying it.");
            if (!string.IsNullOrEmpty(State.placedSite)) return Reject("Pack the instrument at " + SiteName(State.placedSite) + " first.");
            if (!insideFlatPatch) return Reject("Move inside the marked flat patch. The tripod must not be tilted.");
            if (Record(id) != null) return Reject("This site's record is already saved.");
            State.placedSite = id; State.levelX = .5f; State.levelY = -.4f; State.instrumentLeveled = false;
            return Save("Tripod placed. Center the bubble with both adjustment controls.");
        }
        public bool AdjustLevel(float x, float y)
        {
            if (State.phase != GanymedePhase.Surface || string.IsNullOrEmpty(State.placedSite) || Sampling || State.instrumentLeveled) return false;
            if (!Finite(x) || !Finite(y)) return false;
            State.levelX = Mathf.Clamp(State.levelX + x, -1, 1); State.levelY = Mathf.Clamp(State.levelY + y, -1, 1);
            return Save("Center tolerance: ±0.12 on both axes.");
        }
        public bool ConfirmLevel()
        {
            if (State.phase != GanymedePhase.Surface || string.IsNullOrEmpty(State.placedSite)) return Reject("Place the tripod first.");
            if (Mathf.Abs(State.levelX) > .12f || Mathf.Abs(State.levelY) > .12f) return Reject("Bubble outside the center band. Adjust both legs and retry.");
            State.instrumentLeveled = true; return Save("Instrument level. Switch on the dosimeter for a three-second sample.");
        }
        public bool BeginSample()
        {
            if (State.phase != GanymedePhase.Surface || !State.instrumentLeveled || string.IsNullOrEmpty(State.placedSite) || Record(State.placedSite) != null || Sampling)
                return Reject("Place and level the instrument before taking a new sample.");
            Sampling = true; SampleProgress = 0; Notice = "Sampling · remain near the instrument for 3 seconds."; return true;
        }
        public bool Pack()
        {
            if (State.phase != GanymedePhase.Surface || string.IsNullOrEmpty(State.placedSite)) return Reject("No placed instrument to pack.");
            Sampling = false; SampleProgress = 0; State.placedSite = null; State.instrumentLeveled = false;
            return Save("Instrument packed. Saved site records are retained.");
        }
        public bool Compare(string firstRecordLabel, string secondRecordLabel, bool supportedConclusion)
        {
            if (State.phase != GanymedePhase.Surface || !BothRecords) return Reject("Save readings at both Site A and Site B first.");
            if (firstRecordLabel != SiteA || secondRecordLabel != SiteB) return Reject("Match the crater image to Crater Plain and the grooves image to Grooved Rise.");
            if (!supportedConclusion) return Reject("Sam: We measured each site once. Save where and when. A single reading cannot establish that a place is always safe.");
            State.comparisonSaved = true; return Save("Comparison saved: Site B had the lower reading when we measured it.");
        }
        public bool SavePhoto(bool cratersOnLeft, bool groovesOnRight)
        {
            if (State.phase != GanymedePhase.Surface) return Reject("Visit the photo overlook on the surface.");
            if (!cratersOnLeft || !groovesOnRight) return Reject("Check the shapes: round craters on the left, long pale grooves on the right.");
            State.photoSaved = true; return Save("Terrain panorama and both labels saved in the flight log.");
        }
        public bool Ascend()
        {
            if (State.phase != GanymedePhase.Surface || !CanAscend) return Reject("Departure checklist: all three stations built, both readings, accepted comparison, and packed dosimeter.");
            State.phase = GanymedePhase.Orbit; return Save("Back in orbit. Confirm crew systems and approach the marked deployment point.");
        }
        public bool ConfirmCrew()
        {
            if (State.phase != GanymedePhase.Orbit || State.deployed) return Reject("Crew confirmation unavailable.");
            State.crewReady = true; return Save("Crew systems ready. Both Mars clamps remain secure.");
        }
        public bool ReachDeploymentPoint()
        {
            if (State.phase != GanymedePhase.Orbit || !State.crewReady) return Reject("Confirm crew systems first.");
            State.deploymentPointReached = true; return Save("Deployment point reached. Ready to release.");
        }
        public bool Deploy()
        {
            if (State.phase != GanymedePhase.Orbit || State.deployed || !State.crewReady || !State.deploymentPointReached)
                return Reject(State.deployed ? "Orbiter already deployed." : "Clamps remain secure. Confirm crew systems and reach the marked point first.");
            State.deployed = true; State.deploymentCount = 1; State.phase = GanymedePhase.Contact;
            State.antennaLocked = State.handoff.antenna == "light";
            return Save(State.antennaLocked ? "Orbiter released. Light antenna acquired automatically · 10-second contacts." : "Orbiter released. Point the directional antenna for a 14-second contact.");
        }
        public void AdjustAntenna(float amount)
        {
            if (State.phase != GanymedePhase.Contact || State.antennaLocked || !Finite(amount)) return;
            State.antennaPosition = Mathf.Clamp(State.antennaPosition + amount, -1, 1);
        }
        public bool MovePacketFirst(string id)
        {
            if (ContactRunning || !KnownPacket(id) || State.received.Contains(id)) return Reject("Reorder queued packets between contact windows.");
            State.queue.Remove(id); State.queue.Insert(0, id); return Save("Queue updated. Health and small reading are required; full graph is optional.");
        }
        public bool PrioritizeRequired()
        {
            if (ContactRunning) return Reject("Wait for this contact window to end.");
            State.queue = new[] { "health", "sample", "full-graph" }.Where(id => !State.received.Contains(id)).ToList();
            return Save("Required packets moved to the front.");
        }
        public bool BeginContact()
        {
            if (!State.deployed || !State.antennaLocked || ContactRunning || State.queue.Count == 0) return Reject("Acquire the orbiter signal and queue a packet before opening contact.");
            if (State.phase != GanymedePhase.Contact && State.phase != GanymedePhase.Acknowledged && State.phase != GanymedePhase.Complete) return false;
            State.windowRemaining = WindowSeconds; PacketProgress = 0; ContactRunning = true;
            return Save("Contact open. Packets are sent in queue order.");
        }
        public bool Acknowledge()
        {
            if (!RequiredPackets || State.earthAcknowledged) return Reject("Earth needs the health report and small science reading first.");
            ContactRunning = false; PacketProgress = 0; State.earthAcknowledged = true; State.phase = GanymedePhase.Acknowledged;
            return Save("Earth Flight Control: Commander " + State.handoff.playerName + ", first readings received. The orbiter is working on its own. You can head home.");
        }
        public bool OpenFlightLog()
        {
            if (!State.earthAcknowledged) return Reject("Receive Earth's acknowledgement first.");
            State.phase = GanymedePhase.Complete; State.logOpened = true; return Save("First Signal Received · campaign flight log saved.");
        }

        public void Tick(float seconds, bool nearInstrument)
        {
            if (!Finite(seconds) || seconds <= 0) return;
            float dt = Mathf.Min(seconds, .25f); State.missionSeconds += dt;
            if (CheckoutRunning)
            {
                CheckoutProgress += dt;
                if (CheckoutProgress >= 3f) { CheckoutRunning = false; State.checkoutComplete = true; Save("Checkout complete. Confirm the three-step approach plan to land."); }
            }
            if (Sampling)
            {
                if (!nearInstrument) { Sampling = false; SampleProgress = 0; Notice = "Sample interrupted. Tripod stays leveled; return and switch the dosimeter on again."; }
                else
                {
                    SampleProgress += dt;
                    if (SampleProgress >= SampleDuration)
                    {
                        Sampling = false;
                        if (Record(State.placedSite) == null) State.records.Add(new SurfaceRecord {
                            siteId = State.placedSite, location = SiteName(State.placedSite), thumbnailId = State.placedSite,
                            missionSeconds = State.missionSeconds, timestamp = Timestamp(State.missionSeconds),
                            simulatedDoseRate = State.placedSite == SiteA ? 24 : 16 });
                        Save("Record saved with location and mission time. Pack the instrument to continue.");
                    }
                }
            }
            if (State.phase == GanymedePhase.Contact && !State.antennaLocked)
            {
                AlignmentProgress = Mathf.Abs(State.antennaPosition - .25f) <= .08f ? AlignmentProgress + dt : 0;
                if (AlignmentProgress >= 1f) { State.antennaLocked = true; Save("Directional antenna aligned. Longer contact window unlocked: 14 seconds."); }
            }
            if (ContactRunning)
            {
                float available = Mathf.Min(dt, State.windowRemaining);
                while (available > .00001f && State.queue.Count > 0)
                {
                    string id = State.queue[0]; float used = Mathf.Min(available, PacketSeconds(id) - PacketProgress);
                    PacketProgress += used; available -= used; State.windowRemaining = Mathf.Max(0, State.windowRemaining - used);
                    if (PacketProgress + .0001f >= PacketSeconds(id))
                    {
                        if (!State.received.Contains(id)) State.received.Add(id);
                        State.queue.RemoveAt(0); PacketProgress = 0; Save(PacketName(id) + " received by Earth.");
                    }
                }
                if (State.windowRemaining <= .0001f || State.queue.Count == 0)
                {
                    ContactRunning = false; PacketProgress = 0;
                    Save(RequiredPackets ? "Required packets received. Earth's reply is ready after the signal-travel time skip." : "Contact closed. Unfinished packets remain queued. Move health and small reading first, then retry.");
                }
            }
        }
        public int[] ParticleSamples()
        {
            int count = State.handoff.sensorMode == "frequent" ? 12 : 6;
            int[] samples = new int[count]; for (int i = 0; i < count; i++) samples[i] = 32 + ((i * 17 + 7) % 53);
            return samples;
        }
        public void Flush() => store.Save(JsonUtility.ToJson(State));
        public static string Timestamp(float seconds) => "MET " + TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private bool Reject(string message) { Notice = message; Changed?.Invoke(); return false; }
        private bool Save(string message) { State.revision++; Notice = message; Flush(); Changed?.Invoke(); return true; }
    }
}
