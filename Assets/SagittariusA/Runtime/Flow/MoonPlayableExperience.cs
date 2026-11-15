using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.Flow
{
    // Builds the first playable surface around the imported Beacon-pad cart.
    // The world is temporary in memory; gameplay and the environment remain reusable.
    public sealed class MoonPlayableExperience : MonoBehaviour
    {
        private const int TargetLayer = 30;
        private GameObject world, testOwner, hiddenCanvas;
        private MoonSceneConnector connector;
        private MoonFirstPersonController player;
        private Text objective, prompt, notice;
        private Renderer cover;
        private readonly Renderer[] dust = new Renderer[3];
        private GameObject[] patchTargets;
        private readonly List<MoonWorldTarget> targets = new List<MoonWorldTarget>();
        private Material markerMaterial;
        private bool testMode;
        private sealed class MemoryStore : IMoonDraftStore
        {
            private string json = "";
            public string Load() { return json; }
            public void Save(string value) { json = value; }
        }

        public void StartCampaign()
        {
            var stage = GetComponent<MoonStageController>();
            if (stage == null || !stage.Initialized) { Debug.LogWarning("Moon handoff is not ready."); return; }
            var handoff = GetComponent<EarthGameplaySession>()?.GetMoonHandoff();
            if (handoff?.checks?.padId != "main")
            { Debug.LogWarning("Ridge landing is not yet connected to the Moon surface."); return; }
            Begin(stage, false);
        }

        public void StartIsolatedTest()
        {
            if (world != null) return;
            testOwner = new GameObject("Moon isolated test state");
            var stage = testOwner.AddComponent<MoonStageController>();
            stage.Initialize(new EarthMoonHandoff {
                missionId = "pole-7", commanderId = "commander_male", playerName = "Test Commander",
                build = new EarthBuild(), checks = new EarthChecks { padId = "main", arrivalMinute = 2,
                    powerTestCompleted = true, cabinOn = true, radioOn = true },
                fuelUnits = 15, batteryKwh = 6, supplyKw = 5
            }, new MemoryStore());
            Begin(stage, true);
        }

        private void Begin(MoonStageController stage, bool isolated)
        {
            if (world != null) return;
            var model = Resources.Load<GameObject>("MoonEnvironment");
            if (model == null) { Debug.LogError("MoonEnvironment FBX is missing; wait for Unity to import it."); if (testOwner != null) Destroy(testOwner); return; }
            world = new GameObject("Moon surface");
            testMode = isolated;
            var environment = Instantiate(model, world.transform);
            var terrain = FindMesh(environment, "Moon_Terrain");
            var chassis = FindMesh(environment, "Cart_Chassis");
            cover = FindMesh(environment, "Cart_WheelCover_FR");
            for (int i = 0; i < 3; i++) dust[i] = FindMesh(environment, "Cart_WheelDust_Stage" + (i + 1));
            if (terrain == null || chassis == null || cover == null || dust.Any(x => x == null))
            {
                Debug.LogError("Moon environment is missing required cart or terrain meshes.");
                Destroy(world); world = null; if (testOwner != null) Destroy(testOwner); return;
            }
            var meshFilter = terrain.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            { Debug.LogError("Moon terrain has no mesh for collision."); Destroy(world); world = null; if (testOwner != null) Destroy(testOwner); return; }
            terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
            Physics.SyncTransforms();
            var connectorObject = new GameObject("Moon scene connector"); connectorObject.transform.SetParent(world.transform);
            connector = connectorObject.AddComponent<MoonSceneConnector>(); connector.Bind(stage);
            var markerShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (markerShader != null) markerMaterial = new Material(markerShader) { color = new Color(1f, .5f, .05f) };
            var lamp = new GameObject("Moon sunlight", typeof(Light)); lamp.transform.SetParent(world.transform);
            lamp.transform.rotation = Quaternion.Euler(40, -40, 0); lamp.GetComponent<Light>().type = LightType.Directional;
            lamp.GetComponent<Light>().intensity = 1.4f;
            var playerObject = new GameObject("Moon first-person player", typeof(CharacterController));
            playerObject.transform.SetParent(world.transform);
            var body = playerObject.GetComponent<CharacterController>(); body.height = 1.8f; body.center = new Vector3(0, .9f, 0);
            Vector3 spawn = chassis.bounds.center + new Vector3(0, 5, -3);
            if (Physics.Raycast(spawn, Vector3.down, out var ground, 100f, ~0, QueryTriggerInteraction.Ignore)) spawn.y = ground.point.y + .1f;
            else spawn.y = chassis.bounds.min.y + .1f;
            playerObject.transform.position = spawn;
            Vector3 direction = chassis.bounds.center - spawn; direction.y = 0;
            if (direction.sqrMagnitude > .001f) playerObject.transform.rotation = Quaternion.LookRotation(direction);
            var cameraObject = new GameObject("Moon player camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(playerObject.transform, false); cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<Camera>().backgroundColor = new Color(.012f, .018f, .032f);
            player = playerObject.AddComponent<MoonFirstPersonController>();
            player.Configure(cameraObject.GetComponent<Camera>(), connector, 1 << TargetLayer);
            CreateTarget("Cart switch", chassis.bounds.center + new Vector3(0, .3f, -.65f), new Vector3(1.2f, .65f, .35f), false,
                MoonInteractionIds.StartCart, MoonInteractionIds.SwitchCartOff, MoonInteractionIds.RetestCart);
            CreateTarget("Wheel cover", cover.bounds.center, Vector3.Max(cover.bounds.size, new Vector3(.4f, .5f, .5f)), false,
                MoonInteractionIds.OpenWheelCover, MoonInteractionIds.CloseWheelCover);
            patchTargets = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = cover.bounds.center + new Vector3(-.25f, (i - 1) * .18f, -.15f);
                patchTargets[i] = CreateTarget("Dust patch " + (i + 1), pos, new Vector3(.25f, .15f, .25f), true,
                    i == 0 ? MoonInteractionIds.CleanPatch1 : i == 1 ? MoonInteractionIds.CleanPatch2 : MoonInteractionIds.CleanPatch3);
            }
            CreateTarget("Load P-4 panel", chassis.bounds.center + new Vector3(-.3f, .65f, 0), new Vector3(.5f, .2f, .5f), false, MoonInteractionIds.LoadPanel);
            CreateTarget("Load repair kit", chassis.bounds.center + new Vector3(.3f, .65f, 0), new Vector3(.5f, .2f, .5f), false, MoonInteractionIds.LoadKit);
            CreateHud();
            var canvas = GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.gameObject != world && !c.transform.IsChildOf(world.transform));
            if (canvas != null) { hiddenCanvas = canvas.gameObject; hiddenCanvas.SetActive(false); }
        }

        private static Renderer FindMesh(GameObject root, string name)
        { return root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name.EndsWith("_" + name, StringComparison.Ordinal)); }

        private GameObject CreateTarget(string description, Vector3 position, Vector3 size, bool hold, params string[] ids)
        {
            var target = new GameObject(description); target.transform.SetParent(world.transform); target.transform.position = position;
            target.layer = TargetLayer; var collider = target.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = size;
            var binding = target.AddComponent<MoonWorldTarget>(); binding.Configure(description, hold, ids); targets.Add(binding);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Interaction marker"; marker.transform.SetParent(target.transform, false);
            marker.transform.localScale = Vector3.one * .12f;
            var markerCollider = marker.GetComponent<Collider>(); markerCollider.enabled = false; Destroy(markerCollider);
            if (markerMaterial != null) marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            return target;
        }

        private void CreateHud()
        {
            var hud = new GameObject("Moon HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); hud.transform.SetParent(world.transform);
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            objective = Label(hud.transform, "", .03f, .87f, .8f, .09f, 26, Color.white);
            prompt = Label(hud.transform, "+", .30f, .42f, .4f, .10f, 24, Color.white); prompt.alignment = TextAnchor.MiddleCenter;
            notice = Label(hud.transform, "", .03f, .04f, .90f, .09f, 20, Color.white);
        }

        private void Update()
        {
            if (world == null || connector == null || connector.Context == null) return;
            var state = connector.Context;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) { Exit(); return; }
            if (state.phase == MoonStagePhase.Descent && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                connector.Interact(MoonInteractionIds.ConfirmDescent);
            foreach (var target in targets)
            {
                var targetCollider = target.GetComponent<Collider>();
                bool availableTarget = target.AvailableId(connector) != null;
                if (targetCollider != null) targetCollider.enabled = availableTarget;
                var marker = target.GetComponentInChildren<Renderer>();
                if (marker != null) marker.enabled = availableTarget;
            }
            if (cover != null) cover.enabled = !state.cart.coverOpen;
            for (int i = 0; i < 3; i++)
            {
                if (dust[i] != null) dust[i].enabled = state.cart.coverOpen && !state.cart.cleaned[i];
                if (patchTargets[i] != null) patchTargets[i].SetActive(state.cart.coverOpen && !state.cart.cleaned[i]);
            }
            string[] available = state.availableInteractionIds ?? Array.Empty<string>();
            objective.text = (testMode ? "MOON TEST · " : "POLE-7 · ") +
                (state.phase == MoonStagePhase.Descent ? "Press E to confirm descent" :
                 state.phase == MoonStagePhase.Complete ? "Cart repaired and cargo loaded" :
                 state.phase == MoonStagePhase.RestartAtPreLaunch ? "Descent failed · return to Earth" :
                 available.Length == 0 ? "Awaiting next objective" : string.Join(" / ", available.Select(ActionLabel)));
            var focusedTarget = player.FocusedTarget;
            prompt.text = focusedTarget != null && focusedTarget.AvailableId(connector) != null ?
                (focusedTarget.RequiresHold ? "HOLD E · " : "E · ") + focusedTarget.Prompt : "+";
            notice.text = connector.Notice + "    |    WASD move · Mouse look · Tab return · Esc unlock mouse";
        }
        private static string ActionLabel(string id)
        {
            switch (id)
            {
                case MoonInteractionIds.StartCart: return "Start cart";
                case MoonInteractionIds.SwitchCartOff: return "Switch cart off";
                case MoonInteractionIds.OpenWheelCover: return "Open wheel cover";
                case MoonInteractionIds.CloseWheelCover: return "Close wheel cover";
                case MoonInteractionIds.RetestCart: return "Retest cart";
                case MoonInteractionIds.LoadPanel: return "Load panel";
                case MoonInteractionIds.LoadKit: return "Load kit";
                default: return "Clean dust patches";
            }
        }
        public void Exit()
        {
            if (world == null) return;
            world.SetActive(false); Destroy(world); world = null;
            targets.Clear();
            if (markerMaterial != null) { Destroy(markerMaterial); markerMaterial = null; }
            if (testOwner != null) { Destroy(testOwner); testOwner = null; }
            if (hiddenCanvas != null) { hiddenCanvas.SetActive(true); hiddenCanvas = null; }
        }
        private void OnDestroy() { Exit(); }
    }
}
