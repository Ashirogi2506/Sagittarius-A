using System;
using System.Linq;
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
            Check(!driver.Context.cart.panelLoaded, "cargo cannot be loaded before pickup");
            driver.Send(MoonInteractionIds.PickUpPanel);
            Check(driver.Context.carriedCargoId == "panel", "panel pickup is recorded");
            var carryGo = new GameObject("MoonStageCarryRestoreChecks");
            var carried = carryGo.AddComponent<MoonStageController>();
            carried.Initialize(Handoff(), store);
            Check(Array.IndexOf(carried.AvailableInteractions, MoonInteractionIds.LoadPanel) >= 0, "carried item survives save and restore");
            driver.Send(MoonInteractionIds.LoadPanel);
            driver.Send(MoonInteractionIds.PickUpKit);
            driver.Send(MoonInteractionIds.LoadKit);
            Check(controller.Phase == MoonStagePhase.RouteSurvey && completed == 0, "loaded cart starts route survey without completing Moon");
            driver.Send(MoonInteractionIds.LoadKit);
            driver.Send(MoonInteractionIds.UploadRoute);
            Check(completed == 0 && controller.RemainingFuelUnits == 5, "duplicate and out-of-order callbacks cannot complete Moon or spend fuel");

            driver.Send(MoonInteractionIds.InspectBridge);
            Check(driver.Context.routeStep == 0, "bridge cannot be inspected before cart trip");
            driver.Send(MoonInteractionIds.DriveBridge);
            driver.Send(MoonInteractionIds.InspectBridge);
            driver.Send(MoonInteractionIds.SurveyRoute);
            driver.Send(MoonInteractionIds.InspectBeacon);
            Check(driver.Context.beaconMarked && !driver.Context.batteryLeft, "beacon can be marked without leaving a battery");
            var checkpointGo = new GameObject("MoonStageRouteRestoreChecks");
            var checkpoint = checkpointGo.AddComponent<MoonStageController>();
            checkpoint.Initialize(Handoff(), store);
            Check(checkpoint.Phase == MoonStagePhase.RouteSurvey && checkpoint.AvailableInteractions.Contains(MoonInteractionIds.DriveBase),
                "route checkpoint restores the next cart trip");

            var batteryHandoff = Handoff();
            batteryHandoff.build.cargo.Add("battery");
            var batteryDraft = JsonUtility.FromJson<MoonDraftSave>(store.Json);
            batteryDraft.handoff = batteryHandoff;
            batteryDraft.handoffJson = JsonUtility.ToJson(batteryHandoff);
            var batteryStore = new MemoryStore { Json = JsonUtility.ToJson(batteryDraft) };
            var batteryGo = new GameObject("MoonStageBeaconBatteryChecks");
            var batteryController = batteryGo.AddComponent<MoonStageController>();
            var batteryDriver = new Driver();
            batteryController.Initialize(batteryHandoff, batteryStore);
            batteryController.RegisterDriver(batteryDriver);
            batteryDriver.Send(MoonInteractionIds.LeaveBattery);
            Check(batteryDriver.Context.batteryLeft, "optional beacon consumes the carried battery");
            var batteryCompleted = JsonUtility.FromJson<MoonDraftSave>(batteryStore.Json);
            batteryCompleted.phase = MoonStagePhase.Complete; batteryCompleted.repairStep = 6; batteryCompleted.routeStep = 5;
            batteryStore.Json = JsonUtility.ToJson(batteryCompleted);
            batteryController.Initialize(batteryHandoff, batteryStore);
            Check(batteryController.GetMarsHandoff() != null && !batteryController.GetMarsHandoff().build.cargo.Contains("battery"),
                "battery left at beacon does not travel to Mars");
            driver.Send(MoonInteractionIds.DriveBase);
            driver.Send(MoonInteractionIds.DeliverCargo);
            driver.Send(MoonInteractionIds.IsolateCircuit);
            driver.Send(MoonInteractionIds.RepairCircuit);
            driver.Send(MoonInteractionIds.InstallPanel);
            driver.Send(MoonInteractionIds.RestoreEssential);
            driver.Send(MoonInteractionIds.RestoreRelay);
            driver.Send(MoonInteractionIds.UploadRoute);
            Check(controller.Phase == MoonStagePhase.ReturnToLander && completed == 0, "Mars file requires return to lander before completion");
            Check(controller.GetMarsHandoff() == null, "Mars handoff is unavailable before return");
            driver.Send(MoonInteractionIds.ReturnToShip);
            Check(controller.Phase == MoonStagePhase.ReturnToLander, "ship cannot be reached before the return cart trip");
            driver.Send(MoonInteractionIds.DriveLander);
            driver.Send(MoonInteractionIds.ReturnToShip);
            driver.Send(MoonInteractionIds.ReturnToShip);
            Check(controller.Phase == MoonStagePhase.Complete && completed == 1, "return to lander completes Moon once");

            var restoredGo = new GameObject("MoonStageRestoreChecks");
            var restored = restoredGo.AddComponent<MoonStageController>();
            restored.Initialize(Handoff(), store);
            Check(restored.Phase == MoonStagePhase.Complete && restored.RemainingFuelUnits == 5, "saved Moon state restores");
            var mars = restored.GetMarsHandoff();
            Check(mars != null && mars.nextMissionId == "mars-staging" && mars.commanderId == "commander_male" && mars.remainingFuelUnits == 5 &&
                !mars.build.cargo.Contains("panel") && mars.build.cargo.Contains("kit") && mars.beaconMarked,
                "restored completion provides Mars handoff with commander and remaining fuel");

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
            UnityEngine.Object.DestroyImmediate(carryGo);
            UnityEngine.Object.DestroyImmediate(checkpointGo);
            UnityEngine.Object.DestroyImmediate(batteryGo);
            UnityEngine.Object.DestroyImmediate(failureGo);
            Debug.Log(count + " Moon stage checks passed.");
        }

        [MenuItem("Far Signal/Run Moon Surface Collision Checks")]
        public static void RunSurfaceCollisionChecks()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start MOON SURFACE TEST in Play mode first.");
            var world = GameObject.Find("Moon surface");
            if (world == null) throw new InvalidOperationException("Start MOON SURFACE TEST first.");
            var renderers = world.GetComponentsInChildren<MeshRenderer>(true);
            foreach (string suffix in new[] { "_Moon_Terrain", "_Cart_Chassis", "_Lander_Body", "_Pole7_Exterior" })
            {
                var mesh = renderers.FirstOrDefault(x => x.name.EndsWith(suffix, StringComparison.Ordinal));
                Check(mesh != null && mesh.GetComponent<MeshCollider>() != null && mesh.GetComponent<MeshCollider>().enabled,
                    suffix + " has active collision");
            }
            var cart = renderers.First(x => x.name.EndsWith("_Cart_Chassis", StringComparison.Ordinal));
            var collider = cart.GetComponent<MeshCollider>();
            var bounds = cart.bounds;
            bool hitWall = false; RaycastHit hit = default;
            for (int x = -2; x <= 2 && !hitWall; x++)
                for (int y = 1; y <= 4 && !hitWall; y++)
                {
                    var origin = new Vector3(bounds.center.x + bounds.extents.x * x / 3f,
                        Mathf.Lerp(bounds.min.y, bounds.max.y, y / 5f), bounds.min.z - 2f);
                    hitWall = collider.Raycast(new Ray(origin, Vector3.forward), out hit, bounds.size.z + 4f)
                        && Mathf.Abs(hit.normal.z) > .2f;
                }
            Check(hitWall, "cart collider blocks a horizontal ray");
            var probe = new GameObject("Temporary collision check");
            try
            {
                var body = probe.AddComponent<CharacterController>();
                body.height = 1f; body.radius = .2f; body.center = new Vector3(0, .5f, 0);
                probe.transform.position = new Vector3(hit.point.x, hit.point.y - .5f, hit.point.z - .8f);
                Physics.SyncTransforms();
                Vector3 before = probe.transform.position;
                body.Move(Vector3.forward * 1.6f);
                Check(probe.transform.position.z - before.z < 1.4f, "character cannot pass through cart");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            Debug.Log("6 Moon surface collision checks passed.");
        }
    }
}
