using System;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.Gameplay.Editor
{
    public static class MoonStageChecks
    {
        private sealed class MemoryStore : IMoonDraftStore
        {
            public string Json = "";
            public string Load() { return Json; }
            public void Save(string json) { Json = json; }
        }

        private sealed class Driver : IMoonInteractionDriver
        {
            public MoonStageContext Context;
            public Action<MoonInteractionResult> Report;
            public int Initializes, Updates, Shutdowns;
            public void Initialize(MoonStageContext context, Action<MoonInteractionResult> report)
            { Context = context; Report = report; Initializes++; }
            public void UpdateContext(MoonStageContext context) { Context = context; Updates++; }
            public void Pause() { }
            public void Resume() { }
            public void Shutdown() { Shutdowns++; }
            public void Send(string id, float elapsed = 0)
            { Report(new MoonInteractionResult { runId = Context.runId, interactionId = id, elapsedSeconds = elapsed, completed = true }); }
        }

        private static int count;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Moon check failed: " + name);
            count++;
        }

        private static EarthMoonHandoff Handoff(int fuel = 15)
        {
            return new EarthMoonHandoff {
                missionId = "pole-7", commanderId = "commander_male", playerName = "Alex",
                build = new EarthBuild(),
                checks = new EarthChecks { powerTestCompleted = true, cabinOn = true, radioOn = true, padId = "main", arrivalMinute = 2 },
                fuelUnits = fuel, batteryKwh = 6, supplyKw = 5, launchCostUsd = 93000000, launchMassMarginKg = 530
            };
        }

        [MenuItem("Far Signal/Run Moon Stage Checks")]
        public static void Run()
        {
            count = 0;
            var store = new MemoryStore();
            var go = new GameObject("MoonStageChecks");
            var controller = go.AddComponent<MoonStageController>();
            var driver = new Driver();
            int completed = 0;
            controller.MoonStageCompleted += () => completed++;
            controller.Initialize(Handoff(), store);
            controller.RegisterDriver(driver);

            Check(controller.Phase == MoonStagePhase.Descent, "new run starts at descent");
            Check(driver.Context.availableInteractionIds.Length == 1 && driver.Context.availableInteractionIds[0] == MoonInteractionIds.ConfirmDescent, "only descent is initially available");
            driver.Send(MoonInteractionIds.OpenWheelCover);
            Check(controller.Phase == MoonStagePhase.Descent, "out-of-order callback is ignored");
            driver.Send(MoonInteractionIds.ConfirmDescent);
            Check(controller.Phase == MoonStagePhase.Pole7CartRepair && controller.RemainingFuelUnits == 5, "descent commits ten fuel once");

            driver.Send(MoonInteractionIds.StartCart);
            Check(Array.IndexOf(driver.Context.availableInteractionIds, MoonInteractionIds.SwitchCartOff) >= 0, "jam test requires power off");
            driver.Send(MoonInteractionIds.SwitchCartOff);
            driver.Send(MoonInteractionIds.OpenWheelCover);
            Check(driver.Context.availableInteractionIds.Length == 3, "three dust patches become available");
            driver.Send(MoonInteractionIds.CleanPatch1, .5f);
            driver.Send(MoonInteractionIds.CleanPatch1, .5f);
            driver.Send(MoonInteractionIds.CleanPatch2, 1f);
            driver.Send(MoonInteractionIds.CleanPatch3, 1f);
            Check(Array.IndexOf(driver.Context.availableInteractionIds, MoonInteractionIds.CloseWheelCover) >= 0, "clean wheel can be closed");
            driver.Send(MoonInteractionIds.CloseWheelCover);
            driver.Send(MoonInteractionIds.RetestCart);
            Check(driver.Context.cart.testPassed, "repaired cart passes retest");
            driver.Send(MoonInteractionIds.LoadPanel);
            driver.Send(MoonInteractionIds.LoadKit);
            Check(controller.Phase == MoonStagePhase.Complete && completed == 1, "required cargo completes Moon slice once");
            driver.Send(MoonInteractionIds.LoadKit);
            Check(completed == 1 && controller.RemainingFuelUnits == 5, "duplicate callback cannot repeat completion or fuel spending");

            var restoredGo = new GameObject("MoonStageRestoreChecks");
            var restored = restoredGo.AddComponent<MoonStageController>();
            restored.Initialize(Handoff(), store);
            Check(restored.Phase == MoonStagePhase.Complete && restored.RemainingFuelUnits == 5, "saved Moon state restores");

            var failureGo = new GameObject("MoonStageFuelFailureChecks");
            var failure = failureGo.AddComponent<MoonStageController>();
            var failureDriver = new Driver();
            int restarts = 0;
            failure.RestartAtPreLaunchRequested += () => restarts++;
            failure.Initialize(Handoff(5), new MemoryStore());
            failure.RegisterDriver(failureDriver);
            failureDriver.Send(MoonInteractionIds.ConfirmDescent);
            Check(failure.Phase == MoonStagePhase.RestartAtPreLaunch && restarts == 1, "insufficient fuel requests pre-launch restart once");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(restoredGo);
            UnityEngine.Object.DestroyImmediate(failureGo);
            Debug.Log(count + " Moon stage checks passed.");
        }
    }
}
