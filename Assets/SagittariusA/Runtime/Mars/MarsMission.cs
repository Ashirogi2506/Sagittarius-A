using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Flow;
using UnityEngine;

namespace SagittariusA.Gameplay.Mars
{
    // Owns all rules and durable state. Presentation sends revisioned commands;
    // duplicate, stale and out-of-order commands cannot spend or complete twice.
    public sealed class MarsMission
    {
        public const float ManualLockSeconds = 1f;
        public const float ContactWindowSeconds = 8f;
        public const int CradleCapacityKg = 300;
        private readonly IMarsDraftStore store;
        private MarsDraftSave state;
        private float alignmentUnsavedSeconds;

        public MarsDraftSave State { get { return state; } }
        public string Notice { get; private set; }
        public event Action Changed;

        public MarsMission(MoonMarsHandoff handoff, IMarsDraftStore draftStore)
        {
            if (!ValidHandoff(handoff)) throw new ArgumentException("The Moon-to-Mars handoff is incomplete or incompatible.", "handoff");
            store = draftStore ?? throw new ArgumentNullException("draftStore");
            string handoffJson = JsonUtility.ToJson(handoff);
            MarsDraftSave loaded = null;
            try { loaded = JsonUtility.FromJson<MarsDraftSave>(store.Load()); }
            catch (ArgumentException) { loaded = null; }
            if (ValidSave(loaded, handoffJson))
            {
                state = loaded;
                RepairCollections();
                Notice = state.phase == MarsPhase.Complete ? "Mars mission restored complete." : "Mars progress restored.";
            }
            else
            {
                state = new MarsDraftSave {
                    runId = Guid.NewGuid().ToString("N"), handoffJson = handoffJson,
                    handoff = Clone(handoff), phase = MarsPhase.AntennaRecovery
                };
                RepairCollections();
                Notice = "Automatic tracking stopped. Align the antenna with Mars.";
                Save();
            }
        }

        public MarsStageContext Context()
        {
            return new MarsStageContext {
                runId = state.runId, commanderId = state.handoff.commanderId, playerName = state.handoff.playerName,
                phase = state.phase, revision = state.revision, inventory = Clone(state.handoff.build),
                remainingFuelUnits = state.handoff.remainingFuelUnits,
                antennaAngle = state.antennaAngle, antennaTarget = state.antennaTarget,
                antennaLockSeconds = state.antennaLockSeconds, contactSecondsLeft = state.contactSecondsLeft,
                antennaMisses = state.antennaMisses, showTarget = state.showTarget,
                antennaLocked = state.antennaLocked, spareSpent = state.spareSpent, antennaRecovery = state.antennaRecovery,
                packetQueue = state.packetQueue.ToArray(), completedPackets = state.completedPackets.ToArray(),
                sensorMode = state.sensorMode, antenna = state.antenna, shield = state.shield,
                selectedPart = state.selectedPart, massKg = MassKg, sampleCount = SampleCount,
                withinCapacity = WithinCapacity, sensorInstalled = state.sensorInstalled,
                antennaInstalled = state.antennaInstalled, shieldInstalled = state.shieldInstalled,
                powerConnected = state.powerConnected, dataConnected = state.dataConnected,
                benchPowered = state.benchPowered, sampleTaken = state.sampleTaken, testPassed = CurrentTestPassed,
                benchGraph = Clone(state.benchGraph), orbiterSeated = state.orbiterSeated,
                loaderAtCradle = state.loaderAtCradle, leftClamp = state.leftClamp, rightClamp = state.rightClamp,
                planReceived = state.planReceived, canDepart = CanDepart,
                deploymentPlan = state.deploymentPlan, firstContactSchedule = state.firstContactSchedule,
                dustDiaryComplete = state.dustDiaryComplete, dustSelectedTime = state.dustSelectedTime,
                dustSelectedIndex = state.dustSelectedIndex, practiceQueue = state.practiceQueue.ToArray(),
                practiceDelivered = state.practiceDelivered.ToArray(), practicePassed = state.practicePassed,
                routeUploaded = state.handoff.routeUploaded, relayRestored = state.handoff.relayRestored,
                beaconMarked = state.handoff.beaconMarked, batteryLeft = state.handoff.batteryLeft, usedLaser = state.handoff.usedLaser
            };
        }

