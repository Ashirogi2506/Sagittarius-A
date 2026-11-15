using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Mars;
using SagittariusA.Gameplay.UI;
using SagittariusA.Minigames;
using SagittariusA.Minigames.MoonPowerRouting;
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
        private readonly List<AudioListener> hiddenListeners = new List<AudioListener>();
        private GameObject cartRoot, playerObject;
        private GameObject hudObject, completionPanel;
        private MoonStageController activeStage;
        private Collider terrainSurface;
        private Vector3 bridgeStop, baseStop, cartStart;
        private bool driving;
        private bool ridgeLanding;
        private bool previousReturnArrived;
        private Vector3[] drivePath;
        private int previousRouteStep;
        private float cartGroundOffset;
        private MoonCartMotor cartMotor;
        private MoonCartRoutePlanner routePlanner;
        private string routeNotice;
        private MoonRepairPanel repairPanel;
        private TaskBriefingView briefingPanel;
        private readonly HashSet<string> explainedTasks = new HashSet<string>();
        private bool sharedTaskActive, exiting;
        private MinigameManager sharedManager;
        private string pendingDrive;
        private string destinationName;
        private float worldInputResumeAt;
        private Renderer doorLeft, doorRight;
        private Vector3 leftDoorClosed, rightDoorClosed;
        private bool doorsOpen;
        private MoonSceneConnector connector;
        private MoonFirstPersonRig player;
        private Text objective, prompt, notice;
        private Renderer cover;
        private Collider coverCollider;
        private readonly GameObject[] cargoVisuals = new GameObject[2];
        private readonly GameObject[] heldVisuals = new GameObject[2];
        private readonly GameObject[] loadedVisuals = new GameObject[2];
        private readonly Renderer[] dust = new Renderer[3];
        private GameObject[] patchTargets;
        private readonly List<MoonWorldTarget> targets = new List<MoonWorldTarget>();
        private Material markerMaterial;
        private UnityEngine.Rendering.AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private bool ambientOverridden;
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
            ReleaseServices.Film("MoonLanding",()=>Begin(stage, false));
        }

        public void StartIsolatedTest()
        {
            StartIsolatedTestForPad("main");
        }

        public void StartIsolatedTestForPad(string padId)
        {
            if (world != null) return;
            testOwner = new GameObject("Moon isolated test state");
            var stage = testOwner.AddComponent<MoonStageController>();
            stage.Initialize(new EarthMoonHandoff {
                missionId = "pole-7", commanderId = "commander_male", playerName = "Test Commander",
                build = new EarthBuild(), checks = new EarthChecks { padId = padId, arrivalMinute = 2,
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
            activeStage = stage;
            testMode = isolated;
            worldInputResumeAt = 0;
            exiting = false; sharedTaskActive = false; explainedTasks.Clear();
            ridgeLanding = stage.SelectedPadId == "ridge";
            var environment = Instantiate(model, world.transform);
            // This FBX was exported in centimetres; Unity imports it at 100x the
            // source's metre scale. Normalize the complete environment together.
            environment.transform.localScale = Vector3.one * .01f;
            Physics.SyncTransforms();
            if (ridgeLanding)
            {
                var beaconPad = FindMesh(environment, "LandingPad_Beacon");
                var ridgePad = FindMesh(environment, "LandingPad_Ridge");
                if (beaconPad == null || ridgePad == null)
                { Debug.LogError("Ridge or Beacon landing pad is missing from the Moon environment."); Destroy(world); world = null; if (testOwner != null) Destroy(testOwner); return; }
                Vector3 padOffset = ridgePad.bounds.center - beaconPad.bounds.center;
                // The painted cart marks follow uneven terrain and can extend
                // below a pad's bounding box. Align the lander to pad tops.
                padOffset.y = ridgePad.bounds.max.y - beaconPad.bounds.max.y;
                foreach (var part in environment.GetComponentsInChildren<MeshRenderer>())
                    if (part.name.Contains("_Cart_") || part.name.Contains("_Lander_")) part.transform.position += padOffset;
                Physics.SyncTransforms();
            }
            // The updated Pole-7 model places the landing-pad safety railing
            // through the authored cart. Move every cart mesh together, before
            // creating colliders, cargo or interaction targets from their bounds.
            foreach (var part in environment.GetComponentsInChildren<MeshRenderer>())
                if (part.name.Contains("_Cart_")) part.transform.position += new Vector3(-5.5f, 0, 0);
            Physics.SyncTransforms();
            var terrain = FindMesh(environment, "Moon_Terrain");
            var chassis = FindMesh(environment, "Cart_Chassis");
            var console = FindMesh(environment, "Cart_StatusConsole");
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
            var terrainCollider = terrain.gameObject.AddComponent<MeshCollider>();
            terrainCollider.sharedMesh = meshFilter.sharedMesh;
            terrainSurface = terrainCollider;
            Physics.SyncTransforms();
            // The moved cart must follow the ground at its new location. Keep
            // all parts together and rest the lowest wheel contacts on terrain.
            var cartParts = environment.GetComponentsInChildren<MeshRenderer>()
                .Where(x => x.name.Contains("_Cart_")).ToArray();
            var wheels = new[] { "Cart_Wheel_FL", "Cart_Wheel_FR", "Cart_Wheel_RL", "Cart_Wheel_RR" }
                .Select(name => FindMesh(environment, name)).Where(x => x != null).ToArray();
            if (wheels.Length != 4)
            {
                Debug.LogError("Moon cart needs all four named wheel meshes.");
                Destroy(world); world = null; if (testOwner != null) Destroy(testOwner); return;
            }
            if (wheels.Length == 4)
            {
                float lift = wheels.Max(wheel => SurfaceHeight(wheel.bounds.center, terrainCollider) - wheel.bounds.min.y);
                foreach (var part in cartParts) part.transform.position += Vector3.up * lift;
                Physics.SyncTransforms();
            }
            cartRoot = new GameObject("Mobile cargo cart");
            cartRoot.transform.SetParent(world.transform);
            cartRoot.transform.position = chassis.bounds.center;
            // FBX pivots do not define vehicle forward. Derive it from the axles
            // before parenting the meshes, preserving their authored world pose.
            if (wheels.Length == 4)
            {
                Vector3 forward = wheels[0].bounds.center + wheels[1].bounds.center - wheels[2].bounds.center - wheels[3].bounds.center;
                forward.y = 0;
                cartRoot.transform.rotation = Quaternion.LookRotation(forward.normalized);
            }
            foreach (var part in cartParts) part.transform.SetParent(cartRoot.transform, true);
            cartStart = cartRoot.transform.position;
            cartGroundOffset = cartStart.y - SurfaceHeight(cartStart, terrainCollider);
            var bridge = FindMesh(environment, "Bridge_Broken");
            var parkingBay = FindMesh(environment, "CartParkingBay");
            var cargoDoor = FindMesh(environment, "Pole7_CargoDoor_L");
            doorLeft = cargoDoor;
            doorRight = FindMesh(environment, "Pole7_CargoDoor_R");
            if (doorLeft != null) leftDoorClosed = doorLeft.transform.position;
            if (doorRight != null) rightDoorClosed = doorRight.transform.position;
            doorsOpen = false;
            // Park on the authored bay, outside the large solid survey ledge.
            // The old +5 offset put the cart inside that ledge's mesh.
            bridgeStop = parkingBay != null ? parkingBay.bounds.center :
                bridge != null ? bridge.bounds.center + new Vector3(-7f, 0, bridge.bounds.extents.z + 4f) : cartStart;
            if (parkingBay != null)
            {
                float bayHeight = SurfaceHeight(bridgeStop, terrainCollider) + .02f;
                parkingBay.transform.position += Vector3.up * (bayHeight - parkingBay.bounds.min.y);
            }
            baseStop = cargoDoor != null ? cargoDoor.bounds.center + new Vector3(4f, 0, 5f) : cartStart;
            bridgeStop.y = SurfaceHeight(bridgeStop, terrainCollider) + cartGroundOffset;
            baseStop.y = SurfaceHeight(baseStop, terrainCollider) + cartGroundOffset;
            var savedContext = stage.Phase;
            bool savedAtBase = savedContext == MoonStagePhase.Pole7Repair || savedContext == MoonStagePhase.ReturnToLander || savedContext == MoonStagePhase.Complete;
            // The context is available after the connector binds below; route
            // checkpoints are positioned there before player and targets spawn.
            foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer == terrain || !IsSolidEnvironmentMesh(renderer.name)) continue;
                var mesh = renderer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                var collision = renderer.gameObject.AddComponent<MeshCollider>();
                collision.sharedMesh = mesh.sharedMesh;
                if (renderer == cover) coverCollider = collision;
            }
            Physics.SyncTransforms();
            var connectorObject = new GameObject("Moon scene connector"); connectorObject.transform.SetParent(world.transform);
            connector = connectorObject.AddComponent<MoonSceneConnector>(); connector.Bind(stage);
            previousRouteStep = connector.Context.routeStep;
            previousReturnArrived = connector.Context.returnCartArrived;
            if ((savedContext == MoonStagePhase.ReturnToLander && previousReturnArrived) || savedContext == MoonStagePhase.Complete) cartRoot.transform.position = cartStart;
            else if (savedAtBase || (savedContext == MoonStagePhase.RouteSurvey && previousRouteStep >= 4)) cartRoot.transform.position = baseStop;
            else if (savedContext == MoonStagePhase.RouteSurvey && previousRouteStep >= 1) cartRoot.transform.position = bridgeStop;
            Physics.SyncTransforms();
            routePlanner = new MoonCartRoutePlanner(terrainCollider, cartRoot.transform);
            cartMotor = new MoonCartMotor(cartRoot.transform, terrainCollider, wheels, routePlanner.IsClear);
            routeNotice = null;
            var markerShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (markerShader != null) markerMaterial = new Material(markerShader) { color = new Color(1f, .5f, .05f) };
            var lamp = new GameObject("Moon sunlight", typeof(Light)); lamp.transform.SetParent(world.transform);
            lamp.transform.rotation = Quaternion.Euler(40, -40, 0); lamp.GetComponent<Light>().type = LightType.Directional;
            lamp.GetComponent<Light>().intensity = 1.4f;
            previousAmbientMode = RenderSettings.ambientMode; previousAmbientLight = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.28f, .30f, .36f);
            ambientOverridden = true;
            playerObject = new GameObject("Moon first-person player", typeof(CharacterController));
            playerObject.transform.SetParent(world.transform);
            var body = playerObject.GetComponent<CharacterController>(); body.height = 1.8f; body.center = new Vector3(0, .9f, 0);
            Vector3 spawn = chassis.bounds.center + new Vector3(0, 0, 3);
            // Only the lunar ground determines spawn height. A general raycast
            // can hit the cart, lander, or a rail and put the player inside it.
            spawn.y = SurfaceHeight(spawn, terrainCollider) + .1f;
            playerObject.transform.position = spawn;
            Vector3 direction = chassis.bounds.center - spawn; direction.y = 0;
            if (direction.sqrMagnitude > .001f) playerObject.transform.rotation = Quaternion.LookRotation(direction);
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if(listener.enabled){hiddenListeners.Add(listener);listener.enabled=false;}
            var cameraObject = new GameObject("Moon player camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(playerObject.transform, false); cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<Camera>().backgroundColor = new Color(.012f, .018f, .032f);
            player = new MoonFirstPersonRig(body, cameraObject.GetComponent<Camera>(), connector, 1 << TargetLayer, RequestInteraction);
            Vector3 switchPosition = console != null ? console.bounds.center + Vector3.up * .3f : chassis.bounds.center + new Vector3(0, .5f, .85f);
            CreateTarget("Cart switch", switchPosition, new Vector3(1.2f, .9f, .8f), false,
                MoonInteractionIds.StartCart, MoonInteractionIds.SwitchCartOff, MoonInteractionIds.RetestCart,
                MoonInteractionIds.DriveBridge, MoonInteractionIds.DriveBase, MoonInteractionIds.DriveLander).transform.SetParent(cartRoot.transform, true);
            CreateTarget("Wheel cover", cover.bounds.center, Vector3.Max(cover.bounds.size, new Vector3(.4f, .5f, .5f)), false,
                MoonInteractionIds.OpenWheelCover, MoonInteractionIds.CloseWheelCover).transform.SetParent(cartRoot.transform, true);
            patchTargets = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = cover.bounds.center + new Vector3(-.25f, (i - 1) * .18f, -.15f);
                patchTargets[i] = CreateTarget("Clean wheel dust " + (i + 1), pos, new Vector3(.25f, .15f, .25f), false,
                    i == 0 ? MoonInteractionIds.CleanPatch1 : i == 1 ? MoonInteractionIds.CleanPatch2 : MoonInteractionIds.CleanPatch3);
                patchTargets[i].transform.SetParent(cartRoot.transform, true);
            }
            Vector3 panelPosition = chassis.bounds.center + new Vector3(-1.8f, 0, -.5f);
            Vector3 kitPosition = chassis.bounds.center + new Vector3(1.8f, 0, -.5f);
            panelPosition.y = SurfaceHeight(panelPosition, terrainCollider);
            kitPosition.y = SurfaceHeight(kitPosition, terrainCollider);
            cargoVisuals[0] = CreateCargoVisual("panel", panelPosition);
            cargoVisuals[1] = CreateCargoVisual("kit", kitPosition);
            PlaceCargo(cargoVisuals[0], panelPosition);
            PlaceCargo(cargoVisuals[1], kitPosition);
            var deck = chassis.bounds;
            // Chassis bounds include the raised frame. Find the bed itself
            // beneath each item instead of using the top of the bounding box.
            var panelOnCart = new Vector3(deck.center.x, deck.center.y, deck.center.z - .43f);
            var kitOnCart = new Vector3(deck.center.x, deck.center.y, deck.center.z + .43f);
            var chassisCollider = chassis.GetComponent<MeshCollider>();
            if (chassisCollider != null)
            {
                panelOnCart.y = SurfaceHeight(panelOnCart, chassisCollider);
                kitOnCart.y = SurfaceHeight(kitOnCart, chassisCollider);
            }
            loadedVisuals[0] = CreateCargoVisual("panel", panelOnCart);
            loadedVisuals[1] = CreateCargoVisual("kit", kitOnCart);
            PlaceCargo(loadedVisuals[0], panelOnCart);
            PlaceCargo(loadedVisuals[1], kitOnCart);
            loadedVisuals[0].transform.SetParent(cartRoot.transform, true);
            loadedVisuals[1].transform.SetParent(cartRoot.transform, true);
            for (int i = 0; i < heldVisuals.Length; i++)
            {
                heldVisuals[i] = CreateCargoVisual(i == 0 ? "panel" : "kit", Vector3.zero);
                heldVisuals[i].transform.SetParent(cameraObject.transform, false);
                heldVisuals[i].transform.localPosition = new Vector3(.34f, -.28f, .75f);
                heldVisuals[i].transform.localRotation = Quaternion.Euler(5, -20, 0);
                heldVisuals[i].transform.localScale *= .55f;
                heldVisuals[i].SetActive(false);
            }
            CreateTarget("Pick up P-4 panel", CargoBounds(cargoVisuals[0]).center, new Vector3(.8f, .8f, .8f), false, MoonInteractionIds.PickUpPanel);
            CreateTarget("Pick up repair kit", CargoBounds(cargoVisuals[1]).center, new Vector3(.8f, .8f, .8f), false, MoonInteractionIds.PickUpKit);
            CreateTarget("Place P-4 panel on cart", panelOnCart + Vector3.up * .25f, new Vector3(.7f, .5f, .7f), false, MoonInteractionIds.LoadPanel).transform.SetParent(cartRoot.transform, true);
            CreateTarget("Place repair kit on cart", kitOnCart + Vector3.up * .25f, new Vector3(.7f, .5f, .7f), false, MoonInteractionIds.LoadKit).transform.SetParent(cartRoot.transform, true);
            Vector3 bridgeInspect = bridgeStop + new Vector3(-2, 0, -1);
            bridgeInspect.y = SurfaceHeight(bridgeInspect, terrainCollider) + 1.2f;
            CreateTarget("Inspect closed bridge", bridgeInspect, Vector3.one * 1.5f, false, MoonInteractionIds.InspectBridge);
            var ledge = FindMesh(environment, "SurveyLedge");
            Vector3 survey = ledge != null ? new Vector3(ledge.bounds.center.x, 0, ledge.bounds.max.z + 1.5f) : bridgeStop + Vector3.right * 5;
            survey.y = SurfaceHeight(survey, terrainCollider) + 1.2f;
            CreateTarget("Survey and mark firm route", survey, Vector3.one * 1.5f, false, MoonInteractionIds.SurveyRoute);
            Vector3 steep = survey + new Vector3(4, 0, 0);
            steep.y = SurfaceHeight(steep, terrainCollider) + 1.2f;
            CreateTarget("Inspect steep loose route (optional)", steep, Vector3.one * 1.5f, false, MoonInteractionIds.InspectSteep);
            CreateEnvironmentTarget(environment, "Pole7_CargoDoor_L", "Deliver panel and repair kit", MoonInteractionIds.DeliverCargo);
            CreateEnvironmentTarget(environment, "Pole7_DisconnectLever", "Isolate damaged circuit", MoonInteractionIds.IsolateCircuit);
            CreateEnvironmentTarget(environment, "Pole7_CabinetDoor", "Repair circuit with kit", MoonInteractionIds.RepairCircuit);
            var cabinet = FindMesh(environment, "Pole7_CabinetDoor");
            if (cabinet != null)
                CreateTarget("Repair circuit with powered laser", cabinet.bounds.center + new Vector3(1f, 0, .5f), new Vector3(.6f, 1f, .6f), false, MoonInteractionIds.RepairLaser);
            CreateEnvironmentTarget(environment, "Pole7_CabinetDoor", "Install P-4 panel", MoonInteractionIds.InstallPanel);
            CreateEnvironmentTarget(environment, "Pole7_EssentialBreaker", "Restore essential power", MoonInteractionIds.RestoreEssential);
            CreateEnvironmentTarget(environment, "Pole7_RelayBreaker", "Restore relay", MoonInteractionIds.RestoreRelay);
            CreateEnvironmentTarget(environment, "Pole7_MapScreen", "Upload route and receive Mars file", MoonInteractionIds.UploadRoute);
            CreateEnvironmentTarget(environment, "Lander_CargoRamp", "Return to the lander", MoonInteractionIds.ReturnToShip);
            CreateEnvironmentTarget(environment, "DeadBeacon", "Inspect and mark dead beacon (optional)", MoonInteractionIds.InspectBeacon);
            CreateEnvironmentTarget(environment, "DeadBeacon", "Leave portable battery here for Pole-7 (optional)", MoonInteractionIds.LeaveBattery);
            var oldLander=FindMesh(environment,"Lander_Body");
            if(oldLander!=null&&Resources.Load<GameObject>("Ships/ExpeditionShip")!=null)
            {
                var at=oldLander.bounds.center;var pad=FindMesh(environment,ridgeLanding?"LandingPad_Ridge":"LandingPad_Beacon");at.y=pad!=null?pad.bounds.max.y:SurfaceHeight(at,terrainCollider);
                foreach(var r in environment.GetComponentsInChildren<Renderer>())if(r.name.Contains("_Lander_")&&!r.name.Contains("CargoRamp"))r.gameObject.SetActive(false);
                ExpeditionShipVisual.Create(world.transform,at);
            }
            CreateHud();
            var canvas = GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.gameObject != world && !c.transform.IsChildOf(world.transform));
            if (canvas != null) { hiddenCanvas = canvas.gameObject; hiddenCanvas.SetActive(false); }
        }

        private static Renderer FindMesh(GameObject root, string name)
        { return root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name.EndsWith("_" + name, StringComparison.Ordinal)); }

        private void CreateEnvironmentTarget(GameObject environment, string meshName, string description, string id)
        {
            var mesh = FindMesh(environment, meshName);
            if (mesh == null) { Debug.LogWarning("Moon objective mesh missing: " + meshName); return; }
            var bounds = mesh.bounds;
            var position = bounds.center + new Vector3(0, 0, bounds.extents.z + .5f);
            CreateTarget(description, position, new Vector3(1.2f, 1.2f, .8f), false, id);
        }

        private static bool IsSolidEnvironmentMesh(string name)
        {
            return name.Contains("_Rock_") || name.Contains("_Lander_Body") || name.Contains("_Lander_CargoRamp") ||
                name.Contains("_LandingPad_") || name.Contains("_Pole7_Exterior") || name.Contains("_Pole7_ServiceRoom") ||
                name.Contains("_Pole7_CargoDoor") || name.Contains("_Pole7_CabinetDoor") || name.Contains("_DeadBeacon") ||
                name.Contains("_Bridge_Broken") || name.Contains("_SurveyLedge") || name.Contains("_ServicePost") ||
                name.Contains("Landing pad safety rails") ||
                name.Contains("_CartParkingBay") || name.Contains("_Cart_StatusConsole") || name.Contains("_Cart_Chassis") ||
                name.Contains("_Cart_Wheel_") || name.Contains("_Cart_WheelCover_FR");
        }
        private static float SurfaceHeight(Vector3 position, Collider terrain)
        {
            var ray = new Ray(position + Vector3.up * 20, Vector3.down);
            return terrain.Raycast(ray, out var hit, 100f) ? hit.point.y : position.y;
        }

        private GameObject CreateCargoVisual(string id, Vector3 position)
        {
            var holder = new GameObject("Loose cargo " + id); holder.transform.SetParent(world.transform); holder.transform.position = position;
            var prefab = Resources.Load<GameObject>("EarthParts/" + id);
            if (prefab != null) Instantiate(prefab, holder.transform, false);
            else
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube); fallback.transform.SetParent(holder.transform, false);
                fallback.transform.localScale = id == "panel" ? new Vector3(.8f, .12f, .65f) : new Vector3(.45f, .35f, .4f);
                var collision = fallback.GetComponent<Collider>(); collision.enabled = false; Destroy(collision);
            }
            foreach (var collision in holder.GetComponentsInChildren<Collider>()) collision.enabled = false;
            var renderers = holder.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (largest > .001f) holder.transform.localScale *= .7f / largest;
            }
            return holder;
        }

        private static Bounds CargoBounds(GameObject cargo)
        {
            var renderers = cargo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(cargo.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void PlaceCargo(GameObject cargo, Vector3 bottomCenter)
        {
            // Prefabs have different pivots; use their visible bounds after
            // scaling, rather than guessing an offset from the pivot.
            var bounds = CargoBounds(cargo);
            cargo.transform.position += new Vector3(bottomCenter.x - bounds.center.x,
                bottomCenter.y - bounds.min.y, bottomCenter.z - bounds.center.z);
        }

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
            var hud = new GameObject("Moon HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); hud.transform.SetParent(world.transform);
            hudObject = hud;
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            objective = Label(hud.transform, "", .03f, .87f, .8f, .09f, 26, Color.white);
            prompt = Label(hud.transform, "+", .30f, .42f, .4f, .10f, 24, Color.white); prompt.alignment = TextAnchor.MiddleCenter;
            notice = Label(hud.transform, "", .03f, .04f, .90f, .09f, 20, Color.white);
        }

        private void Update()
        {
            if(SagittariusA.Gameplay.Flow.ReleaseServices.Modal)return;
            if (world == null) return;
            // Unity can reload scripts while Play mode is active. Scene objects
            // survive that reload, but this non-serializable player helper does
            // not. Close the interrupted surface session cleanly so it can be
            // started again instead of throwing every frame.
            if (player == null)
            {
                Debug.LogWarning("Moon surface was interrupted by a script reload. Start the Moon surface again.");
                Exit();
                return;
            }
            if (connector == null || connector.Context == null) return;
            if (repairPanel != null || briefingPanel != null || sharedTaskActive) { player.ReleaseHold(); return; }
            if (Time.unscaledTime < worldInputResumeAt) return;
            if (!driving && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                string id = player.FocusedTarget?.AvailableId(connector) ?? connector.Context.availableInteractionIds?.FirstOrDefault();
                if (id != null) { ExplainTask(id, true); return; }
            }
            bool cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            if (driving) player.TickLookOnly();
            else if (connector.Context.phase != MoonStagePhase.Complete) player.Tick();
            var state = connector.Context;
            if (state.phase == MoonStagePhase.Complete && completionPanel == null) ShowMoonResults(state);
            previousRouteStep = state.routeStep;
            previousReturnArrived = state.returnCartArrived;
            if (driving) UpdateCartTrip();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) { Exit(); return; }
            if (state.phase == MoonStagePhase.Complete && keyboard != null && keyboard.enterKey.wasPressedThisFrame) { Exit(); return; }
            if (state.phase == MoonStagePhase.Descent && cursorWasLocked &&
                ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (keyboard != null && keyboard.eKey.wasPressedThisFrame)))
                RequestInteraction(MoonInteractionIds.ConfirmDescent);
            foreach (var target in targets)
            {
                var targetCollider = target.GetComponent<Collider>();
                bool availableTarget = target.AvailableId(connector) != null;
                if (targetCollider != null) targetCollider.enabled = availableTarget;
                var marker = target.GetComponentInChildren<Renderer>();
                if (marker != null) marker.enabled = availableTarget;
            }
            if (cover != null) cover.enabled = !state.cart.coverOpen;
            if (coverCollider != null) coverCollider.enabled = !state.cart.coverOpen;
            if (cargoVisuals[0] != null) cargoVisuals[0].SetActive(!state.cart.panelLoaded && state.carriedCargoId != "panel");
            if (cargoVisuals[1] != null) cargoVisuals[1].SetActive(!state.cart.kitLoaded && state.carriedCargoId != "kit");
            if (heldVisuals[0] != null) heldVisuals[0].SetActive(state.carriedCargoId == "panel");
            if (heldVisuals[1] != null) heldVisuals[1].SetActive(state.carriedCargoId == "kit");
            bool delivered = state.phase == MoonStagePhase.Pole7Repair || state.phase == MoonStagePhase.ReturnToLander || state.phase == MoonStagePhase.Complete;
            if (loadedVisuals[0] != null) loadedVisuals[0].SetActive(state.cart.panelLoaded && !delivered);
            if (loadedVisuals[1] != null) loadedVisuals[1].SetActive(state.cart.kitLoaded && !delivered);
            OpenCargoDoors(delivered);
            for (int i = 0; i < 3; i++)
            {
                if (dust[i] != null) dust[i].enabled = state.cart.coverOpen && !state.cart.cleaned[i];
                if (patchTargets[i] != null) patchTargets[i].SetActive(state.cart.coverOpen && !state.cart.cleaned[i]);
            }
            string[] available = state.availableInteractionIds ?? Array.Empty<string>();
            objective.text = (testMode ? "MOON TEST Â· " : "POLE-7 Â· ") +
                (state.phase == MoonStagePhase.Descent ? "LEFT CLICK to confirm descent" :
                 state.phase == MoonStagePhase.Complete ? "Moon mission complete Â· Mars file received" :
                 state.phase == MoonStagePhase.RestartAtPreLaunch ? "Descent failed Â· return to Earth" :
                 driving ? destinationName + "  Â·  " + Mathf.RoundToInt(cartMotor.Progress * 100) + "%  Â·  " + (cartMotor.Speed * 3.6f).ToString("0") + " km/h" :
                 available.Length == 0 ? "Awaiting next objective" : string.Join(" / ", available.Where(x => x != MoonInteractionIds.InspectBeacon && x != MoonInteractionIds.LeaveBattery && x != MoonInteractionIds.InspectSteep).Select(ActionLabel).Distinct()) + NavigationHint());
            var focusedTarget = player.FocusedTarget;
            string focusedAction = focusedTarget == null ? null : focusedTarget.AvailableId(connector);
            prompt.text = focusedAction != null ?
                (IsPickup(focusedAction) ? "RIGHT CLICK Â· " : "LEFT CLICK Â· ") + focusedTarget.Prompt + (MoonRepairPanel.Supports(focusedAction) ? "  [REPAIR TASK]" : "") : "+";
            string carrying = string.IsNullOrEmpty(state.carriedCargoId) ? "Empty hands" : "Carrying " + (state.carriedCargoId == "panel" ? "P-4 panel" : "repair kit");
            notice.text = state.phase == MoonStagePhase.Complete ?
                "POLE-7 ONLINE Â· Route uploaded Â· Mars approach file received Â· Press Enter to return" :
                (driving ? "AUTOMATIC CARGO ROUTE   |   Mouse look Â· Esc unlock   |   Dismounts at the next stop" : (routeNotice ?? connector.Notice) + "   |   " + carrying + "   |   WASD move Â· Space jump Â· Left use Â· Right pick up Â· H explain Â· Tab return Â· Esc unlock");
        }

        public void RequestInteraction(string id)
        {
            if (exiting || connector == null || !connector.IsAvailable(id) || driving || repairPanel != null || briefingPanel != null || sharedTaskActive) return;
            bool needsBriefing = CampaignTaskBriefings.IsMoonMilestone(id) || id == MoonInteractionIds.LeaveBattery;
            if (needsBriefing && !explainedTasks.Contains(id)) { ExplainTask(id, false); return; }
            ExecuteInteraction(id);
        }

        private void ExplainTask(string id, bool helpOnly)
        {
            var data = CampaignTaskBriefings.Moon(id, connector.Context);
            if (data == null) { if (!helpOnly) ExecuteInteraction(id); return; }
            string run = connector.Context.runId;
            player.ReleaseHold();
            briefingPanel = TaskBriefingView.Open(world.transform, data, accepted =>
            {
                briefingPanel = null;
                if (exiting || world == null || connector?.Context?.runId != run) return;
                worldInputResumeAt = Time.unscaledTime + .18f;
                if (!accepted || helpOnly) return;
                explainedTasks.Add(id);
                if (connector.IsAvailable(id)) ExecuteInteraction(id);
            }, helpOnly);
        }

        private void ExecuteInteraction(string id)
        {
            if (id == MoonInteractionIds.RestoreEssential)
            {
                player.ReleaseHold(); sharedTaskActive = true;
                string run = connector.Context.runId;
                sharedManager = MinigameManager.Instance;
                sharedManager.StartMinigame(new MinigameContext {
                    MinigameId = MoonPowerRoutingMinigame.IdValue, MissionId = "pole-7", EventId = id,
                    DifficultyLevel = 1, RandomSeed = 0, TimeLimitSeconds = 0, AllowCancel = true
                }, result =>
                {
                    sharedTaskActive = false; sharedManager = null;
                    if (exiting || world == null || connector?.Context?.runId != run) return;
                    bool valid = result != null && result.MinigameId == MoonPowerRoutingMinigame.IdValue &&
                        result.MissionId == "pole-7" && result.EventId == id && result.Outcome == MinigameOutcome.Success;
                    if (valid && connector.IsAvailable(id)) connector.Interact(id);
                    // Suggested prototype stat modifiers are intentionally not
                    // applied: the campaign controller owns its resource math.
                    routeNotice = valid ? null : "Power routing incomplete. Use the panel to retry; your earlier repairs are saved.";
                    worldInputResumeAt = Time.unscaledTime + .18f;
                    Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                });
                return;
            }
            if (id == MoonInteractionIds.DriveBridge || id == MoonInteractionIds.DriveBase || id == MoonInteractionIds.DriveLander)
            {
                pendingDrive = id;
                BeginCartTrip(id == MoonInteractionIds.DriveBridge ? 0 : id == MoonInteractionIds.DriveBase ? 1 : 2);
                return;
            }
            if (!MoonRepairPanel.Supports(id)) { connector.Interact(id); return; }
            player.ReleaseHold();
            repairPanel = MoonRepairPanel.Open(world.transform, id, success =>
            {
                repairPanel = null;
                if (world == null || connector == null) return;
                if (success && connector.IsAvailable(id))
                {
                    if (id == MoonInteractionIds.CleanPatch1 || id == MoonInteractionIds.CleanPatch2 || id == MoonInteractionIds.CleanPatch3)
                        connector.HoldPatch(id, MissionSimulation.Minigames.CartWheelMinigame.HoldSecondsPerPatch);
                    else connector.Interact(id);
                }
                // The completion click must not leak into the world behind it.
                worldInputResumeAt = Time.unscaledTime + .15f;
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            });
        }

        private string NavigationHint()
        {
            var target = targets.FirstOrDefault(x => x != null && x.AvailableId(connector) != null);
            if (target == null || playerObject == null) return "";
            Vector3 direction = target.transform.position - playerObject.transform.position;
            direction.y = 0;
            float distance = direction.magnitude;
            if (distance < 3f) return "  Â·  Nearby";
            float angle = Vector3.SignedAngle(playerObject.transform.forward, direction, Vector3.up);
            string bearing = Mathf.Abs(angle) < 25 ? "ahead" : Mathf.Abs(angle) > 150 ? "behind" : angle < 0 ? "left" : "right";
            return "  Â·  " + Mathf.RoundToInt(distance) + " m " + bearing;
        }

        private void ShowMoonResults(MoonStageContext state)
        {
            if (hudObject == null) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var panel = Box(hudObject.transform, "Moon results", .18f, .17f, .64f, .65f, new Color(.035f, .05f, .075f, .98f));
            completionPanel = panel.gameObject;
            Label(panel, "POLE-7 ONLINE", .06f, .79f, .88f, .15f, 42, Color.white);
            string beacon = state.batteryLeft ? "Battery left at the beacon." : state.beaconMarked ? "Dead beacon marked." : "Beacon not inspected.";
            Label(panel, "Commander " + state.playerName + " delivered the P-4 panel, restored the relay and uploaded the safe cargo route.\n\n" +
                "Mars engineer: Commander " + state.playerName + ", the assembly bay is ready. Come to Mars and we'll test your Jupiter orbiter together.\n\n" + beacon + "   Fuel remaining: " +
                state.descent.remainingFuelUnits.ToString("0.#") + " units.", .06f, .34f, .88f, .43f, 22, Color.white);
            if (testMode) Label(panel, "TEST RUN Â· Campaign progress is not changed.", .06f, .24f, .88f, .08f, 16, new Color(1f, .65f, .2f));
            ActionButton(panel, "CONTINUE TO MARS", .49f, .08f, .44f, .12f, ShowMarsStaging, new Color(1f, .43f, .04f));
            ActionButton(panel, "MAIN MENU", .06f, .08f, .36f, .12f, ReturnToMenu);
        }

        private void ShowMarsStaging()
        {
            var handoff = activeStage != null ? activeStage.GetMarsHandoff() : null;
            if (handoff == null || hudObject == null) return;
            if (completionPanel != null) Destroy(completionPanel);
            var panel = Box(hudObject.transform, "Mars staging", .18f, .17f, .64f, .65f, new Color(.035f, .05f, .075f, .98f));
            completionPanel = panel.gameObject;
            Label(panel, "MARS STAGING", .06f, .79f, .88f, .15f, 42, Color.white);
            Label(panel, "Pole-7 restored. Next stop: the Mars orbiter workshop.\n\n" +
                "Our ascent craft will dock with the expedition transfer ship in lunar orbit, reconnecting to long-range propulsion and supplies for Mars.\n\nCommander: " + handoff.playerName + "\nFuel: " + handoff.remainingFuelUnits.ToString("0.#") + " units\nCargo: " +
                string.Join(", ", handoff.build.cargo), .06f, .27f, .88f, .5f, 22, Color.white);
            ActionButton(panel, "BEGIN MARS TRANSIT", .49f, .08f, .44f, .12f, StartMarsCampaign, new Color(1f, .43f, .04f));
            ActionButton(panel, "MAIN MENU", .06f, .08f, .36f, .12f, ReturnToMenu);
        }

        private void StartMarsCampaign()
        {
            var handoff = activeStage != null ? activeStage.GetMarsHandoff() : null;
            if (handoff == null) return;
            bool isolated = testMode;
            Exit();
            var marsStage = GetComponent<MarsStageController>();
            if (marsStage == null) marsStage = gameObject.AddComponent<MarsStageController>();
            var mars = GetComponent<MarsPlayableExperience>();
            if (mars == null) mars = gameObject.AddComponent<MarsPlayableExperience>();
            if (marsStage.Initialize(handoff, isolated ? new MemoryMarsDraftStore() : null)) ReleaseServices.Film("MoonTakeoff",()=>mars.StartCampaign(isolated));
        }

        private void ReturnToMenu()
        {
            Exit();
            GetComponent<GameFlowController>()?.Show(GameScreen.MainMenu);
        }

        private void BeginCartTrip(int trip)
        {
            if (cartRoot == null || playerObject == null) return;
            var body = playerObject.GetComponent<CharacterController>();
            // Keep west of both the ledge and the broken bridge until the
            // route has passed them, then turn toward Pole-7.
            float safeX = bridgeStop.x - 4f;
            Vector3 approach = new Vector3(safeX, 0, 24f);
            Vector3 bypass = new Vector3(safeX, 0, -20f);
            Vector3 detour = new Vector3(18f, 0, -35f);
            Vector3 ridgeExit = new Vector3(cartStart.x, 0, 50f);
            drivePath = trip == 1 ? new[] { bypass, detour, baseStop } :
                trip == 2 ? (ridgeLanding ? new[] { detour, bypass, bridgeStop, approach, ridgeExit, cartStart } :
                    new[] { detour, bypass, bridgeStop, approach, cartStart }) :
                (ridgeLanding ? new[] { ridgeExit, approach, bridgeStop } : new[] { approach, bridgeStop });
            Physics.SyncTransforms();
            var planned = routePlanner.Plan(cartRoot.transform.position, drivePath);
            if (planned == null)
            {
                routeNotice = "Route obstructed. Cart remains parked; use the switch to retry.";
                pendingDrive = null;
                Debug.LogWarning("Moon route could not find a clear corridor for trip " + trip + " from " + cartRoot.transform.position);
                return;
            }
            drivePath = planned;
            routeNotice = null;
            if (body != null) body.enabled = false;
            destinationName = trip == 0 ? "EN ROUTE / SURVEY BAY" : trip == 1 ? "EN ROUTE / POLE-7" : "RETURN / LANDING PAD";
            cartMotor.Begin(drivePath);
            driving = true;
        }

        private void UpdateCartTrip()
        {
            if (cartMotor == null || cartRoot == null || playerObject == null) { driving = false; return; }
            Vector3 oldForward = cartMotor.Forward;
            cartMotor.Tick(Time.deltaTime);
            playerObject.transform.Rotate(0, Vector3.SignedAngle(oldForward, cartMotor.Forward, Vector3.up), 0, Space.World);
            playerObject.transform.position = cartRoot.transform.TransformPoint(new Vector3(0, .35f, -.3f));
            if (!cartMotor.Moving)
            {
                driving = false;
                Physics.SyncTransforms();
                playerObject.transform.position = FindDismountPosition();
                player.ResetVerticalMotion();
                var body = playerObject.GetComponent<CharacterController>();
                if (body != null) body.enabled = true;
                string arrived = pendingDrive; pendingDrive = null;
                if (arrived != null) connector.Interact(arrived);
                Physics.SyncTransforms();
            }
        }

        private Vector3 FindDismountPosition()
        {
            for (float radius = 2.4f; radius <= 5f; radius += .8f)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = Quaternion.Euler(0, 90 + i * 45, 0) * cartMotor.Forward * radius;
                    Vector3 point = cartRoot.transform.position + offset;
                    point.y = SurfaceHeight(point, terrainSurface) + .12f;
                    if (!Physics.CheckCapsule(point + Vector3.up * .35f, point + Vector3.up * 1.45f, .28f, ~(1 << TargetLayer), QueryTriggerInteraction.Ignore)) return point;
                }
            // Clear ground around every authored stop is checked by the route tests.
            return cartRoot.transform.position + Vector3.up * .4f;
        }

        private void OpenCargoDoors(bool open)
        {
            if (open == doorsOpen) return;
            doorsOpen = open;
            if (doorLeft != null)
            {
                doorLeft.transform.position = leftDoorClosed + (open ? Vector3.right * 1.7f : Vector3.zero);
                var collision = doorLeft.GetComponent<Collider>(); if (collision != null) collision.enabled = !open;
            }
            if (doorRight != null)
            {
                doorRight.transform.position = rightDoorClosed + (open ? Vector3.left * 1.7f : Vector3.zero);
                var collision = doorRight.GetComponent<Collider>(); if (collision != null) collision.enabled = !open;
            }
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
                case MoonInteractionIds.PickUpPanel: return "Pick up panel";
                case MoonInteractionIds.PickUpKit: return "Pick up kit";
                case MoonInteractionIds.LoadPanel: return "Load panel";
                case MoonInteractionIds.LoadKit: return "Load kit";
                case MoonInteractionIds.DriveBridge: return "Use cart switch to drive to bridge";
                case MoonInteractionIds.DriveBase: return "Use cart switch to drive to Pole-7";
                case MoonInteractionIds.DriveLander: return "Use cart switch to return to the landing pad";
                case MoonInteractionIds.InspectBridge: return "Inspect the closed bridge";
                case MoonInteractionIds.SurveyRoute: return "Survey the ledge and mark a safe route";
                case MoonInteractionIds.DeliverCargo: return "Deliver supplies to Pole-7";
                case MoonInteractionIds.IsolateCircuit: return "Isolate the damaged circuit";
                case MoonInteractionIds.RepairCircuit: return "Repair the circuit with the kit";
                case MoonInteractionIds.RepairLaser: return "or use the powered laser";
                case MoonInteractionIds.InstallPanel: return "Install the P-4 panel";
                case MoonInteractionIds.RestoreEssential: return "Restore essential power";
                case MoonInteractionIds.RestoreRelay: return "Restore the relay";
                case MoonInteractionIds.UploadRoute: return "Upload the route at the map console";
                case MoonInteractionIds.ReturnToShip: return "Return to the lander";
                default: return "Clean dust patches";
            }
        }
        public void Exit()
        {
            if (world == null) return;
            exiting = true;
            if (briefingPanel != null) { briefingPanel.Cancel(); briefingPanel = null; }
            if (sharedManager != null && sharedTaskActive) sharedManager.CancelActive("Moon surface closed.");
            sharedTaskActive = false; sharedManager = null;
            if (repairPanel != null) { repairPanel.Cancel(); repairPanel = null; }
            if (player != null) { player.Dispose(); player = null; }
            driving = false; drivePath = null; cartRoot = null; playerObject = null; terrainSurface = null;
            cartMotor = null; routePlanner = null; pendingDrive = null;
            hudObject = null; completionPanel = null; activeStage = null;
            world.SetActive(false); Destroy(world); world = null;
            targets.Clear();
            if (markerMaterial != null) { Destroy(markerMaterial); markerMaterial = null; }
            if (ambientOverridden)
            { RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbientLight; ambientOverridden = false; }
            if (testOwner != null) { Destroy(testOwner); testOwner = null; }
            if (hiddenCanvas != null) { hiddenCanvas.SetActive(true); hiddenCanvas = null; }
            foreach(var listener in hiddenListeners)if(listener!=null)listener.enabled=true;
            hiddenListeners.Clear();
        }
        private void OnDestroy() { Exit(); }
        private void OnApplicationFocus(bool focused) { if (!focused && player != null) player.ReleaseHold(); }
        internal static bool IsPickup(string id) { return id == MoonInteractionIds.PickUpPanel || id == MoonInteractionIds.PickUpKit; }
    }

    // This runtime-only helper is compiled with the scene controller, and does not
    // require a separate MonoBehaviour asset or serialize a second player script.
    internal sealed class MoonFirstPersonRig : IDisposable
    {
        private readonly CharacterController character;
        private readonly Camera viewCamera;
        private readonly MoonSceneConnector connector;
        private readonly LayerMask interactionLayers;
        private readonly Action<string> interact;
        private readonly float gravity, jumpHeight;
        private float verticalSpeed, pitch;
        private MoonWorldTarget focused, held;
        public MoonWorldTarget FocusedTarget { get { return focused; } }

        public MoonFirstPersonRig(CharacterController character, Camera camera, MoonSceneConnector connector, LayerMask layers, Action<string> interact,
            float gravity = 1.62f, float jumpHeight = 1.1f)
        {
            this.character = character; viewCamera = camera; this.connector = connector; interactionLayers = layers;
            this.interact = interact;
            this.gravity = Mathf.Max(.01f, gravity); this.jumpHeight = Mathf.Max(0, jumpHeight);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        public void Tick()
        {
            if (viewCamera == null || connector == null || character == null) return;
            if (!UpdateLook()) return;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            Vector2 input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            if (character.isGrounded)
            {
                if (verticalSpeed < 0) verticalSpeed = -.5f;
                if (keyboard.spaceKey.wasPressedThisFrame) verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            verticalSpeed -= gravity * Time.deltaTime;
            var body = character.transform;
            character.Move((body.right * input.x * 4f + body.forward * input.y * 4f + Vector3.up * verticalSpeed) * Time.deltaTime);
            RaycastHit hit;
            focused = Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out hit,
                3f, interactionLayers, QueryTriggerInteraction.Collide)
                ? hit.collider.GetComponentInParent<MoonWorldTarget>() : null;
            bool useHeld = (mouse != null && mouse.leftButton.isPressed) || keyboard.eKey.isPressed;
            if (held != null && (focused != held || !useHeld)) ReleaseHold();
            string action = focused == null ? null : focused.AvailableId(connector);
            if (action == null) return;
            if (MoonPlayableExperience.IsPickup(action))
            {
                if (mouse != null && mouse.rightButton.wasPressedThisFrame) interact(action);
            }
            else if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || keyboard.eKey.wasPressedThisFrame) interact(action);
        }

        public void ResetVerticalMotion() { verticalSpeed = -.5f; }

        public void TickLookOnly()
        {
            if (viewCamera == null || character == null) return;
            ReleaseHold();
            focused = null;
            UpdateLook();
        }

        private bool UpdateLook()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null) return false;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { ReleaseHold(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return false;
            }
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * (.1f*SagittariusA.Gameplay.Flow.ReleaseServices.Sensitivity);
                character.transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            return true;
        }
        public void ReleaseHold() { if (held != null && connector != null) connector.EndHold(); held = null; }
        public void Dispose() { ReleaseHold(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; focused = null; }
    }
}
