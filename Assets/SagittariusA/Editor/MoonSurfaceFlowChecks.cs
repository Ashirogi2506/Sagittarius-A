using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.UI;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.Gameplay.Editor
{
    // Runs only in the editor and only with the isolated in-memory test handoff.
    public static class MoonSurfaceFlowChecks
    {
        private static bool failed;
        [MenuItem("Far Signal/Inspect Moon Positions")]
        public static void InspectPositions()
        {
            var world = GameObject.Find("Moon surface");
            if (world == null) throw new InvalidOperationException("Open the Moon surface first.");
            var player = world.transform.Find("Moon first-person player");
            var cart = world.transform.Find("Mobile cargo cart");
            var target = world.GetComponentsInChildren<MoonWorldTarget>(true).FirstOrDefault(x => x.name == "Cart switch");
            var camera = world.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            Debug.Log("Moon positions: player=" + (player == null ? "missing" : player.position.ToString("F2")) +
                ", camera=" + (camera == null ? "missing" : camera.transform.position.ToString("F2")) +
                ", cart=" + (cart == null ? "missing" : cart.position.ToString("F2")) +
                ", switch=" + (target == null ? "missing" : target.transform.position.ToString("F2")));
        }

        [MenuItem("Far Signal/Run Moon Surface Flow Checks")]
        public static void Run()
        { StartCheck("main", 20f); }

        [MenuItem("Far Signal/Run Moon Ridge Flow Checks")]
        public static void RunRidge()
        { StartCheck("ridge", 20f); }

        [MenuItem("Far Signal/Run Visible Moon Drive Check")]
        public static void RunVisible()
        { StartCheck("main", 1f); }

        private static void StartCheck(string padId, float timeScale)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first, then run the Moon surface flow checks.");
            if (GameObject.Find("Moon surface") != null) throw new InvalidOperationException("Return from the current Moon surface before running the isolated check.");
            var flow = Resources.FindObjectsOfTypeAll<GameFlowController>()
                .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy);
            if (flow == null) throw new InvalidOperationException("Gameplay UI controller was not found.");
            var experience = flow.GetComponent<MoonPlayableExperience>() ?? flow.gameObject.AddComponent<MoonPlayableExperience>();
            failed = false;
            experience.StartCoroutine(CheckFlow(experience, padId, timeScale));
        }

        private static void Require(bool condition, string name)
        { if (!condition) { failed = true; throw new InvalidOperationException("Moon surface flow check failed: " + name); } }

        private static void Send(MoonSceneConnector connector, string id)
        {
            Require(connector.IsAvailable(id), id + " is available");
            if (id != MoonInteractionIds.ConfirmDescent)
            {
                var target = connector.transform.root.GetComponentsInChildren<MoonWorldTarget>(true)
                    .FirstOrDefault(x => x.AvailableId(connector) == id);
                Require(target != null && target.GetComponent<Collider>() != null, id + " has a world interaction target");
            }
            connector.Interact(id);
        }

        private static IEnumerator CheckFlow(MoonPlayableExperience experience, string padId, float timeScale)
        {
            float oldTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = timeScale;
                experience.StartIsolatedTestForPad(padId);
                yield return null;
                var world = GameObject.Find("Moon surface");
                Require(world != null, "surface created");
                var connector = world.GetComponentInChildren<MoonSceneConnector>();
                Require(connector != null && connector.Context != null, "surface connected to isolated state");
                var cart = world.transform.Find("Mobile cargo cart");
                Require(cart != null && cart.GetComponentsInChildren<MeshCollider>().Length > 0, "movable cart retains collisions");
                MoonStageChecks.RunSurfaceCollisionChecks();
                var initial = cart.position;
                Send(connector, MoonInteractionIds.ConfirmDescent);
                Send(connector, MoonInteractionIds.StartCart);
                Send(connector, MoonInteractionIds.SwitchCartOff);
                Send(connector, MoonInteractionIds.OpenWheelCover);
                foreach (var id in new[] { MoonInteractionIds.CleanPatch1, MoonInteractionIds.CleanPatch2, MoonInteractionIds.CleanPatch3 })
                    connector.HoldPatch(id, 1f);
                Send(connector, MoonInteractionIds.CloseWheelCover);
                Send(connector, MoonInteractionIds.RetestCart);
                Send(connector, MoonInteractionIds.PickUpPanel); Send(connector, MoonInteractionIds.LoadPanel);
                Send(connector, MoonInteractionIds.PickUpKit); Send(connector, MoonInteractionIds.LoadKit);
                experience.RequestInteraction(MoonInteractionIds.DriveBridge);
                AcceptTravelBriefing();
                Require(connector.Context.routeStep == 0, "travel is not committed before arrival");
                yield return null;
                yield return WaitForTrip(experience);
                Require(!failed, "bridge trip completed without errors");
                Require(Vector3.Distance(initial, cart.position) > 10, "cart moved to bridge");
                Send(connector, MoonInteractionIds.InspectBridge);
                Send(connector, MoonInteractionIds.InspectSteep);
                Send(connector, MoonInteractionIds.SurveyRoute);
                experience.RequestInteraction(MoonInteractionIds.DriveBase);
                Require(UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>() == null, "repeat cart travel needs no extra explanation");
                yield return null;
                yield return WaitForTrip(experience);
                Require(!failed, "Pole-7 trip completed without errors");
                Send(connector, MoonInteractionIds.DeliverCargo);
                foreach (var id in new[] { MoonInteractionIds.IsolateCircuit, MoonInteractionIds.RepairCircuit, MoonInteractionIds.InstallPanel,
                    MoonInteractionIds.RestoreEssential, MoonInteractionIds.RestoreRelay, MoonInteractionIds.UploadRoute }) Send(connector, id);
                experience.RequestInteraction(MoonInteractionIds.DriveLander);
                Require(UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>() == null, "return travel needs no extra explanation");
                yield return null;
                yield return WaitForTrip(experience);
                Require(!failed, "return trip completed without errors");
                Require(Vector3.Distance(initial, cart.position) < 1, "cart returned to landing area");
                Send(connector, MoonInteractionIds.ReturnToShip);
                yield return null;
                Require(connector.Context.phase == MoonStagePhase.Complete && GameObject.Find("Moon results") != null, "Moon results shown");
                experience.Exit();
                yield return null;
                experience.StartIsolatedTestForPad(padId);
                yield return null;
                var restartedWorld = GameObject.Find("Moon surface");
                Require(restartedWorld != null, "second surface starts");
                var restartedPlayer = restartedWorld.transform.Find("Moon first-person player");
                var restartedCart = restartedWorld.transform.Find("Mobile cargo cart");
                Require(restartedPlayer != null && restartedCart != null && Vector3.Distance(restartedPlayer.position, restartedCart.position) < 6,
                    "second surface starts near the cart");
                Debug.Log("Moon " + padId + " surface flow checks passed: cart trips, cargo, repair, return and results.");
            }
            finally
            {
                if (experience != null) experience.Exit();
                Time.timeScale = oldTimeScale;
            }
        }

        private static IEnumerator WaitForTrip(MoonPlayableExperience experience)
        {
            var field = typeof(MoonPlayableExperience).GetField("driving", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null && (bool)field.GetValue(experience), "cart trip started");
            var world = GameObject.Find("Moon surface");
            var cart = world.transform.Find("Mobile cargo cart");
            var obstacles = world.GetComponentsInChildren<MeshRenderer>()
                .Where(x => x.name.EndsWith("_SurveyLedge") || x.name.EndsWith("_Bridge_Broken") || x.name.EndsWith("_Pole7_Exterior")).ToArray();
            var fence = world.GetComponentsInChildren<MeshRenderer>()
                .FirstOrDefault(x => x.name.Contains("Landing pad safety rails"))?.GetComponent<Collider>();
            Vector3 previous = cart.position;
            Quaternion initialRotation = cart.rotation;
            bool turned = false;
            float worstSlip = 0;
            var motor = (MoonCartMotor)typeof(MoonPlayableExperience).GetField("cartMotor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(experience);
            Vector3 stepPosition = cart.position;
            void MeasureStep()
            {
                Vector3 delta = cart.position - stepPosition;
                delta.y = 0;
                if (delta.sqrMagnitude > .00000001f)
                    worstSlip = Mathf.Max(worstSlip, Vector3.Angle(delta, new Vector3(cart.forward.x, 0, cart.forward.z)));
                stepPosition = cart.position;
            }
            motor.SubstepCompleted += MeasureStep;
            float deadline = Time.realtimeSinceStartup + 30;
            try
            {
            while ((bool)field.GetValue(experience) && Time.realtimeSinceStartup < deadline)
            {
                Require(obstacles.All(x => !x.bounds.Contains(cart.position)), "cart avoids major solid structures");
                Physics.SyncTransforms();
                var blocked = Physics.OverlapBox(cart.position + cart.up * .25f, new Vector3(1.15f, .65f, 1.65f), cart.rotation,
                    ~(1 << 30), QueryTriggerInteraction.Ignore).FirstOrDefault(c => !c.transform.IsChildOf(cart) &&
                    (c.name.Contains("_Rock_") || c.name.Contains("safety rails") || c.name.Contains("_SurveyLedge") ||
                     c.name.Contains("_Bridge_Broken") || c.name.Contains("_Pole7_Exterior")));
                Require(blocked == null, "cart body clears route geometry at " + cart.position + "; obstacle=" + (blocked == null ? "none" : blocked.name));
                Vector3 movement = cart.position - previous;
                if (Quaternion.Angle(initialRotation, cart.rotation) > 10) turned = true;
                if (fence != null && movement.sqrMagnitude > .0001f)
                {
                    RaycastHit hit;
                    Require(!fence.Raycast(new Ray(previous + Vector3.up * .5f, movement.normalized), out hit, movement.magnitude),
                        "cart route avoids landing-pad fences");
                }
                previous = cart.position;
                yield return null;
            }
            }
            finally { motor.SubstepCompleted -= MeasureStep; }
            Require(!(bool)field.GetValue(experience), "cart trip finished");
            Require(turned, "cart changes heading along the route");
            // Compare each movement with its simultaneous chassis orientation,
            // including rounded bends and stops followed by in-place turning.
            Require(worstSlip < 5, "cart faces travel instead of sliding sideways; worst=" + worstSlip);
            var player = world.transform.Find("Moon first-person player");
            Require(player.GetComponent<CharacterController>().enabled && Vector3.Distance(player.position, cart.position) > 1.5f,
                "passenger dismounts beside the cart with movement enabled");
        }

        private static void AcceptTravelBriefing()
        {
            var briefing = UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>();
            Require(briefing != null && Time.timeScale == 0, "travel explains the next destination before departure");
            briefing.BeginButton.onClick.Invoke();
        }
    }
}