        public int MassKg { get { return 210 + (state.antenna == "directional" ? 45 : 20) + (state.shield == "reinforced" ? 50 : 20); } }
        public int SampleCount { get { return state.sensorMode == "frequent" ? 12 : 6; } }
        public bool WithinCapacity { get { return MassKg <= CradleCapacityKg; } }
        public bool AllModulesInstalled { get { return state.sensorInstalled && state.antennaInstalled && state.shieldInstalled; } }
        public bool CurrentTestPassed { get { return state.testPassed && state.benchGraph != null && state.benchGraph.buildSignature == BuildSignature(); } }
        public bool CanDepart { get { return state.phase == MarsPhase.MarsSurface && CurrentTestPassed && state.orbiterSeated && state.loaderAtCradle && state.leftClamp && state.rightClamp && state.planReceived; } }

        public bool TickManualAlignment(float direction, float deltaTime)
        {
            if (state.phase != MarsPhase.AntennaRecovery || state.antennaLocked || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return false;
            float remaining = Mathf.Min(deltaTime, 1f);
            float input = float.IsNaN(direction) || float.IsInfinity(direction) ? 0 : Mathf.Clamp(direction, -1f, 1f);
            while (remaining > 0 && !state.antennaLocked)
            {
                float step = Mathf.Min(remaining, 1f / 60f); remaining -= step;
                state.antennaAngle = Mathf.Clamp(state.antennaAngle + input * 55f * step, -90f, 90f);
                bool inBand = Mathf.Abs(state.antennaAngle - state.antennaTarget) <= 5f;
                state.antennaLockSeconds = inBand ? Mathf.Min(ManualLockSeconds, state.antennaLockSeconds + step) : 0f;
                if (state.antennaLockSeconds >= ManualLockSeconds) state.antennaLocked = true;
            }
            if (state.antennaLockSeconds >= ManualLockSeconds)
            {
                state.antennaLocked = true; state.antennaRecovery = "manual"; state.phase = MarsPhase.ArrivalPackets;
                Notice = "That's Mars. Queue the crew status + estimated arrival packet.";
            }
            alignmentUnsavedSeconds += deltaTime;
            if (state.antennaLocked || alignmentUnsavedSeconds >= .2f) { Save(); alignmentUnsavedSeconds = 0; }
            Changed?.Invoke();
            return state.antennaLocked;
        }

        public MarsActionResult Apply(MarsAction command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.operationId)) return Reject("Invalid Mars command.");
            if (state.appliedOperations.Contains(command.operationId)) return new MarsActionResult { status = MarsActionStatus.AlreadyApplied, message = "This operation was already applied." };
            if (command.runId != state.runId) return Reject("This command belongs to a different Mars run.");
            if (command.expectedRevision != state.revision) return Reject("Mars state changed. Refresh this panel and retry.");
            string message;
            if (!ApplyRules(command.kind, out message)) return Reject(message);
            state.appliedOperations.Add(command.operationId);
            if (state.appliedOperations.Count > 96) state.appliedOperations.RemoveRange(0, state.appliedOperations.Count - 96);
            state.revision++;
            Notice = message;
            Save(); Changed?.Invoke();
            return new MarsActionResult { status = MarsActionStatus.Applied, message = message };
        }

        public MarsActionResult Act(MarsActionKind kind)
        {
            return Apply(new MarsAction { runId = state.runId, expectedRevision = state.revision,
                operationId = Guid.NewGuid().ToString("N"), kind = kind });
        }

