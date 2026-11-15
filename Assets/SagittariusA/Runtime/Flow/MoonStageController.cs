using System;
using System.Collections.Generic;
using System.Linq;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;
using SagittariusA.Gameplay.Data;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    public enum MoonStagePhase { Descent, Pole7CartRepair, Complete, RestartAtPreLaunch, RouteSurvey, Pole7Repair, ReturnToLander }

    public static class MoonInteractionIds
    {
        public const string ConfirmDescent = "moon.descent.confirm";
        public const string StartCart = "pole7.cart.start";
        public const string SwitchCartOff = "pole7.cart.switch_off";
        public const string OpenWheelCover = "pole7.cart.cover_open";
        public const string CleanPatch1 = "pole7.cart.patch_1";
        public const string CleanPatch2 = "pole7.cart.patch_2";
        public const string CleanPatch3 = "pole7.cart.patch_3";
        public const string CloseWheelCover = "pole7.cart.cover_close";
        public const string RetestCart = "pole7.cart.retest";
        public const string PickUpPanel = "pole7.cargo.pickup_panel";
        public const string PickUpKit = "pole7.cargo.pickup_kit";
        public const string LoadPanel = "pole7.cart.load_panel";
        public const string LoadKit = "pole7.cart.load_kit";
        public const string DriveBridge = "pole7.cart.drive_bridge";
        public const string DriveBase = "pole7.cart.drive_base";
        public const string InspectBridge = "pole7.route.inspect_bridge";
        public const string SurveyRoute = "pole7.route.survey";
        public const string DeliverCargo = "pole7.route.deliver";
        public const string IsolateCircuit = "pole7.base.isolate";
        public const string RepairCircuit = "pole7.base.repair";
        public const string RepairLaser = "pole7.base.repair_laser";
        public const string InstallPanel = "pole7.base.install_panel";
        public const string RestoreEssential = "pole7.base.essential";
        public const string RestoreRelay = "pole7.base.relay";
        public const string UploadRoute = "pole7.base.upload";
        public const string ReturnToShip = "pole7.return";
        public const string DriveLander = "pole7.cart.drive_lander";
        public const string InspectBeacon = "pole7.beacon.inspect";
        public const string LeaveBattery = "pole7.beacon.leave_battery";
        public const string InspectSteep = "pole7.route.inspect_steep";
    }

    [Serializable]
    public sealed class MoonStageContext
    {
        public string runId, missionId, commanderId, playerName, carriedCargoId;
        public MoonStagePhase phase;
        public EarthBuild build;
        public EarthChecks earthChecks;
        public LunarDescentState descent;
        public CartWheelState cart;
        public int batteryKwh, supplyKw;
        public bool laserCarried, laserUsable;
        public int routeStep, repairStep;
        public bool beaconMarked, batteryLeft, returnCartArrived, usedLaser, steepInspected;
        public string[] availableInteractionIds;
    }

    [Serializable]
    public sealed class MoonInteractionResult
    {
        public string runId, interactionId;
        // Hold interactions report the elapsed time since their previous callback.
        public float elapsedSeconds;
        public bool completed = true;
    }

    // Provisional handoff for the future Mars stage. The Moon draft remains the
    // durable source of truth until a campaign-wide save contract is introduced.
    [Serializable]
    public sealed class MoonMarsHandoff
    {
        public string sourceRunId, missionId, nextMissionId, commanderId, playerName;
        public EarthBuild build;
        public float remainingFuelUnits;
        public int batteryKwh, supplyKw;
        public bool routeUploaded, relayRestored, beaconMarked, batteryLeft, usedLaser;
    }

    // Implement this interface on the teammate-owned Moon scene bridge.
    // The gameplay controller remains the only owner of mission state and saving.
    public interface IMoonInteractionDriver
    {
        void Initialize(MoonStageContext context, Action<MoonInteractionResult> report);
        void UpdateContext(MoonStageContext context);
        void Pause();
        void Resume();
        void Shutdown();
    }

    [Serializable]
    public sealed class MoonDraftSave
    {
        public int version = 3;
        public string runId, handoffJson, descentOperationId, carriedCargoId;
        public EarthMoonHandoff handoff;
        public LunarDescentState descent;
        public CartWheelState cart = new CartWheelState();
        public MoonStagePhase phase;
        public int routeStep, repairStep;
        public bool beaconMarked, batteryLeft, returnCartArrived, usedLaser, steepInspected;
    }

    public interface IMoonDraftStore
    {
        string Load();
        void Save(string json);
    }

    public sealed class PlayerPrefsMoonDraftStore : IMoonDraftStore
    {
        private const string Key = "FarSignal.MoonDraft.v1";
        public string Load() { return PlayerPrefs.GetString(Key, ""); }
        public void Save(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }

    public sealed class MoonStageController : MonoBehaviour
    {
        private IMoonDraftStore store;
        private MoonDraftSave draft;
        private EarthScenario scenario;
        private EarthGameplaySession earth;
        private IMoonInteractionDriver driver;
        private CartWheelMinigame cart;
        private readonly LunarDescent lunarDescent = new LunarDescent();

        public bool Initialized { get { return draft != null; } }
        public MoonStagePhase Phase { get { return draft == null ? MoonStagePhase.Descent : draft.phase; } }
        public string Notice { get; private set; }
        public float RemainingFuelUnits { get { return draft != null && draft.descent != null ? draft.descent.remainingFuelUnits : 0; } }
        public string SelectedPadId { get { return draft?.handoff?.checks?.padId; } }
        public string[] AvailableInteractions { get { return Available().ToArray(); } }
        public event Action Changed;
        public event Action RestartAtPreLaunchRequested;
        public event Action MoonStageCompleted;

        public MoonMarsHandoff GetMarsHandoff()
        {
            if (draft == null || draft.phase != MoonStagePhase.Complete || draft.repairStep != 6) return null;
            var remainingBuild = Clone(draft.handoff.build);
            remainingBuild.cargo.Remove("panel");
            if (draft.batteryLeft) remainingBuild.cargo.Remove("battery");
            return new MoonMarsHandoff {
                sourceRunId = draft.runId, missionId = draft.handoff.missionId, nextMissionId = "mars-staging",
                commanderId = draft.handoff.commanderId, playerName = draft.handoff.playerName,
                build = remainingBuild, remainingFuelUnits = draft.descent.remainingFuelUnits,
                batteryKwh = draft.handoff.batteryKwh, supplyKw = draft.handoff.supplyKw,
                routeUploaded = true, relayRestored = true, beaconMarked = draft.beaconMarked, batteryLeft = draft.batteryLeft, usedLaser = draft.usedLaser
            };
        }

        private static T Clone<T>(T value)
        {
            return value == null ? default(T) : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        }

        public void Connect(EarthGameplaySession earthSession)
        {
            if (earth == earthSession) return;
            if (earth != null) earth.MoonStageRequested -= Initialize;
            earth = earthSession;
            if (earth == null) return;
            earth.MoonStageRequested += Initialize;
            var handoff = earth.GetMoonHandoff();
            if (handoff != null) Initialize(handoff);
        }

        public void Initialize(EarthMoonHandoff handoff)
        {
            Initialize(handoff, null);
        }

        public void Initialize(EarthMoonHandoff handoff, IMoonDraftStore draftStore)
        {
            LoadScenario();
            if (!ValidHandoff(handoff))
            {
                Notice = "The Earth-to-Moon handoff is incomplete or incompatible.";
                Changed?.Invoke();
                return;
            }

            if (draftStore != null) store = draftStore;
            if (store == null) store = new PlayerPrefsMoonDraftStore();
            string handoffJson = JsonUtility.ToJson(handoff);
            MoonDraftSave saved = null;
            try { saved = JsonUtility.FromJson<MoonDraftSave>(store.Load()); }
            catch (ArgumentException) { saved = null; }

            if (ValidSave(saved, handoffJson))
            {
                draft = saved;
                // Version 1 called the cart milestone Complete. Preserve it as a
                // route-ready checkpoint, not as completion of the Moon chapter.
                if (draft.version == 1)
                {
                    if (draft.phase == MoonStagePhase.Complete) draft.phase = MoonStagePhase.RouteSurvey;
                    draft.version = 2;
                }
                if (draft.version == 2)
                {
                    if (draft.phase == MoonStagePhase.RouteSurvey)
                        draft.routeStep = draft.routeStep == 0 ? 0 : draft.routeStep == 1 ? 2 : 3;
                    else if (draft.phase == MoonStagePhase.Pole7Repair || draft.phase == MoonStagePhase.ReturnToLander || draft.phase == MoonStagePhase.Complete)
                        draft.routeStep = 5;
                    draft.version = 3;
                    Save();
                }
                cart = new CartWheelMinigame(draft.cart);
                Notice = draft.phase == MoonStagePhase.Complete ? "Pole-7 repair complete." : "Moon progress restored.";
            }
            else
            {
                string runId = Guid.NewGuid().ToString("N");
                draft = new MoonDraftSave {
                    runId = runId,
                    handoffJson = handoffJson,
                    handoff = Clone(handoff),
                    descentOperationId = Guid.NewGuid().ToString("N"),
                    descent = new LunarDescentState { runId = runId, remainingFuelUnits = handoff.fuelUnits },
                    cart = new CartWheelState(),
                    phase = MoonStagePhase.Descent
                };
                cart = new CartWheelMinigame(draft.cart);
                Notice = "Confirm the planned lunar descent.";
                Save();
            }
            PushContext(true);
            Changed?.Invoke();
        }

        public void RegisterDriver(IMoonInteractionDriver value)
        {
            if (driver == value) return;
            if (driver != null) driver.Shutdown();
            driver = value;
            if (driver != null && Initialized) driver.Initialize(Context(), ReportInteraction);
            Changed?.Invoke();
        }

        public void UnregisterDriver(IMoonInteractionDriver value)
        {
            if (driver != value) return;
            driver.Shutdown();
            driver = null;
            Changed?.Invoke();
        }

        public void ReportInteraction(MoonInteractionResult result)
        {
            if (!Initialized || result == null || !result.completed || result.runId != draft.runId ||
                string.IsNullOrWhiteSpace(result.interactionId) || !Available().Contains(result.interactionId)) return;

            if (result.interactionId == MoonInteractionIds.ConfirmDescent)
            {
                ConfirmDescent();
                return;
            }

            if (result.interactionId == MoonInteractionIds.InspectBeacon || result.interactionId == MoonInteractionIds.LeaveBattery)
            {
                if (result.interactionId == MoonInteractionIds.InspectBeacon)
                { draft.beaconMarked = true; Notice = "Dead beacon marked on the map. Leaving a battery is optional."; }
                else { draft.batteryLeft = true; Notice = "Portable battery left at the beacon; it will not travel to Mars."; }
                Save(); PushContext(false); Changed?.Invoke(); return;
            }
            if (result.interactionId == MoonInteractionIds.DriveLander)
            {
                draft.returnCartArrived = true;
                Notice = "Cart returning to the landing pad. Return to the lander.";
                Save(); PushContext(false); Changed?.Invoke(); return;
            }
            if (result.interactionId == MoonInteractionIds.InspectSteep)
            {
                draft.steepInspected = true;
                Notice = "Loose, steep ground is unsafe for the cart. Mark the wider firm route instead.";
                Save(); PushContext(false); Changed?.Invoke(); return;
            }

            if (draft.phase == MoonStagePhase.RouteSurvey || draft.phase == MoonStagePhase.Pole7Repair || draft.phase == MoonStagePhase.ReturnToLander)
            {
                if (draft.phase == MoonStagePhase.RouteSurvey)
                {
                    draft.routeStep++;
                    string[] messages = { "Drive the cart to the bridge.", "Cart parked. Inspect the closed bridge.",
                        "The bridge is closed. Survey the ledge for a safe route.", "Firm ground marked. Return to the cart and drive to Pole-7.",
                        "Cart arrived. Deliver the supplies at the cargo door.", "Supplies delivered. Isolate the damaged circuit." };
                    Notice = messages[draft.routeStep];
                    if (draft.routeStep == 5) draft.phase = MoonStagePhase.Pole7Repair;
                }
                else if (draft.phase == MoonStagePhase.Pole7Repair)
                {
                    if (result.interactionId == MoonInteractionIds.RepairLaser) draft.usedLaser = true;
                    draft.repairStep++;
                    string[] messages = { "Circuit isolated. Use the repair kit.", "Circuit repaired. Install the P-4 panel.",
                        "Panel installed. Restore essential power.", "Essential power restored. Restore the relay.",
                        "Relay restored. Upload the route at the map console.", "Route uploaded. Mars file received; return to the lander." };
                    Notice = messages[draft.repairStep - 1];
                    if (draft.repairStep == 6) draft.phase = MoonStagePhase.ReturnToLander;
                }
                else { draft.phase = MoonStagePhase.Complete; Notice = "Moon mission complete. Mars file ready for the next stage."; }
                Save(); PushContext(false); Changed?.Invoke();
                if (draft.phase == MoonStagePhase.Complete) MoonStageCompleted?.Invoke();
                return;
            }

            if (result.interactionId == MoonInteractionIds.PickUpPanel || result.interactionId == MoonInteractionIds.PickUpKit)
            {
                draft.carriedCargoId = result.interactionId == MoonInteractionIds.PickUpPanel ? "panel" : "kit";
                Notice = draft.carriedCargoId == "panel" ? "P-4 panel picked up. Place it on the cart." : "Repair kit picked up. Place it on the cart.";
                Save(); PushContext(false); Changed?.Invoke(); return;
            }

            CartWheelResponse response;
            switch (result.interactionId)
            {
                case MoonInteractionIds.StartCart: response = cart.StartCart(); break;
                case MoonInteractionIds.SwitchCartOff: response = cart.SwitchOff(); break;
                case MoonInteractionIds.OpenWheelCover: response = cart.OpenCover(); break;
                case MoonInteractionIds.CleanPatch1: response = cart.HoldPatch(0, result.elapsedSeconds); break;
                case MoonInteractionIds.CleanPatch2: response = cart.HoldPatch(1, result.elapsedSeconds); break;
                case MoonInteractionIds.CleanPatch3: response = cart.HoldPatch(2, result.elapsedSeconds); break;
                case MoonInteractionIds.CloseWheelCover: response = cart.CloseCover(); break;
                case MoonInteractionIds.RetestCart: response = cart.StartCart(); break;
                case MoonInteractionIds.LoadPanel: response = cart.LoadAndLock("panel"); break;
                case MoonInteractionIds.LoadKit: response = cart.LoadAndLock("kit"); break;
                default: return;
            }

            draft.cart = cart.Snapshot();
            if (response.accepted && (result.interactionId == MoonInteractionIds.LoadPanel || result.interactionId == MoonInteractionIds.LoadKit))
                draft.carriedCargoId = null;
            Notice = response.message;
            bool completedNow = draft.cart.testPassed && draft.cart.panelLoaded && draft.cart.panelLocked &&
                draft.cart.kitLoaded && draft.cart.kitLocked;
            if (completedNow) { draft.phase = MoonStagePhase.RouteSurvey; Notice = "Cart ready. Use its switch to drive to the bridge."; }
            Save();
            PushContext(false);
            Changed?.Invoke();
        }

        public void EndPatchHold()
        {
            if (cart != null) cart.EndHold();
        }

        private void ConfirmDescent()
        {
            var checks = draft.handoff.checks;
            DescentResult result = lunarDescent.Confirm(scenario, draft.descent, checks.padId,
                checks.arrivalMinute, draft.descentOperationId);
            Notice = result.message;
            if (result.status == DescentStatus.Committed || result.status == DescentStatus.AlreadyCommitted)
            {
                if (result.nextState != null) draft.descent = result.nextState;
                draft.phase = MoonStagePhase.Pole7CartRepair;
            }
            else if (result.status == DescentStatus.InsufficientFuel)
            {
                draft.phase = MoonStagePhase.RestartAtPreLaunch;
            }
            Save();
            PushContext(false);
            Changed?.Invoke();
            if (draft.phase == MoonStagePhase.RestartAtPreLaunch) RestartAtPreLaunchRequested?.Invoke();
        }

        private List<string> Available()
        {
            var result = new List<string>();
            if (!Initialized) return result;
            if (draft.phase == MoonStagePhase.Descent)
            {
                result.Add(MoonInteractionIds.ConfirmDescent);
                return result;
            }
            if (draft.phase == MoonStagePhase.RouteSurvey)
            {
                string[] steps = { MoonInteractionIds.DriveBridge, MoonInteractionIds.InspectBridge, MoonInteractionIds.SurveyRoute,
                    MoonInteractionIds.DriveBase, MoonInteractionIds.DeliverCargo };
                if (draft.routeStep >= 0 && draft.routeStep < steps.Length) result.Add(steps[draft.routeStep]);
                if (draft.routeStep == 2 && !draft.steepInspected) result.Add(MoonInteractionIds.InspectSteep);
                if (draft.routeStep >= 2) AddOptionalBeacon(result);
                return result;
            }
            if (draft.phase == MoonStagePhase.Pole7Repair)
            {
                string[] steps = { MoonInteractionIds.IsolateCircuit, MoonInteractionIds.RepairCircuit, MoonInteractionIds.InstallPanel,
                    MoonInteractionIds.RestoreEssential, MoonInteractionIds.RestoreRelay, MoonInteractionIds.UploadRoute };
                if (draft.repairStep >= 0 && draft.repairStep < steps.Length) result.Add(steps[draft.repairStep]);
                if (draft.repairStep == 1 && draft.handoff.build.cargo.Contains("laser") && draft.handoff.supplyKw >= 6 && draft.handoff.checks.laserTested)
                    result.Add(MoonInteractionIds.RepairLaser);
                AddOptionalBeacon(result);
                return result;
            }
            if (draft.phase == MoonStagePhase.ReturnToLander) { result.Add(draft.returnCartArrived ? MoonInteractionIds.ReturnToShip : MoonInteractionIds.DriveLander); AddOptionalBeacon(result); return result; }
            if (draft.phase != MoonStagePhase.Pole7CartRepair) return result;
            CartWheelState state = draft.cart;
            bool allClean = state.cleaned != null && state.cleaned.Length == 3 && state.cleaned.All(x => x);
            if (state.testPassed)
            {
                if (draft.carriedCargoId == "panel") result.Add(MoonInteractionIds.LoadPanel);
                else if (draft.carriedCargoId == "kit") result.Add(MoonInteractionIds.LoadKit);
                else
                {
                    if (!state.panelLoaded) result.Add(MoonInteractionIds.PickUpPanel);
                    if (!state.kitLoaded) result.Add(MoonInteractionIds.PickUpKit);
                }
            }
            else if (!state.foundJam) result.Add(MoonInteractionIds.StartCart);
            else if (state.powered) result.Add(MoonInteractionIds.SwitchCartOff);
            else if (state.coverOpen)
            {
                if (!state.cleaned[0]) result.Add(MoonInteractionIds.CleanPatch1);
                if (!state.cleaned[1]) result.Add(MoonInteractionIds.CleanPatch2);
                if (!state.cleaned[2]) result.Add(MoonInteractionIds.CleanPatch3);
                if (allClean) result.Add(MoonInteractionIds.CloseWheelCover);
            }
            else if (!allClean) result.Add(MoonInteractionIds.OpenWheelCover);
            else result.Add(MoonInteractionIds.RetestCart);
            return result;
        }

        private void AddOptionalBeacon(List<string> result)
        {
            if (!draft.beaconMarked) result.Add(MoonInteractionIds.InspectBeacon);
            else if (!draft.batteryLeft && draft.handoff.build.cargo.Contains("battery")) result.Add(MoonInteractionIds.LeaveBattery);
        }

        private MoonStageContext Context()
        {
            bool carriesLaser = draft.handoff.build.cargo.Contains("laser");
            return new MoonStageContext {
                runId = draft.runId,
                missionId = draft.handoff.missionId,
                commanderId = draft.handoff.commanderId,
                playerName = draft.handoff.playerName,
                carriedCargoId = draft.carriedCargoId,
                phase = draft.phase,
                routeStep = draft.routeStep, repairStep = draft.repairStep,
                beaconMarked = draft.beaconMarked, batteryLeft = draft.batteryLeft, returnCartArrived = draft.returnCartArrived, usedLaser = draft.usedLaser, steepInspected = draft.steepInspected,
                build = Clone(draft.handoff.build),
                earthChecks = Clone(draft.handoff.checks),
                descent = Clone(draft.descent),
                cart = Clone(draft.cart),
                batteryKwh = draft.handoff.batteryKwh,
                supplyKw = draft.handoff.supplyKw,
                laserCarried = carriesLaser,
                laserUsable = carriesLaser && draft.handoff.supplyKw >= 6 && draft.handoff.checks.laserTested,
                availableInteractionIds = Available().ToArray()
            };
        }

        private void PushContext(bool initialize)
        {
            if (driver == null || !Initialized) return;
            if (initialize) driver.Initialize(Context(), ReportInteraction);
            else driver.UpdateContext(Context());
        }

        private void Save()
        {
            if (draft != null && store != null) store.Save(JsonUtility.ToJson(draft));
        }

        private void LoadScenario()
        {
            if (scenario != null) return;
            TextAsset asset = Resources.Load<TextAsset>("FarSignal.Earth.proposed");
            if (asset == null) throw new InvalidOperationException("FarSignal.Earth.proposed mission data is missing.");
            scenario = JsonUtility.FromJson<EarthScenario>(asset.text);
        }

        private bool ValidHandoff(EarthMoonHandoff value)
        {
            return value != null && scenario != null && value.missionId == scenario.nextMissionId &&
                !string.IsNullOrWhiteSpace(value.commanderId) && !string.IsNullOrWhiteSpace(value.playerName) &&
                value.build != null && value.build.parts != null && value.build.parts.Length == 6 &&
                value.build.cargo != null && value.build.cargo.Contains("panel") && value.build.cargo.Contains("kit") &&
                value.checks != null && !string.IsNullOrWhiteSpace(value.checks.padId) &&
                !float.IsNaN(value.checks.arrivalMinute) && !float.IsInfinity(value.checks.arrivalMinute) &&
                value.fuelUnits >= 0 && value.batteryKwh >= 0 && value.supplyKw >= 0;
        }

        private bool ValidSave(MoonDraftSave value, string handoffJson)
        {
            if (value == null || (value.version != 1 && value.version != 2 && value.version != 3) || value.handoffJson != handoffJson ||
                string.IsNullOrWhiteSpace(value.runId) || string.IsNullOrWhiteSpace(value.descentOperationId) ||
                value.handoff == null || value.descent == null || value.descent.runId != value.runId ||
                value.cart == null || value.cart.cleaned == null || value.cart.cleaned.Length != 3) return false;
            if (!Enum.IsDefined(typeof(MoonStagePhase), value.phase) || value.routeStep < 0 || value.routeStep > (value.version < 3 ? 3 : 5) || value.repairStep < 0 || value.repairStep > 6) return false;
            try { new CartWheelMinigame(value.cart); }
            catch (ArgumentException) { return false; }
            if (float.IsNaN(value.descent.remainingFuelUnits) || float.IsInfinity(value.descent.remainingFuelUnits) || value.descent.remainingFuelUnits < 0) return false;
            if (value.phase == MoonStagePhase.Descent && value.descent.committed) return false;
            if (value.phase != MoonStagePhase.Descent && value.phase != MoonStagePhase.RestartAtPreLaunch && !value.descent.committed) return false;
            if (!string.IsNullOrEmpty(value.carriedCargoId) && value.carriedCargoId != "panel" && value.carriedCargoId != "kit") return false;
            if (!string.IsNullOrEmpty(value.carriedCargoId) &&
                (value.phase != MoonStagePhase.Pole7CartRepair || !value.cart.testPassed ||
                 (value.carriedCargoId == "panel" && value.cart.panelLoaded) || (value.carriedCargoId == "kit" && value.cart.kitLoaded))) return false;
            return true;
        }

        private void OnApplicationPause(bool paused)
        {
            if (driver == null) return;
            if (paused) driver.Pause(); else driver.Resume();
        }

        private void OnDestroy()
        {
            if (earth != null) earth.MoonStageRequested -= Initialize;
            if (driver != null) driver.Shutdown();
        }
    }
}
