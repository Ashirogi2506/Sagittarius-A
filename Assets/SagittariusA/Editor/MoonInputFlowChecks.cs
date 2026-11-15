using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.UI;
using SagittariusA.Minigames;
using SagittariusA.Minigames.MoonPowerRouting;
using SagittariusA.Minigames.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SagittariusA.Gameplay.Editor
{
    // Exercises the real first-person raycast and mouse buttons. The test moves
    // to each objective to make the complete input check fast and repeatable.
    public static class MoonInputFlowChecks
    {
        private const int TargetMask = 1 << 30;
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static bool failed;
        private static Mouse testMouse;
        private static Keyboard testKeyboard;

        [MenuItem("Far Signal/Run Moon Repair Panel Checks")]
        public static void RunPanels()
        {
            if (!Application.isPlaying || GameObject.Find("Moon surface") != null)
                throw new InvalidOperationException("Enter Play mode and return to the menu first.");
            var exp = UnityEngine.Object.FindAnyObjectByType<GameFlowController>().GetComponent<MoonPlayableExperience>();
            if (exp == null) exp = UnityEngine.Object.FindAnyObjectByType<GameFlowController>().gameObject.AddComponent<MoonPlayableExperience>();
            failed = false; testMouse = InputSystem.AddDevice<Mouse>("MoonPanelTestMouse"); testMouse.MakeCurrent();
            exp.StartCoroutine(CheckPanels(exp));
        }

        private static IEnumerator CheckPanels(MoonPlayableExperience exp)
        {
            MoonRepairPanel panel = null;
            try
            {
                foreach (var id in new[] { MoonInteractionIds.OpenWheelCover, MoonInteractionIds.CloseWheelCover, MoonInteractionIds.CleanPatch1,
                    MoonInteractionIds.CleanPatch2, MoonInteractionIds.CleanPatch3, MoonInteractionIds.RetestCart, MoonInteractionIds.IsolateCircuit,
                    MoonInteractionIds.RepairCircuit, MoonInteractionIds.RepairLaser, MoonInteractionIds.InstallPanel, MoonInteractionIds.RestoreEssential,
                    MoonInteractionIds.RestoreRelay, MoonInteractionIds.UploadRoute })
                {
                    int callbacks = 0; bool success = true;
                    panel = MoonRepairPanel.Open(exp.transform, id, ok => { callbacks++; success = ok; });
                    panel.Cancel(); yield return null;
                    Require(callbacks == 1 && !success, id + " cancellation cannot complete a repair");
                    callbacks = 0;
                    panel = MoonRepairPanel.Open(exp.transform, id, ok => { callbacks++; success = ok; });
                    yield return SolveRepair(panel);
                    if (failed) yield break;
                    Require(callbacks == 1 && success, id + " completed exactly once");
                }
                Debug.Log("Moon repair panel checks passed: 13 tasks, cancellation, wrong inputs and mouse-driven completion.");
            }
            finally
            {
                if (panel != null) panel.Cancel();
                if (testMouse != null) { InputSystem.RemoveDevice(testMouse); testMouse = null; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }

        [MenuItem("Far Signal/Run Moon Mouse Interaction Flow")]
        public static void Run()
        {
            if (!Application.isPlaying || GameObject.Find("Moon surface") != null)
                throw new InvalidOperationException("Enter Play mode and return to the menu first.");
            var flow = Resources.FindObjectsOfTypeAll<GameFlowController>()
                .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy);
            if (flow == null || Mouse.current == null || Keyboard.current == null)
                throw new InvalidOperationException("Gameplay scene, mouse and keyboard are required.");
            var exp = flow.GetComponent<MoonPlayableExperience>() ?? flow.gameObject.AddComponent<MoonPlayableExperience>();
            failed = false;
            testMouse = InputSystem.AddDevice<Mouse>("MoonAutomatedMouse");
            testMouse.MakeCurrent();
            testKeyboard = InputSystem.AddDevice<Keyboard>("MoonAutomatedKeyboard"); testKeyboard.MakeCurrent();
            exp.StartCoroutine(Check(exp));
        }

        private static void Require(bool ok, string detail)
        { if (!ok) { failed = true; throw new InvalidOperationException("Moon input check failed: " + detail); } }

        private static IEnumerator Check(MoonPlayableExperience exp)
        {
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 8f;
                exp.StartIsolatedTest();
                var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                gameView.Focus();
                yield return new WaitForSecondsRealtime(.3f);
                var world = GameObject.Find("Moon surface");
                Require(world != null, "surface created");
                var connector = world.GetComponentInChildren<MoonSceneConnector>();
                Cursor.lockState = CursorLockMode.Locked;
                yield return null;
                QueueMouse(true, false);
                yield return null;
                QueueMouse(false, false);
                yield return null;
                var briefing = UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>();
                Require(briefing != null && connector.Context.phase == MoonStagePhase.Descent, "descent explanation waits for acceptance");
                yield return ClickRect((RectTransform)briefing.BackButton.transform);
                Require(connector.Context.phase == MoonStagePhase.Descent && Mathf.Approximately(Time.timeScale, 8f), "Back preserves descent and restores world time");
                yield return new WaitForSecondsRealtime(.25f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.H)); yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); yield return null;
                briefing = UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>();
                Require(briefing != null, "H reopens the current explanation");
                yield return ClickRect((RectTransform)briefing.BeginButton.transform);
                Require(connector.Context.phase == MoonStagePhase.Descent, "Help Return must not execute descent");
                exp.RequestInteraction(MoonInteractionIds.ConfirmDescent);
                yield return AcceptBriefing(MoonInteractionIds.ConfirmDescent);
                Require(connector.Context.phase == MoonStagePhase.Pole7CartRepair, "mouse confirms descent");
                foreach (var id in new[] { MoonInteractionIds.StartCart, MoonInteractionIds.SwitchCartOff, MoonInteractionIds.OpenWheelCover,
                    MoonInteractionIds.CleanPatch1, MoonInteractionIds.CleanPatch2, MoonInteractionIds.CleanPatch3,
                    MoonInteractionIds.CloseWheelCover, MoonInteractionIds.RetestCart, MoonInteractionIds.PickUpPanel,
                    MoonInteractionIds.LoadPanel, MoonInteractionIds.PickUpKit, MoonInteractionIds.LoadKit })
                { yield return Use(exp, connector, id); if (failed) yield break; }
                yield return Use(exp, connector, MoonInteractionIds.DriveBridge);
                if (failed) yield break;
                yield return WaitForDrive(exp, true);
                if (failed) yield break;
                yield return Use(exp, connector, MoonInteractionIds.InspectBridge);
                if (failed) yield break;
                yield return Use(exp, connector, MoonInteractionIds.SurveyRoute);
                if (failed) yield break;
                yield return Use(exp, connector, MoonInteractionIds.DriveBase);
                if (failed) yield break;
                yield return WaitForDrive(exp, false);
                if (failed) yield break;
                foreach (var id in new[] { MoonInteractionIds.DeliverCargo, MoonInteractionIds.IsolateCircuit, MoonInteractionIds.RepairCircuit,
                    MoonInteractionIds.InstallPanel, MoonInteractionIds.RestoreEssential, MoonInteractionIds.RestoreRelay, MoonInteractionIds.UploadRoute,
                    MoonInteractionIds.DriveLander }) { yield return Use(exp, connector, id); if (failed) yield break; }
                yield return WaitForDrive(exp, false);
                if (failed) yield break;
                yield return Use(exp, connector, MoonInteractionIds.ReturnToShip);
                if (failed) yield break;
                yield return null;
                Require(!failed && connector.Context.phase == MoonStagePhase.Complete && GameObject.Find("Moon results") != null, "results displayed with no earlier failures");
                Debug.Log("Moon mouse interaction flow passed: all required actions, ride look and results.");
            }
            finally
            {
                if (exp != null) exp.Exit();
                Time.timeScale = oldScale;
                if (testMouse != null)
                {
                    InputSystem.RemoveDevice(testMouse);
                    testMouse = null;
                }
                if (testKeyboard != null) { InputSystem.RemoveDevice(testKeyboard); testKeyboard = null; }
            }
        }

        private static IEnumerator Use(MoonPlayableExperience exp, MoonSceneConnector connector, string id)
        {
            yield return new WaitForSecondsRealtime(.2f);
            Require(connector.IsAvailable(id), id + " available; actual=" + string.Join(",", connector.Context.availableInteractionIds ?? Array.Empty<string>()));
            var target = connector.transform.root.GetComponentsInChildren<MoonWorldTarget>(true)
                .FirstOrDefault(x => x.AvailableId(connector) == id);
            Require(target != null, id + " has a target");
            var player = connector.transform.root.Find("Moon first-person player");
            var body = player.GetComponent<CharacterController>();
            var camera = player.GetComponentInChildren<Camera>();
            body.enabled = false;
            Vector3 observer = target.transform.position + new Vector3(0, 0, -2.2f);
            var terrain = connector.transform.root.GetComponentsInChildren<MeshRenderer>()
                .FirstOrDefault(x => x.name.EndsWith("_Moon_Terrain"));
            RaycastHit ground;
            if (terrain != null && terrain.GetComponent<Collider>().Raycast(new Ray(observer + Vector3.up * 50f, Vector3.down), out ground, 100f))
                observer.y = ground.point.y + .1f;
            else observer.y = target.transform.position.y - 1.6f;
            player.position = observer;
            var rig = typeof(MoonPlayableExperience).GetField("player", Private).GetValue(exp);
            rig.GetType().GetField("verticalSpeed", Private).SetValue(rig, -.5f);
            body.enabled = true;
            Physics.SyncTransforms();
            Cursor.lockState = CursorLockMode.Locked;
            yield return null;
            Vector3 look = target.transform.position - camera.transform.position;
            Vector3 horizontal = new Vector3(look.x, 0, look.z);
            if (horizontal.sqrMagnitude > .001f) player.rotation = Quaternion.LookRotation(horizontal);
            float pitch = -Mathf.Atan2(look.y, horizontal.magnitude) * Mathf.Rad2Deg;
            rig.GetType().GetField("pitch", Private).SetValue(rig, pitch);
            camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Physics.SyncTransforms();
            RaycastHit hit;
            bool hasHit = Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 3f, TargetMask, QueryTriggerInteraction.Collide);
            var hitTarget = hasHit ? hit.collider.GetComponentInParent<MoonWorldTarget>() : null;
            Require(hasHit && hitTarget?.AvailableId(connector) == id, id + " can be aimed at; target=" + target.transform.position.ToString("F2") +
                ", eye=" + camera.transform.position.ToString("F2") + ", hit=" + (hasHit ? hit.collider.name : "none") +
                ", enabled=" + target.GetComponent<Collider>().enabled);
            bool pickup = id == MoonInteractionIds.PickUpPanel || id == MoonInteractionIds.PickUpKit;
            QueueMouse(!pickup, pickup);
            yield return null;
            QueueMouse(false, false);
            yield return null;
            if (CampaignTaskBriefings.IsMoonMilestone(id))
            {
                Require(connector.IsAvailable(id), "reading " + id + " cannot advance mission state");
                yield return AcceptBriefing(id);
            }
            else Require(UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>() == null, id + " starts without an extra explanation popup");
            if (id == MoonInteractionIds.RestoreEssential)
            {
                yield return SolveSharedGrid(exp, connector);
                if (failed) yield break;
            }
            else if (MoonRepairPanel.Supports(id))
            {
                var panel = UnityEngine.Object.FindAnyObjectByType<MoonRepairPanel>();
                Require(panel != null && panel.ActionId == id, id + " opens its repair task");
                Require(connector.IsAvailable(id), "opening a panel must not advance mission state");
                yield return SolveRepair(panel);
                if (failed) yield break;
            }
            bool travel = id == MoonInteractionIds.DriveBridge || id == MoonInteractionIds.DriveBase || id == MoonInteractionIds.DriveLander;
            Require(travel ? (bool)typeof(MoonPlayableExperience).GetField("driving", Private).GetValue(exp) : !connector.IsAvailable(id), id + " responds to mouse input");
        }

        private static IEnumerator AcceptBriefing(string id)
        {
            var briefing = UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>();
            Require(briefing != null && briefing.Briefing.Id == id, id + " opens the correct explanation");
            Require(Time.timeScale == 0, "reading pauses the world");
            yield return ClickRect((RectTransform)briefing.BeginButton.transform);
            Require(UnityEngine.Object.FindAnyObjectByType<TaskBriefingView>() == null, "Begin closes explanation");
        }

        private static IEnumerator SolveSharedGrid(MoonPlayableExperience exp, MoonSceneConnector connector)
        {
            var manager = MinigameManager.Instance;
            Require(manager.IsActive && manager.ActiveMinigame is MoonPowerRoutingMinigame, "essential line opens supplied Moon grid");
            // A cancelled attempt and an energized empty board must both preserve the repair milestone.
            var shell = UnityEngine.Object.FindAnyObjectByType<MinigameShellView>();
            yield return ClickRect((RectTransform)shell.CloseButton.transform);
            Require(!manager.IsActive && connector.IsAvailable(MoonInteractionIds.RestoreEssential), "cancelled grid grants no progress");
            exp.RequestInteraction(MoonInteractionIds.RestoreEssential);
            yield return null;
            yield return ClickNamedButton("Energize grid");
            Require(manager.ShowingResult && connector.IsAvailable(MoonInteractionIds.RestoreEssential), "failed grid cannot restore essential power");
            yield return ClickNamedButton("Continue");
            Require(connector.IsAvailable(MoonInteractionIds.RestoreEssential) && Mathf.Approximately(Time.timeScale, 8f), "failed result restores world and allows retry");
            exp.RequestInteraction(MoonInteractionIds.RestoreEssential);
            yield return null;
            var game = manager.ActiveMinigame as MoonPowerRoutingMinigame;
            Require(game != null, "grid reopened without repeating accepted briefing");
            var nodes = game.GetComponentsInChildren<RectTransform>();
            foreach (var p in new[] { (0,2),(0,1),(1,1),(2,1),(3,1),(3,0),(0,2),(0,3),(1,3),(2,3),(3,3),(3,4) })
                yield return ClickRect(nodes.First(x => x.name == $"Node{p.Item1}_{p.Item2}"));
            Require(Time.timeScale == 0 && connector.IsAvailable(MoonInteractionIds.RestoreEssential), "laying cables alone does not commit progress");
            yield return ClickNamedButton("Energize grid");
            Require(manager.ShowingResult && connector.IsAvailable(MoonInteractionIds.RestoreEssential), "result waits for Continue");
            yield return ClickNamedButton("Continue");
            Require(!manager.IsActive && !connector.IsAvailable(MoonInteractionIds.RestoreEssential), "two critical lines complete the campaign grid");
            Require(Mathf.Approximately(Time.timeScale, 8f), "shared grid restores saved time scale");
        }

        private static IEnumerator ClickNamedButton(string label)
        {
            var button = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().FirstOrDefault(x =>
                x.GetComponentInChildren<UnityEngine.UI.Text>()?.text == label);
            Require(button != null, "button exists: " + label);
            yield return ClickRect((RectTransform)button.transform);
        }

        private static IEnumerator ClickRect(RectTransform rect)
        {
            yield return new WaitForSecondsRealtime(.1f);
            Canvas.ForceUpdateCanvases();
            var screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = screen });
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = screen });
            yield return null; yield return null;
        }

        private static IEnumerator SolveRepair(MoonRepairPanel panel)
        {
            yield return new WaitForSecondsRealtime(.2f);
            switch (panel.Kind)
            {
                case MoonRepairPanel.TaskKind.Bolts:
                    yield return ClickPanel(panel,new Vector2(.75f,.75f));
                    Require(!panel.Solved, "wrong first bolt does not solve task");
                    foreach(var p in new[]{new Vector2(.25f,.75f),new Vector2(.75f,.25f),new Vector2(.75f,.75f),new Vector2(.25f,.25f)}) yield return ClickPanel(panel,p);
                    break;
                case MoonRepairPanel.TaskKind.Dust:
                    PanelMouse(panel,new Vector2(.27f,.25f),true);yield return new WaitForSecondsRealtime(.2f);
                    Require(!panel.Solved,"holding on one spot cannot clean all dust");
                    for(int i=0;i<20;i++){PanelMouse(panel,new Vector2(.27f+(i%5)*.115f,.25f+(i/5)*.17f),true);yield return null;}
                    QueueMouse(false,false);yield return null;
                    break;
                case MoonRepairPanel.TaskKind.Sequence:
                    yield return ClickPanel(panel,new Vector2(.22f,.5f));
                    Require(!panel.Solved,"out-of-order switch does not complete task");
                    foreach(float x in new[]{.78f,.22f,.5f})yield return ClickPanel(panel,new Vector2(x,.5f));
                    break;
                case MoonRepairPanel.TaskKind.Wires:
                    yield return DragPanel(panel,new Vector2(.18f,.78f),new Vector2(.82f,.78f));
                    Require(!panel.Solved,"mismatched socket is rejected");
                    int[] sockets={2,0,3,1};
                    for(int i=0;i<4;i++)yield return DragPanel(panel,new Vector2(.18f,.78f-.19f*i),new Vector2(.82f,.78f-.19f*sockets[i]));
                    break;
                case MoonRepairPanel.TaskKind.Weld:
                    for(int i=0;i<8;i++){PanelMouse(panel,new Vector2(.15f+i*.1f,.5f+Mathf.Sin(i*1.6f)*.12f),true);yield return null;}
                    QueueMouse(false,false);yield return null;
                    break;
                case MoonRepairPanel.TaskKind.Panel:
                    yield return DragPanel(panel,new Vector2(.25f,.5f),new Vector2(.4f,.2f));
                    Require(!panel.Solved,"misaligned panel is rejected");
                    yield return DragPanel(panel,new Vector2(.25f,.5f),new Vector2(.71f,.5f));
                    for(int i=0;i<4;i++)yield return ClickPanel(panel,new Vector2(.71f+(i%2==0?-.16f:.16f),.5f+(i<2?.27f:-.27f)));
                    break;
                case MoonRepairPanel.TaskKind.Calibration:
                    yield return ClickPanel(panel,new Vector2(.5f,.08f));
                    Require(!panel.Solved,"untuned relay cannot lock");
                    float[] values={.28f,.72f,.46f};
                    for(int i=0;i<3;i++)yield return DragPanel(panel,new Vector2(.35f,.78f-.24f*i),new Vector2(.29f+.6f*values[i],.78f-.24f*i));
                    yield return ClickPanel(panel,new Vector2(.5f,.08f));
                    break;
                case MoonRepairPanel.TaskKind.Diagnostic:
                    for(int i=0;i<3;i++)
                    {
                        float until=Time.realtimeSinceStartup+5;
                        var needle=panel.Board.Find("Needle") as RectTransform;
                        while((needle.anchorMin.x<.55f || needle.anchorMin.x>.62f) && Time.realtimeSinceStartup<until)yield return null;
                        yield return ClickPanel(panel,new Vector2(.5f,.25f));
                    }
                    break;
            }
            Require(panel != null && panel.Solved,"task solved by actual mouse controls: " + panel.ActionId);
            yield return new WaitForSecondsRealtime(.85f);
        }

        private static IEnumerator ClickPanel(MoonRepairPanel panel,Vector2 p)
        { PanelMouse(panel,p,true);yield return null;PanelMouse(panel,p,false);yield return null; }
        private static IEnumerator DragPanel(MoonRepairPanel panel,Vector2 a,Vector2 b)
        {
            PanelMouse(panel,a,true);yield return null;
            for(int i=1;i<=8;i++){PanelMouse(panel,Vector2.Lerp(a,b,i/8f),true);yield return null;}
            PanelMouse(panel,b,false);yield return null;
        }
        private static void PanelMouse(MoonRepairPanel panel,Vector2 p,bool held)
        {
            var rect=panel.Board;
            Vector3 local=new Vector3(rect.rect.xMin+p.x*rect.rect.width,rect.rect.yMin+p.y*rect.rect.height,0);
            Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local));
            var state=new MouseState{position=screen};if(held)state=state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(testMouse,state);
        }

        private static IEnumerator WaitForDrive(MoonPlayableExperience exp, bool checkLook)
        {
            var field = typeof(MoonPlayableExperience).GetField("driving", Private);
            yield return null;
            Require((bool)field.GetValue(exp), "cart started moving");
            if (checkLook)
            {
                // Briefing acceptance suppresses world input for .18 seconds
                // so its click cannot also interact with the world behind it.
                yield return new WaitForSecondsRealtime(.22f);
                var body = GameObject.Find("Moon first-person player").transform;
                var before = body.rotation;
                Cursor.lockState = CursorLockMode.Locked;
                QueueMouse(false, false, new Vector2(100, 0));
                yield return null;
                Require(Quaternion.Angle(before, body.rotation) > 1f, "mouse can look around during the ride");
            }
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((bool)field.GetValue(exp) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!(bool)field.GetValue(exp), "cart trip finished");
        }

        private static void QueueMouse(bool left, bool right, Vector2 delta = default)
        {
            var state = new MouseState { position = testMouse.position.ReadValue(), delta = delta };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(testMouse, state);
        }
    }
}