        public MarsGanymedeHandoff GetGanymedeHandoff()
        {
            if (state.phase != MarsPhase.Complete || !state.testPassed || !state.orbiterSeated || !state.leftClamp || !state.rightClamp || !state.planReceived) return null;
            return new MarsGanymedeHandoff {
                sourceRunId = state.runId, missionId = "mars-staging", nextMissionId = "ganymede",
                commanderId = state.handoff.commanderId, playerName = state.handoff.playerName,
                build = Clone(state.handoff.build), remainingFuelUnits = state.handoff.remainingFuelUnits,
                batteryKwh = state.handoff.batteryKwh, supplyKw = state.handoff.supplyKw,
                routeUploaded = state.handoff.routeUploaded, relayRestored = state.handoff.relayRestored,
                beaconMarked = state.handoff.beaconMarked, batteryLeft = state.handoff.batteryLeft, usedLaser = state.handoff.usedLaser,
                antennaRecovery = state.antennaRecovery, sensorMode = state.sensorMode, antenna = state.antenna,
                shield = state.shield, orbiterMassKg = MassKg, sampleCount = SampleCount,
                benchGraph = Clone(state.benchGraph), orbiterSeated = state.orbiterSeated,
                leftClamp = state.leftClamp, rightClamp = state.rightClamp, deploymentPlanReceived = state.planReceived,
                deploymentPlan = state.deploymentPlan, firstContactSchedule = state.firstContactSchedule,
                dustDiaryComplete = state.dustDiaryComplete, dustSelectedTime = state.dustSelectedTime,
                dustSelectedIndex = state.dustSelectedIndex, practicePassed = state.practicePassed,
                practiceOrder = state.practiceQueue.ToArray()
            };
        }

