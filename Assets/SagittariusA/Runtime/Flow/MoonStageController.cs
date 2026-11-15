using System;
using System.Collections.Generic;
using System.Linq;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;
using SagittariusA.Gameplay.Data;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    public enum MoonStagePhase { Descent, Pole7CartRepair, Complete, RestartAtPreLaunch }

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
        public const string LoadPanel = "pole7.cart.load_panel";
        public const string LoadKit = "pole7.cart.load_kit";
    }

    [Serializable]
    public sealed class MoonStageContext
    {
        public string runId, missionId, commanderId, playerName;
        public MoonStagePhase phase;
        public EarthBuild build;
        public EarthChecks earthChecks;
        public LunarDescentState descent;
        public CartWheelState cart;
        public int batteryKwh, supplyKw;
        public bool laserCarried, laserUsable;
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
        public int version = 1;
        public string runId, handoffJson, descentOperationId;
        public EarthMoonHandoff handoff;
        public LunarDescentState descent;
        public CartWheelState cart = new CartWheelState();
        public MoonStagePhase phase;
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
        public string[] AvailableInteractions { get { return Available().ToArray(); } }
        public event Action Changed;
        public event Action RestartAtPreLaunchRequested;
        public event Action MoonStageCompleted;

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
            Notice = response.message;
            bool completedNow = draft.cart.testPassed && draft.cart.panelLoaded && draft.cart.panelLocked &&
                draft.cart.kitLoaded && draft.cart.kitLocked;
            if (completedNow) draft.phase = MoonStagePhase.Complete;
            Save();
            PushContext(false);
            Changed?.Invoke();
            if (completedNow) MoonStageCompleted?.Invoke();
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
            if (draft.phase != MoonStagePhase.Pole7CartRepair) return result;
            CartWheelState state = draft.cart;
            bool allClean = state.cleaned != null && state.cleaned.Length == 3 && state.cleaned.All(x => x);
            if (!state.foundJam) result.Add(MoonInteractionIds.StartCart);
            else if (state.powered) result.Add(MoonInteractionIds.SwitchCartOff);
            else if (state.coverOpen)
            {
                if (!state.cleaned[0]) result.Add(MoonInteractionIds.CleanPatch1);
                if (!state.cleaned[1]) result.Add(MoonInteractionIds.CleanPatch2);
                if (!state.cleaned[2]) result.Add(MoonInteractionIds.CleanPatch3);
                if (allClean) result.Add(MoonInteractionIds.CloseWheelCover);
            }
            else if (!allClean) result.Add(MoonInteractionIds.OpenWheelCover);
            else if (!state.testPassed) result.Add(MoonInteractionIds.RetestCart);
            else
            {
                if (!state.panelLoaded) result.Add(MoonInteractionIds.LoadPanel);
                if (!state.kitLoaded) result.Add(MoonInteractionIds.LoadKit);
            }
            return result;
        }

        private MoonStageContext Context()
        {
            bool carriesLaser = draft.handoff.build.cargo.Contains("laser");
            return new MoonStageContext {
                runId = draft.runId,
                missionId = draft.handoff.missionId,
                commanderId = draft.handoff.commanderId,
                playerName = draft.handoff.playerName,
                phase = draft.phase,
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
            if (value == null || value.version != 1 || value.handoffJson != handoffJson ||
                string.IsNullOrWhiteSpace(value.runId) || string.IsNullOrWhiteSpace(value.descentOperationId) ||
                value.handoff == null || value.descent == null || value.descent.runId != value.runId ||
                value.cart == null || value.cart.cleaned == null || value.cart.cleaned.Length != 3) return false;
            try { new CartWheelMinigame(value.cart); }
            catch (ArgumentException) { return false; }
            if (float.IsNaN(value.descent.remainingFuelUnits) || float.IsInfinity(value.descent.remainingFuelUnits) || value.descent.remainingFuelUnits < 0) return false;
            if (value.phase == MoonStagePhase.Descent && value.descent.committed) return false;
            if (value.phase != MoonStagePhase.Descent && value.phase != MoonStagePhase.RestartAtPreLaunch && !value.descent.committed) return false;
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
