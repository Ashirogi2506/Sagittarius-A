using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.Gameplay.Editor
{
    // Editor-only contract tests. Never registers on the player's session or touches PlayerPrefs.
    public static class EarthSessionChecks
    {
        private sealed class MemoryStore : IEarthDraftStore
        {
            public string Json = "";
            public int Writes;
            public string Load() => Json;
            public void Save(string json) { Json = json; Writes++; }
        }
        private sealed class Runner : IEarthMinigameRunner
        {
            public EarthMinigameContext Context;
            public Action<EarthMinigameResult> Callback;
            public bool Supports(EarthActivity activity) => true;
            public void Begin(EarthMinigameContext context, Action<EarthMinigameResult> finished)
            { Context = context; Callback = finished; }
            public void Pause() { }
            public void Resume() { }
            public void Cancel() { }
            public EarthMinigameResult Result() => new EarthMinigameResult {
                runId = Context.runId, buildRevision = Context.buildRevision,
                activity = Context.activity, completed = true,
                checks = new EarthChecks {
                    placements = new List<CargoPlacement> {
                        new CargoPlacement { itemId = "panel", firstSlot = 0, locked = true },
                        new CargoPlacement { itemId = "kit", firstSlot = 2, locked = true }
                    },
                    powerTestCompleted = true, cabinOn = true, radioOn = true,
                    padId = "main", arrivalMinute = 1
                },
                switchedOffDeviceId = "work-light"
            };
        }

        [MenuItem("Far Signal/Run Earth Session Checks")]
        public static void Run()
        {
            var objects = new List<GameObject>();
            int count = 0;
            void Check(bool passed, string message)
            { if (!passed) throw new InvalidOperationException(message); count++; }
            EarthGameplaySession Create(MemoryStore store)
            {
                var go = new GameObject("EarthContractTest") { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(go);
                var s = go.AddComponent<EarthGameplaySession>(); s.Initialize(store); return s;
            }
            try
            {
                var store = new MemoryStore();
                var s = Create(store);
                Check(!s.TryLaunch(), "Unchecked build must not launch.");
                s.LoadReadyBuild("cargo");
                Check(s.Assessment.slotsAvailable == 6 && !s.Assessment.CanLaunch,
                    "Ready model loads instantly but preserves required launch checks.");
                int presetWrites = store.Writes;
                s.LoadReadyBuild("cargo");
                Check(store.Writes == presetWrites, "Selecting the current ready model must not reset its state.");
                Check(EarthReadyBuild.All[1].Matches(Create(store).Build), "Ready model must survive reload.");
                s.LoadPreset(2);
                var restored = Create(store);
                Check(restored.Assessment.MassMarginKg == -340, "Saved build must restore.");
                s.SelectPart(1, "n2");
                Check(s.Assessment.MassMarginKg == 10, "N-2 must correct the build.");
                s.LoadPreset(0);
                EarthAssessment live = null;
                s.Changed += () => live = s.Assessment;
                s.SetCargo("fuel", true);
                Check(live != null && live.costUsd == 96000000 && live.massKg == 10170,
                    "Fitting cargo must publish updated cost and mass immediately.");
                int fittingWrites = store.Writes;
                s.SetCargo("fuel", true);
                Check(store.Writes == fittingWrites && s.Build.cargo.Count(id => id == "fuel") == 1,
                    "Repeated fitting must not remove or duplicate cargo.");
                s.SetCargo("fuel", false);
                Check(live.costUsd == 93000000 && live.massKg == 9570, "Removing cargo must restore live totals.");
                var runner = new Runner(); s.RegisterMinigames(runner);
                s.BeginActivity(EarthActivity.Cargo);
                var cancelled = runner.Result();
                s.SelectPart(0, "l3");
                Check(s.Build.parts[0] == "l1", "Build must remain fixed during an activity.");
                s.CancelActivity();
                runner.Callback(cancelled);
                Check(!s.Busy && s.Checks.placements.Count == 0, "Cancelled callback cannot grant cargo.");
                s.BeginActivity(EarthActivity.Cargo);
                runner.Callback(cancelled);
                Check(s.Busy, "Old callback must not close a newer run.");
                var cargo = runner.Result(); runner.Callback(cargo);
                Check(s.Checks.placements.Count == 2, "Valid cargo must save.");
                int writes = store.Writes; runner.Callback(cargo);
                Check(store.Writes == writes, "Duplicate cargo callback must not save twice.");
                s.SelectPart(4, "c2");
                Check(s.Checks.placements.Count == 0, "Bay changes must clear cargo checks.");
                s.LoadPreset(0);
                foreach (var activity in new[] { EarthActivity.Cargo, EarthActivity.Power, EarthActivity.Landing })
                { s.BeginActivity(activity); runner.Callback(runner.Result()); }
                Check(s.Assessment.CanLaunch, "Three valid checks must allow launch.");
                s.SelectPart(5, "p2");
                Check(!s.Checks.powerTestCompleted && !s.Assessment.CanLaunch, "Power change must invalidate its test.");
                s.BeginActivity(EarthActivity.Power); runner.Callback(runner.Result());
                Check(s.TryLaunch(), "Rechecked build must launch.");
                writes = store.Writes;
                Check(s.TryLaunch() && store.Writes == writes, "Launch must commit once.");
                s.SelectPart(0, "l3");
                Check(s.Build.parts[0] == "l1", "Launched build must be immutable.");
                int handoffs = 0; EarthMoonHandoff handoff = null;
                s.MoonStageRequested += value => { handoffs++; handoff = value; };
                s.BeginActivity(EarthActivity.Transit);
                var invalid = runner.Result(); invalid.switchedOffDeviceId = "radio"; runner.Callback(invalid);
                Check(!s.TransitCompleted && handoffs == 0, "Turning off essential radio cannot complete transit.");
                s.BeginActivity(EarthActivity.Transit);
                var transit = runner.Result(); runner.Callback(transit); runner.Callback(transit);
                Check(handoffs == 1 && handoff.missionId == "pole-7" && handoff.checks.padId == "main",
                    "Transit must hand off once with saved plan.");
                var resumed = Create(store);
                Check(resumed.GetMoonHandoff()?.switchedOffDeviceId == "work-light" && !resumed.CanRun(EarthActivity.Transit),
                    "Completed transit must survive reload.");
                Debug.Log($"EARTH SESSION CHECKS: {count} passed. Player save unchanged.");
            }
            catch (Exception error) { Debug.LogError("EARTH SESSION CHECKS FAILED: " + error); }
            finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