        private bool ApplyRules(MarsActionKind kind, out string message)
        {
            message = "Action unavailable.";
            if (kind == MarsActionKind.RecordAntennaMiss)
            {
                if (state.phase != MarsPhase.AntennaRecovery) return false;
                if (Mathf.Abs(state.antennaAngle - state.antennaTarget) <= 5f) { message = "Hold in the target band for one second."; return false; }
                state.antennaMisses++; state.showTarget = state.antennaMisses >= 2;
                message = state.showTarget ? "Two misses recorded. Show target assistance is now available." : DirectionHint(); return true;
            }
            if (kind == MarsActionKind.UseSpare)
            {
                if (state.phase != MarsPhase.AntennaRecovery || state.spareSpent || state.handoff.build == null || !state.handoff.build.cargo.Contains("spares"))
                { message = "No carried spare parts box is available."; return false; }
                state.handoff.build.cargo.Remove("spares"); state.spareSpent = true; state.antennaLocked = true;
                state.antennaRecovery = "automatic-spare"; state.phase = MarsPhase.ArrivalPackets;
                message = "One spare parts box spent. Automatic Mars tracking restored."; return true;
            }
            if (kind == MarsActionKind.QueueArrival || kind == MarsActionKind.QueueArchive)
            {
                if (state.phase != MarsPhase.ArrivalPackets) return false;
                string id = kind == MarsActionKind.QueueArrival ? "arrival-update" : "pole-7-archive";
                if (id == "pole-7-archive" && !state.handoff.routeUploaded) { message = "No Pole-7 route archive is available in this save."; return false; }
                if (state.completedPackets.Contains(id) || state.packetQueue.Contains(id)) { message = "That packet is already queued or delivered."; return false; }
                state.packetQueue.Add(id); message = PacketLabel(id) + " queued (" + PacketSeconds(id) + " s)."; return true;
            }
            if(kind==MarsActionKind.PrioritizeArrival)
            {
                if(state.phase!=MarsPhase.ArrivalPackets||state.completedPackets.Contains("arrival-update"))return false;
                state.packetQueue.Remove("arrival-update");state.packetQueue.Insert(0,"arrival-update");
                message="Arrival update first: 3 seconds. The archive can wait for another contact.";return true;
            }
            if (kind == MarsActionKind.SendNextPacket)
            {
                if (state.phase != MarsPhase.ArrivalPackets || state.packetQueue.Count == 0) { message = "Queue a packet first."; return false; }
                string id = state.packetQueue[0]; float seconds = PacketSeconds(id);
                if (seconds > state.contactSecondsLeft + .001f)
                { state.contactSecondsLeft = 0; message = PacketLabel(id) + " did not fit. It remains first in the queue for the next contact."; return true; }
                state.contactSecondsLeft -= seconds; state.packetQueue.RemoveAt(0);
                if (!state.completedPackets.Contains(id)) state.completedPackets.Add(id);
                message = PacketLabel(id) + " delivered." + (id == "arrival-update" ? " Mars approach confirmed; landing is available." : "");
                return true;
            }
            if (kind == MarsActionKind.StartContactWindow)
            {
                if (state.phase != MarsPhase.ArrivalPackets || state.contactSecondsLeft > .001f) { message = "The current contact window is still open."; return false; }
                state.contactSecondsLeft = ContactWindowSeconds; message = "Fresh 8-second contact window open. Delivered packets remain delivered."; return true;
            }
            if (kind == MarsActionKind.Land)
            {
                if (state.phase != MarsPhase.ArrivalPackets || !state.completedPackets.Contains("arrival-update")) { message = "Send the arrival update before landing."; return false; }
                state.phase = MarsPhase.MarsSurface; message = "Mars landing complete. Follow the marked approach to the assembly bay."; return true;
            }

            if (state.phase != MarsPhase.MarsSurface) return false;
            if(kind==MarsActionKind.ReturnOrbiter)
            {
                if(!state.orbiterSeated){message="Orbiter is already at the assembly stand.";return false;}
                state.orbiterSeated=false;state.loaderAtCradle=false;state.leftClamp=false;state.rightClamp=false;state.planReceived=false;
                message="Orbiter returned to its stand. Changing hardware requires a new bench test.";return true;
            }
            if (kind == MarsActionKind.SetStandardSensor || kind == MarsActionKind.SetFrequentSensor)
            { string value = kind == MarsActionKind.SetFrequentSensor ? "frequent" : "standard"; return ChangeModule(ref state.sensorMode, value, "Sensor mode set to " + value + ".", out message); }
            if (kind == MarsActionKind.SelectLightAntenna || kind == MarsActionKind.SelectDirectionalAntenna)
            { string value = kind == MarsActionKind.SelectDirectionalAntenna ? "directional" : "light"; return ChangeModule(ref state.antenna, value, "Antenna set to " + value + ".", out message); }
            if (kind == MarsActionKind.SelectBasicShield || kind == MarsActionKind.SelectReinforcedShield)
            { string value = kind == MarsActionKind.SelectReinforcedShield ? "reinforced" : "basic"; return ChangeModule(ref state.shield, value, "Shield set to " + value + ".", out message); }
            if (kind == MarsActionKind.SelectSensorPart || kind == MarsActionKind.SelectAntennaPart || kind == MarsActionKind.SelectShieldPart)
            { if(state.orbiterSeated){message="Return the orbiter to assembly first.";return false;} state.selectedPart = kind == MarsActionKind.SelectSensorPart ? "sensor" : kind == MarsActionKind.SelectAntennaPart ? "antenna" : "shield"; message = "Selected " + state.selectedPart + ". Choose its labelled mount."; return true; }
            if (kind == MarsActionKind.PlaceSensorMount || kind == MarsActionKind.PlaceAntennaMount || kind == MarsActionKind.PlaceShieldMount)
            {
                string mount = kind == MarsActionKind.PlaceSensorMount ? "sensor" : kind == MarsActionKind.PlaceAntennaMount ? "antenna" : "shield";
                if (string.IsNullOrEmpty(state.selectedPart)) { message = "Select a module from the shelf first."; return false; }
                if (state.selectedPart != mount) { message = "Mount mismatch. " + Capital(state.selectedPart) + " fits the labelled " + state.selectedPart + " mount."; return false; }
                if (mount == "sensor") state.sensorInstalled = true; else if (mount == "antenna") state.antennaInstalled = true; else state.shieldInstalled = true;
                state.selectedPart = null; InvalidateTest(); message = Capital(mount) + " snapped into its keyed mount."; return true;
            }
            if (kind == MarsActionKind.ConnectPower || kind == MarsActionKind.ConnectData)
            {
                if(state.orbiterSeated){message="Return the orbiter to the stand before changing connections.";return false;}
                bool power = kind == MarsActionKind.ConnectPower;
                if (!AllModulesInstalled) { message = "Install all three modules before connecting cables."; return false; }
                if (power) state.powerConnected = !state.powerConnected; else state.dataConnected = !state.dataConnected;
                InvalidateTest(); message = (power ? "POWER" : "DATA") + " cable " + ((power ? state.powerConnected : state.dataConnected) ? "connected." : "disconnected. Test invalidated."); return true;
            }
            if (kind == MarsActionKind.BenchPowerOn)
            {
                if (!AllModulesInstalled || !state.powerConnected) { message = "Install every module and connect the labelled POWER cable."; return false; }
                state.benchPowered = true; state.sampleTaken = false; message = "Bench power on. Take a simulated sample."; return true;
            }
            if (kind == MarsActionKind.BenchSample)
            {
                if (!state.benchPowered) { message = "Power on the bench first."; return false; }
                state.sampleTaken = true; message = "Simulated sample captured. Transmit it to the hangar receiver."; return true;
            }
            if (kind == MarsActionKind.BenchTransmit)
            {
                if (!state.sampleTaken) { message = "Take a simulated sample before transmitting."; return false; }
                if (!state.dataConnected)
                {
                    state.testPassed = false; state.benchGraph = new MarsBenchGraph { buildSignature = null, samples = Array.Empty<int>() };
                    message = "The sensor is on, but DATA is unplugged. Graph blank: connect the DATA cable and test again."; return true;
                }
                state.testPassed = true; state.benchGraph = new MarsBenchGraph { buildSignature = BuildSignature(), samples = SimulatedSamples(SampleCount) };
                message = "Mars Bench Test received. Save that graph; the next one will come from Jupiter."; return true;
            }
            if (kind == MarsActionKind.SeatOrbiter)
            {
                if (!WithinCapacity) { message = "5 kg over the cradle limit. Switch to the light antenna or basic shield."; return false; }
                if (!CurrentTestPassed) { message = "A confirmed test for the current build is required before loading."; return false; }
                state.orbiterSeated = true; state.loaderAtCradle = true; message = "Loader moved on its fixed lane. Orbiter seated in the ship cradle."; return true;
            }
            if (kind == MarsActionKind.CloseLeftClamp || kind == MarsActionKind.CloseRightClamp)
            {
                if (!state.orbiterSeated || !state.loaderAtCradle) { message = "Seat the orbiter before closing clamps."; return false; }
                if (kind == MarsActionKind.CloseLeftClamp) state.leftClamp = true; else state.rightClamp = true;
                message = (kind == MarsActionKind.CloseLeftClamp ? "Left" : "Right") + " clamp locked."; return true;
            }
            if (kind == MarsActionKind.ReceiveDeploymentPlan)
            {
                if (!state.orbiterSeated) { message = "Load the tested orbiter before requesting its deployment plan."; return false; }
                state.planReceived = true; state.deploymentPlan = "Release after Ganymede ascent; verify health before science packets.";
                state.firstContactSchedule = "First orbiter contact: +00:18 after release (fictional schedule).";
                message = "Deployment plan and first contact schedule received."; return true;
            }
            if (kind == MarsActionKind.CompleteMars)
            {
                if (!CanDepart) { message = !state.leftClamp || !state.rightClamp ? "Both distinct cargo clamps must be locked." : "Assembly, current test, seating and plan receipt are required."; return false; }
                state.phase = MarsPhase.Complete; message = "Mars staging complete. Ganymede handoff prepared; Ganymede gameplay is not part of this slice."; return true;
            }
            if (kind == MarsActionKind.DustAnswerMorning || kind == MarsActionKind.DustAnswerAfternoon)
            {
                if (kind == MarsActionKind.DustAnswerMorning) { message = "Match the time on the photo to the time on the chart."; return false; }
                state.dustDiaryComplete = true; state.dustSelectedTime = "14:00"; state.dustSelectedIndex = 7;
                message = "Dust Diary saved: 14:00, Simulated dust index 7. The picture suggested haze; the measurement checked it."; return true;
            }
            if (kind == MarsActionKind.PracticeClear)
            { state.practiceQueue.Clear(); state.practiceDelivered.Clear(); state.practicePassed = false; message = "Practice queue reset for a fresh 10-second window."; return true; }
            if (kind == MarsActionKind.PracticeQueueHealth || kind == MarsActionKind.PracticeQueueSample || kind == MarsActionKind.PracticeQueueGraph)
            {
                string id = kind == MarsActionKind.PracticeQueueHealth ? "health" : kind == MarsActionKind.PracticeQueueSample ? "sample" : "full-graph";
                if (state.practiceQueue.Contains(id) || state.practiceDelivered.Contains(id)) { message = "That practice packet is already queued or delivered."; return false; }
                state.practiceQueue.Add(id); message = Capital(id) + " queued (" + PracticeSeconds(id) + " s)."; return true;
            }
            if (kind == MarsActionKind.PracticeSend)
            {
                if (state.practiceQueue.Count == 0) { message = "Queue practice packets first."; return false; }
                int remaining = 10; state.practiceDelivered.Clear();
                foreach (string id in state.practiceQueue)
                {
                    int seconds = PracticeSeconds(id); if (seconds > remaining) break;
                    remaining -= seconds; state.practiceDelivered.Add(id);
                }
                state.practicePassed = state.practiceDelivered.Contains("health") && state.practiceDelivered.Contains("sample");
                message = state.practicePassed ? "Practice passed. Editable Ganymede suggestion saved." : "Practice failed. The 9-second graph leaves too little time for both required packets; clear and reorder."; return true;
            }
            return false;
        }

