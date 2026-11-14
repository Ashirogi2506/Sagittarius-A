using System;
using System.Linq;
using SagittariusA.Gameplay.Data;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    public enum EarthActivity { Cargo, Power, Landing, Transit }
    [Serializable] public sealed class EarthMinigameContext
    {
        public string runId, missionId, commanderId, playerName;
        public int buildRevision;
        public EarthActivity activity;
        public EarthBuild build;
        public EarthChecks checks;
        public int fuelUnits, batteryKwh, supplyKw;
    }
    [Serializable] public sealed class EarthMinigameResult
    {
        public string runId;
        public int buildRevision;
        public EarthActivity activity;
        public bool completed;
        public EarthChecks checks;
        public string switchedOffDeviceId;
    }
    [Serializable] public sealed class EarthMoonHandoff
    {
        public string missionId, commanderId, playerName, switchedOffDeviceId;
        public EarthBuild build;
        public EarthChecks checks;
        public int fuelUnits, batteryKwh, supplyKw, launchCostUsd, launchMassMarginKg;
    }
    // Implement on a teammate-owned MonoBehaviour, then register it with the session.
    // The runner owns its panel and closes it before invoking finished, including on cancellation.
    public interface IEarthMinigameRunner
    {
        bool Supports(EarthActivity activity);
        void Begin(EarthMinigameContext context, Action<EarthMinigameResult> finished);
        void Pause();
        void Resume();
        void Cancel();
    }
    [Serializable] public sealed class EarthDraftSave
    {
        public int version = 1, revision;
        public EarthBuild build = new EarthBuild();
        public EarthChecks checks = new EarthChecks();
        public bool launched, transitCompleted;
        public string switchedOffDeviceId;
    }
    public interface IEarthDraftStore
    {
        string Load();
        void Save(string json);
    }
    public sealed class PlayerPrefsEarthDraftStore : IEarthDraftStore
    {
        private const string Key = "FarSignal.EarthGarageDraft.v1";
        public string Load() => PlayerPrefs.GetString(Key, "");
        public void Save(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }
    public sealed class EarthGameplaySession : MonoBehaviour
    {
        private IEarthDraftStore store;
        private EarthDraftSave draft;
        private IEarthSimulationAdapter simulator = new DraftEarthSimulationAdapter();
        private IEarthMinigameRunner runner;
        private string activeRun;
        private EarthActivity activeActivity;
        public EarthCatalog Catalog { get; private set; }
        public EarthBuild Build => Clone(draft.build);
        public EarthChecks Checks => Clone(draft.checks);
        public bool Launched => draft.launched;
        public bool TransitCompleted => draft.transitCompleted;
        public bool Busy => activeRun != null;
        public EarthAssessment Assessment => simulator.Assess(Catalog, draft.build, draft.checks);
        public event Action Changed;
        public event Action<EarthMoonHandoff> MoonStageRequested;
        public string Notice { get; private set; }

        private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public void Initialize(IEarthDraftStore draftStore = null)
        {
            if (Catalog != null) return;
            store = draftStore ?? new PlayerPrefsEarthDraftStore();
            var asset = Resources.Load<TextAsset>("FarSignalEarth");
            if (asset == null) throw new InvalidOperationException("FarSignalEarth catalog is missing.");
            Catalog = JsonUtility.FromJson<EarthCatalog>(asset.text);
            try { draft = JsonUtility.FromJson<EarthDraftSave>(store.Load()); }
            catch (ArgumentException) { draft = null; }
            if (draft == null || draft.version != 1 || !ValidBuild(draft.build) || draft.checks?.placements == null)
                draft = new EarthDraftSave();
        }
        private bool ValidBuild(EarthBuild build) => build?.parts?.Length == 6 && build.cargo != null &&
            build.cargo.Distinct().Count() == build.cargo.Count &&
            Enumerable.Range(0, 6).All(i => Catalog.Group(i).Any(p => p.id == build.parts[i])) &&
            build.cargo.All(id => Catalog.cargo.Any(p => p.id == id));
        public void SetSimulator(IEarthSimulationAdapter value)
        { simulator = value ?? throw new ArgumentNullException(nameof(value)); Changed?.Invoke(); }
        public void RegisterMinigames(IEarthMinigameRunner value)
        { if (runner != value && Busy) CancelActivity(); runner = value; Changed?.Invoke(); }
        public void UnregisterMinigames(IEarthMinigameRunner value)
        { if (runner == value) RegisterMinigames(null); }
        public void CancelActivity()
        {
            if (!Busy) return;
            activeRun = null; // Invalidate before Cancel can invoke its completion callback.
            try { runner?.Cancel(); }
            finally { Notice = "Activity cancelled. Your design is preserved."; Changed?.Invoke(); }
        }
        public EarthMoonHandoff GetMoonHandoff()
        {
            if (!TransitCompleted) return null;
            var a = Assessment;
            return new EarthMoonHandoff {
                missionId = Catalog.nextMissionId,
                commanderId = PlayerPrefs.GetString("FarSignal.CommanderId", "commander_male"),
                playerName = PlayerPrefs.GetString("FarSignal.CommanderName", "Alex"),
                switchedOffDeviceId = draft.switchedOffDeviceId, build = Build, checks = Checks,
                fuelUnits = a.fuelUnits, batteryKwh = a.batteryKwh, supplyKw = a.supplyKw,
                launchCostUsd = a.costUsd, launchMassMarginKg = a.MassMarginKg
            };
        }
        public bool CanRun(EarthActivity activity) => !Busy && runner != null && runner.Supports(activity) &&
            (activity == EarthActivity.Transit ? Launched && !TransitCompleted : !Launched);
        public void SelectPart(int group, string id)
        {
            if (Busy || Launched || group < 0 || group > 5 || !Catalog.Group(group).Any(p => p.id == id) || draft.build.parts[group] == id) return;
            draft.build.parts[group] = id;
            if (group == 4) draft.checks.placements.Clear();
            if (group == 5) ClearPower();
            Mutated();
        }
        public void ToggleCargo(string id)
        { SetCargo(id, !draft.build.cargo.Contains(id)); }
        public void SetCargo(string id, bool included)
        {
            if (Busy || Launched || !Catalog.cargo.Any(p => p.id == id)) return;
            if (draft.build.cargo.Contains(id) == included) return;
            if (included) draft.build.cargo.Add(id); else draft.build.cargo.Remove(id);
            draft.checks.placements.Clear();
            if (id == "laser") ClearPower();
            Mutated();
        }
        private void ClearPower()
        { draft.checks.powerTestCompleted = draft.checks.cabinOn = draft.checks.radioOn = draft.checks.laserTested = false; }
        public void LoadReadyBuild(string id)
        {
            if (Busy || Launched) return;
            var preset = EarthReadyBuild.All.FirstOrDefault(p => p.Id == id);
            if (preset == null || preset.Matches(draft.build)) return;
            draft.build = preset.Create(); draft.checks = new EarthChecks();
            Mutated();
        }
        public void LoadPreset(int preset)
        {
            if (Busy || Launched) return;
            draft.build = new EarthBuild(); draft.checks = new EarthChecks();
            if (preset == 1)
            { draft.build.parts = new[] { "l2", "n2", "f2", "e1", "c2", "p2" }; draft.build.cargo.Add("laser"); }
            if (preset == 2)
            { draft.build.parts[2] = "f2"; draft.build.parts[4] = "c2"; draft.build.cargo.Add("fuel"); }
            Mutated();
        }
        private void Mutated()
        { draft.revision++; Notice = "Design saved. Changed systems must pass their checks again."; Save(); }
        private void Save()
        { store.Save(JsonUtility.ToJson(draft)); Changed?.Invoke(); }
        public void BeginActivity(EarthActivity activity)
        {
            if (!CanRun(activity)) { Notice = "This activity is waiting for its minigame module."; Changed?.Invoke(); return; }
            activeRun = Guid.NewGuid().ToString("N"); activeActivity = activity;
            var context = Context(activity); Notice = "Activity in progress."; Changed?.Invoke();
            try { runner.Begin(context, AcceptResult); }
            catch (Exception exception) { activeRun = null; Notice = "Activity could not start. Your design is preserved."; Debug.LogException(exception); Changed?.Invoke(); }
        }
        private EarthMinigameContext Context(EarthActivity activity)
        {
            var totals = Assessment;
            return new EarthMinigameContext {
                runId = activeRun, missionId = Catalog.missionId, activity = activity, buildRevision = draft.revision,
                commanderId = PlayerPrefs.GetString("FarSignal.CommanderId", "commander_male"),
                playerName = PlayerPrefs.GetString("FarSignal.CommanderName", "Alex"),
                build = Build, checks = Checks, fuelUnits = totals.fuelUnits, batteryKwh = totals.batteryKwh, supplyKw = totals.supplyKw
            };
        }
        private void AcceptResult(EarthMinigameResult result)
        {
            if (result == null || activeRun == null || result.runId != activeRun || result.activity != activeActivity || result.buildRevision != draft.revision) return;
            activeRun = null;
            if (!result.completed) { Notice = "Activity cancelled. Your design is preserved."; Changed?.Invoke(); return; }
            var candidate = Checks;
            if (result.activity != EarthActivity.Transit && result.checks == null)
            { Notice = "Activity returned no check data. Retry the activity."; Changed?.Invoke(); return; }
            switch (result.activity)
            {
                case EarthActivity.Cargo: candidate.placements = Clone(result.checks).placements; break;
                case EarthActivity.Power:
                    candidate.powerTestCompleted = result.checks.powerTestCompleted;
                    candidate.cabinOn = result.checks.cabinOn; candidate.radioOn = result.checks.radioOn; candidate.laserTested = result.checks.laserTested;
                    break;
                case EarthActivity.Landing: candidate.padId = result.checks.padId; candidate.arrivalMinute = result.checks.arrivalMinute; break;
                case EarthActivity.Transit:
                    if (result.switchedOffDeviceId != "work-light" && result.switchedOffDeviceId != "cargo-camera")
                    { Notice = "Transit choice is missing. Retry transit."; Changed?.Invoke(); return; }
                    draft.transitCompleted = true; draft.switchedOffDeviceId = result.switchedOffDeviceId;
                    Notice = "Transit complete. Preparing Moon arrival."; Save();
                    GetComponent<GameFlowController>()?.Show(GameScreen.MoonArrival);
                    MoonStageRequested?.Invoke(GetMoonHandoff()); return;
            }
            string checkCode = result.activity == EarthActivity.Cargo ? "cargo-check" : result.activity == EarthActivity.Power ? "power-check" : "landing-check";
            if (simulator.Assess(Catalog, draft.build, candidate).issues.Any(x => x.code == checkCode))
            { Notice = "The returned check is incomplete. Retry the activity."; Changed?.Invoke(); return; }
            draft.checks = candidate; Notice = "Check completed and saved."; Save();
        }
        public bool TryLaunch()
        {
            if (Busy || !Assessment.CanLaunch) return false;
            if (draft.launched) return true;
            draft.launched = true; Notice = "Launch authorized. Manifest saved."; Save(); return true;
        }
        private void OnApplicationPause(bool paused)
        { if (!Busy || runner == null) return; if (paused) runner.Pause(); else runner.Resume(); }
        private void OnDestroy()
        { if (Busy) CancelActivity(); }
    }
}