        private bool ChangeModule(ref string field, string value, string success, out string message)
        {
            if (state.orbiterSeated) { message = "The orbiter is already seated. Reopen loading before changing hardware."; return false; }
            bool changed = field != value; field = value;
            if (changed) InvalidateTest();
            message = success + " Mass: " + MassKg + " / " + CradleCapacityKg + " kg." + (!WithinCapacity ? " 5 kg over the cradle limit. Switch to the light antenna or basic shield." : "");
            return true;
        }

        private void InvalidateTest()
        {
            state.benchPowered = false; state.sampleTaken = false; state.testPassed = false;
            state.benchGraph = new MarsBenchGraph(); state.orbiterSeated = false; state.loaderAtCradle = false;
            state.leftClamp = false; state.rightClamp = false; state.planReceived = false;
            state.deploymentPlan = null; state.firstContactSchedule = null;
        }

        private string BuildSignature()
        { return state.sensorMode + "|" + state.antenna + "|" + state.shield + "|" + state.powerConnected + "|" + state.dataConnected; }
        private string DirectionHint() { return state.antennaAngle < state.antennaTarget ? "Signal is stronger to the right." : "Signal is stronger to the left."; }
        private static int[] SimulatedSamples(int count)
        { var result = new int[count]; for (int i = 0; i < count; i++) result[i] = 18 + ((i * 7 + i * i * 3) % 29); return result; }
        public static float PacketSeconds(string id) { return id == "arrival-update" ? 3f : id == "pole-7-archive" ? 7f : float.PositiveInfinity; }
        public static string PacketLabel(string id) { return id == "arrival-update" ? "Crew status + estimated arrival" : id == "pole-7-archive" ? "Pole-7 route archive" : id; }
        private static int PracticeSeconds(string id) { return id == "health" ? 2 : id == "sample" ? 4 : id == "full-graph" ? 9 : 99; }
        private static string Capital(string value) { return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1); }

        private void Save() { store.Save(JsonUtility.ToJson(state)); }
        private MarsActionResult Reject(string message) { Notice = message; return new MarsActionResult { status = MarsActionStatus.Rejected, message = message }; }
        private static bool ValidHandoff(MoonMarsHandoff h)
        { return h != null && h.nextMissionId == "mars-staging" && !string.IsNullOrWhiteSpace(h.sourceRunId) && !string.IsNullOrWhiteSpace(h.commanderId) && !string.IsNullOrWhiteSpace(h.playerName) && h.build != null && h.build.cargo != null; }
        private static bool ValidSave(MarsDraftSave saved, string handoffJson)
        { return saved != null && saved.version == 1 && !string.IsNullOrWhiteSpace(saved.runId) && saved.handoffJson == handoffJson && saved.handoff != null; }
        private void RepairCollections()
        {
            if (state.packetQueue == null) state.packetQueue = new List<string>();
            if (state.completedPackets == null) state.completedPackets = new List<string>();
            if (state.practiceQueue == null) state.practiceQueue = new List<string>();
            if (state.practiceDelivered == null) state.practiceDelivered = new List<string>();
            if (state.appliedOperations == null) state.appliedOperations = new List<string>();
            if (state.benchGraph == null) state.benchGraph = new MarsBenchGraph();
        }
        private static T Clone<T>(T value) { return value == null ? default(T) : JsonUtility.FromJson<T>(JsonUtility.ToJson(value)); }
    }
}
