using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Ganymede;
using SagittariusA.Minigames;
using SagittariusA.Minigames.CampaignTasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.Mars
{
    // Prefab-free prototype presentation. Every authored-looking object is
    // explicitly labelled as blockout geometry so final Mars art can replace it.
    public sealed partial class MarsPlayableExperience : MonoBehaviour
    {
        private const int TargetLayer = 30;
        [SerializeField] private bool autoStartIsolated = false;
        private MarsStageController stage;
        private GameObject owner, world, canvasObject, activePanel, playerObject;
        private Camera viewCamera;
        private MarsFirstPersonRig player;
        private Text objective, prompt, notice, transitStatus, transitDetail;
        private RectTransform antennaNeedle, antennaTargetBand;
        private readonly List<MarsWorldTarget> targets = new List<MarsWorldTarget>();
        private readonly List<Material> materials = new List<Material>();
        private GameObject sensorVisual, lightAntennaVisual, directionalAntennaVisual, basicShieldVisual, reinforcedShieldVisual;
        private GameObject powerCable, dataCable, loader, orbiterAssembly, leftClampVisual, rightClampVisual;
        private Transform loaderStart, loaderEnd;
        private GameObject airlockLeft, airlockRight;
        private Vector3 airlockLeftClosed, airlockRightClosed;
        private bool airlockOpen, sendingPacket, spareConfirm, minigameOpen;
        private MarsPhase renderedPhase = (MarsPhase)(-1);
        private MarsWorldTarget.PanelKind openPanelKind;
        private string localNotice;
        private bool renderSettingsSaved, previousFog;
        private bool isolated;
        private GameObject ownedEvents, transitCamera;
        private readonly List<Behaviour> hiddenPresentation = new List<Behaviour>();
        private Color previousAmbient, previousFogColor;
        private float previousFogDensity;
        private FogMode previousFogMode;
        private UnityEngine.Rendering.AmbientMode previousAmbientMode;

        private static readonly Color Navy = new Color(.025f, .045f, .075f, .97f);
        private static readonly Color Card = new Color(.07f, .10f, .14f, .98f);
        private static readonly Color Orange = new Color(1f, .42f, .08f);
        private static readonly Color Cyan = new Color(.15f, .75f, .92f);
        private static readonly Color Green = new Color(.22f, .78f, .43f);
        private static readonly Color Red = new Color(.9f, .2f, .16f);

        private void Start() { if (autoStartIsolated) StartIsolatedTest(); }

        public void StartCampaign(bool isolatedRun = false)
        {
            var value = GetComponent<MarsStageController>();
            if (value == null || !value.Initialized) { Debug.LogWarning("Mars handoff is not ready."); return; }
            isolated = isolatedRun; Begin(value);
        }

        public void StartIsolatedTest()
        {
            if (stage != null) return;
            owner = new GameObject("Mars isolated in-memory state");
            var controller = owner.AddComponent<MarsStageController>();
            var build = new EarthBuild(); build.cargo.Add("spares");
            controller.Initialize(new MoonMarsHandoff {
                sourceRunId = "isolated-moon-run", missionId = "pole-7", nextMissionId = "mars-staging",
                commanderId = "commander_test", playerName = "Нара", build = build,
                remainingFuelUnits = 5, batteryKwh = 6, supplyKw = 5,
                routeUploaded = true, relayRestored = true, beaconMarked = true
            }, new MemoryMarsDraftStore());
            isolated = true; Begin(controller);
        }

        private void Begin(MarsStageController controller)
        {
            if (stage != null) return;
            stage = controller;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) if (camera.enabled) { hiddenPresentation.Add(camera); camera.enabled = false; }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) if (listener.enabled) { hiddenPresentation.Add(listener); listener.enabled = false; }
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (canvas.enabled) { hiddenPresentation.Add(canvas); canvas.enabled = false; }
            if (EventSystem.current == null)
            {
                ownedEvents = new GameObject("Mars input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEvents.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            transitCamera = new GameObject("Mars transit background", typeof(Camera), typeof(AudioListener));
            var background = transitCamera.GetComponent<Camera>(); background.clearFlags = CameraClearFlags.SolidColor; background.backgroundColor = Navy; background.cullingMask = 0; background.depth = -10;
            BuildMarsTransitCabin();
            CreateScreenCanvas();
            RenderForPhase(true);
        }

        private void Update()
        {
            if(SagittariusA.Gameplay.Flow.ReleaseServices.Modal)return;
            if (stage == null || !stage.Initialized) return;
            var keys=Keyboard.current;
            if(!minigameOpen&&keys!=null)
            {
                if(keys.tabKey.wasPressedThisFrame&&!loaderMoving){Exit();return;}
                if(keys.escapeKey.wasPressedThisFrame&&activePanel!=null&&stage.Phase==MarsPhase.MarsSurface){ClosePanel(true);return;}
                if(keys.hKey.wasPressedThisFrame&&stage.Phase==MarsPhase.MarsSurface&&!loaderMoving){ShowMarsHelp();return;}
            }
            RenderForPhase(false);
            if (minigameOpen) return;
            var context = stage.Context;
            if (context.phase == MarsPhase.AntennaRecovery && !sendingPacket)
            {
                var keyboard = Keyboard.current;
                float direction = keyboard == null ? 0 : (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0) -
                    (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0);
                if (direction != 0 || Mathf.Abs(context.antennaAngle - context.antennaTarget) <= 5f)
                    stage.TickManualAlignment(direction, Time.unscaledDeltaTime);
                UpdateTransitReadout();
            }
            if (context.phase == MarsPhase.MarsSurface && world != null)
            {
                if (player != null && activePanel == null) player.Tick();
                UpdateWorldHud(); UpdateWorldVisuals();
            }
            if (context.phase == MarsPhase.Complete && activePanel == null) ShowCompletion();
        }

        private void RenderForPhase(bool force)
        {
            MarsPhase phase = stage.Phase;
            if (!force && phase == renderedPhase) return;
            renderedPhase = phase;
            if (phase == MarsPhase.AntennaRecovery || phase == MarsPhase.ArrivalPackets) ShowTransit();
            else if (phase == MarsPhase.MarsSurface)
            {
                ClearScreen();
                var transitListener = transitCamera == null ? null : transitCamera.GetComponent<AudioListener>();
                if (transitListener != null) transitListener.enabled = false;
                if (world == null) BuildMarsWorld();
                if(transitWorld!=null)transitWorld.SetActive(false);
            }
            else if (phase == MarsPhase.Complete) ShowCompletion();
        }

        private void CreateScreenCanvas()
        {
            canvasObject = new GameObject("Mars UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        }

        private void ShowTransit()
        {
            ClearScreen();
            var transitListener = transitCamera == null ? null : transitCamera.GetComponent<AudioListener>();
            if (transitListener != null) transitListener.enabled = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var panel = Box(canvasObject.transform, "Moon-to-Mars transit", .15f, .10f, .70f, .80f, Navy);
            Label(panel, "MOON → MARS TRANSIT", .06f, .87f, .88f, .08f, 38, Color.white);
            Label(panel, "FIRST TEST SIGNAL  /  Keep the crew connected on the way to Mars", .06f, .81f, .88f, .05f, 17, new Color(.65f, .72f, .8f));
            transitStatus = Label(panel, "", .06f, .67f, .88f, .12f, 25, Color.white);
            transitDetail = Label(panel, "", .06f, .14f, .88f, .18f, 19, Color.white);
            if (stage.Phase == MarsPhase.AntennaRecovery) BuildAntennaPanel(panel); else BuildPacketPanel(panel);
            UpdateTransitReadout();
        }

        private void BuildAntennaPanel(RectTransform panel)
        {
            Label(panel, "A / D or ← / → adjusts one axis. Hold inside the target band for 1 second.", .06f, .60f, .88f, .06f, 20, Color.white);
            var track = Box(panel, "Signal track", .08f, .49f, .84f, .07f, new Color(.12f, .15f, .18f));
            antennaTargetBand = Box(track, "Target band", .61f, .05f, .06f, .90f, new Color(.12f, .75f, .40f, .65f));
            antennaNeedle = Box(track, "Indicator", .25f, -.25f, .012f, 1.5f, Orange);
            ActionButton(panel, "← ADJUST", .06f, .36f, .14f, .08f, () => stage.TickManualAlignment(-1,.15f), Orange);
            ActionButton(panel, "ADJUST →", .22f, .36f, .14f, .08f, () => stage.TickManualAlignment(1,.15f), Orange);
            ActionButton(panel, "CHECK", .39f, .36f, .16f, .08f, () => { Act(MarsActionKind.RecordAntennaMiss); ShowTransit(); });
            if (stage.Context.showTarget) Label(panel, "ASSISTANCE: target band shown", .06f, .27f, .60f, .06f, 18, Green);
            bool hasSpare = stage.Context.inventory != null && stage.Context.inventory.cargo.Contains("spares");
            if (hasSpare)
                ActionButton(panel, spareConfirm ? "CONFIRM: USE 1 SPARES BOX" : "USE SPARES FOR AUTO-TRACK", .66f, .36f, .28f, .08f, () => {
                    if (!spareConfirm) { spareConfirm = true; localNotice = "Confirm spending one carried spares box. Manual alignment needs no spare."; ShowTransit(); }
                    else { spareConfirm = false; Act(MarsActionKind.UseSpare); RenderForPhase(true); }
                }, spareConfirm ? Red : Card);
        }

        private void BuildPacketPanel(RectTransform panel)
        {
            var context = stage.Context;
            Label(panel, "CONTACT WINDOW", .06f, .59f, .25f, .06f, 22, Color.white);
            Label(panel, context.contactSecondsLeft.ToString("0.#") + " / 8 seconds remain", .31f, .59f, .30f, .06f, 22, context.contactSecondsLeft > 0 ? Green : Red);
            ActionButton(panel, "ARRIVAL FIRST · 3 s", .06f, .48f, .29f, .08f, () => {Act(MarsActionKind.PrioritizeArrival);ShowTransit();}, Orange).interactable=!sendingPacket&&!context.completedPackets.Contains("arrival-update");
            if (context.routeUploaded) ActionButton(panel, "QUEUE POLE-7 ARCHIVE · 7 s", .37f, .48f, .35f, .08f, () => { Act(MarsActionKind.QueueArchive); ShowTransit(); });
            ActionButton(panel, sendingPacket ? "TRANSMITTING…" : "SEND NEXT", .74f, .48f, .20f, .08f, BeginSend, sendingPacket ? new Color(.2f,.2f,.2f) : Orange).interactable = !sendingPacket && context.contactSecondsLeft > 0;
            if (context.contactSecondsLeft <= 0)
                ActionButton(panel, "NEXT CONTACT WINDOW", .06f, .37f, .30f, .08f, () => { Act(MarsActionKind.StartContactWindow); ShowTransit(); });
            if (context.completedPackets.Contains("arrival-update"))
                ActionButton(panel, "LAND ON MARS", .64f, .36f, .30f, .09f, () => { ReleaseServices.Film("MarsLanding",()=>{ Act(MarsActionKind.Land); RenderForPhase(true); }); }, Green).interactable=!sendingPacket;
        }

        private void BeginSend()
        {
            var context = stage.Context;
            if (sendingPacket || context.packetQueue.Length == 0) { localNotice = "Queue a packet first."; UpdateTransitReadout(); return; }
            StartCoroutine(SendPacketRoutine(context.packetQueue[0], Mathf.Min(MarsMission.PacketSeconds(context.packetQueue[0]), context.contactSecondsLeft)));
        }

        private IEnumerator SendPacketRoutine(string id, float seconds)
        {
            sendingPacket = true; float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                localNotice = "Sending " + MarsMission.PacketLabel(id) + " · " + Mathf.Max(0, seconds - elapsed).ToString("0.0") + " s";
                UpdateTransitReadout(); yield return null;
            }
            sendingPacket = false; Act(MarsActionKind.SendNextPacket); ShowTransit();
        }

        private void UpdateTransitReadout()
        {
            if (transitStatus == null || stage == null) return;
            var c = stage.Context;
            if (c.phase == MarsPhase.AntennaRecovery)
            {
                float strength = Mathf.Clamp01(1f - Mathf.Abs(c.antennaAngle - c.antennaTarget) / 90f);
                transitStatus.text = "AUTOMATIC TRACKING OFF\nDirection " + c.antennaAngle.ToString("0") + "° · Signal " + Mathf.RoundToInt(strength * 100) + "% · Lock " + c.antennaLockSeconds.ToString("0.0") + " / 1.0 s";
                if (antennaNeedle != null)
                {
                    float x = Mathf.InverseLerp(-90, 90, c.antennaAngle);
                    antennaNeedle.anchorMin = new Vector2(x, -.25f); antennaNeedle.anchorMax = new Vector2(x + .012f, 1.25f);
                }
                if (antennaTargetBand != null) antennaTargetBand.gameObject.SetActive(c.showTarget);
            }
            else
            {
                transitStatus.text = "MARS LINK LOCKED · " + (c.antennaRecovery == "manual" ? "manual alignment" : "automatic recovery with one spare") +
                    "\nQueued: " + (c.packetQueue.Length == 0 ? "none" : string.Join(" → ", c.packetQueue.Select(MarsMission.PacketLabel))) +
                    "\nDelivered: " + (c.completedPackets.Length == 0 ? "none" : string.Join(", ", c.completedPackets.Select(MarsMission.PacketLabel)));
            }
            if (transitDetail != null) transitDetail.text = (string.IsNullOrEmpty(localNotice) ? stage.Notice : localNotice) +
                (c.phase==MarsPhase.AntennaRecovery?"\n\nSam: “The antenna stopped tracking. Move it toward the stronger signal and hold there.”":"\n\nSam: “That's Mars. Let's tell them when we'll arrive.”");
        }

        private void BuildMarsWorld()
        {
            world = new GameObject("Mars playable blockout"); world.transform.SetParent(transform, false);
            renderSettingsSaved = true; previousAmbientMode = RenderSettings.ambientMode; previousAmbient = RenderSettings.ambientLight;
            previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor; previousFogDensity = RenderSettings.fogDensity; previousFogMode = RenderSettings.fogMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .25f, .16f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.42f,.18f,.11f); RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .008f;
            var sun = new GameObject("Mars warm key light", typeof(Light)); sun.transform.SetParent(world.transform); sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            var light = sun.GetComponent<Light>(); light.type = LightType.Directional; light.color = new Color(1f, .62f, .42f); light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            if (BuildAuthoredMarsWorld()) { CreatePlayer(); CreateWorldHud(); UpdateWorldVisuals(); return; }
            CreateCube("Mars ground · REPLACE WITH FINAL TERRAIN", new Vector3(0,-.5f,8), new Vector3(70,1,90), new Color(.38f,.13f,.07f));
            CreateCube("Landing pad · REPLACE WITH FINAL ART", new Vector3(0,.05f,-27), new Vector3(18,.2f,14), new Color(.19f,.2f,.22f));
            CreateCube("Marked approach", new Vector3(0,.08f,-12), new Vector3(4,.12f,17), new Color(.58f,.28f,.10f));
            for (int i=0;i<6;i++) { CreateCube("Guidance post L",new Vector3(-2.8f,.6f,-20+i*4),new Vector3(.18f,1.2f,.18f),Orange); CreateCube("Guidance post R",new Vector3(2.8f,.6f,-20+i*4),new Vector3(.18f,1.2f,.18f),Orange); }
            BuildAirlockAndBay(); BuildOrbiterBench(); BuildLoaderArea(); BuildOptionalStations(); CreateWorldLabels(); CreatePlayer(); CreateWorldHud();
            UpdateWorldVisuals();
        }

        private void BuildAirlockAndBay()
        {
            CreateCube("Habitat wall",new Vector3(-7,3,1),new Vector3(10,6,1),new Color(.28f,.30f,.31f));
            CreateCube("Habitat wall",new Vector3(7,3,1),new Vector3(10,6,1),new Color(.28f,.30f,.31f));
            airlockLeft=CreateCube("Airlock door left",new Vector3(-1.25f,2,1),new Vector3(2.4f,4,.4f),new Color(.18f,.35f,.40f));
            airlockRight=CreateCube("Airlock door right",new Vector3(1.25f,2,1),new Vector3(2.4f,4,.4f),new Color(.18f,.35f,.40f));
            airlockLeftClosed=airlockLeft.transform.position; airlockRightClosed=airlockRight.transform.position;
            CreateTarget(MarsWorldTarget.PanelKind.Airlock,"OPERATE AIRLOCK",new Vector3(0,1.5f,-.1f),new Vector3(3,3,.6f));
            CreateCube("Assembly bay floor",new Vector3(0,.03f,18),new Vector3(30,.15f,32),new Color(.16f,.17f,.18f));
            CreateCube("Bay left wall",new Vector3(-15,3,18),new Vector3(.5f,6,32),new Color(.24f,.24f,.25f));
            CreateCube("Bay right wall",new Vector3(15,3,18),new Vector3(.5f,6,32),new Color(.24f,.24f,.25f));
            CreateCube("Bay rear wall",new Vector3(0,3,34),new Vector3(30,6,.5f),new Color(.24f,.24f,.25f));
        }

        private void BuildOrbiterBench()
        {
            CreateCube("Assembly bench",new Vector3(-5,1,15),new Vector3(9,2,5),new Color(.22f,.24f,.26f));
            orbiterAssembly=new GameObject("Jupiter orbiter assembly"); orbiterAssembly.transform.SetParent(world.transform); orbiterAssembly.transform.position=new Vector3(-5,2.5f,15);
            CreateCube("Common frame + sensor bus 210 kg",Vector3.zero,new Vector3(4,.45f,2.3f),new Color(.5f,.52f,.55f),orbiterAssembly.transform);
            sensorVisual=CreateCube("Charged-particle sensor",new Vector3(-1.1f,.6f,0),new Vector3(1.2f,.8f,1.2f),Cyan,orbiterAssembly.transform);
            lightAntennaVisual=CreatePrimitive("Light antenna 20 kg",PrimitiveType.Cylinder,new Vector3(.8f,.75f,0),new Vector3(.18f,.75f,.18f),Orange,orbiterAssembly.transform);
            directionalAntennaVisual=CreatePrimitive("Directional antenna 45 kg",PrimitiveType.Cylinder,new Vector3(.8f,.85f,0),new Vector3(.9f,.12f,.9f),Orange,orbiterAssembly.transform);
            basicShieldVisual=CreateCube("Basic shield 20 kg",new Vector3(0,-.45f,0),new Vector3(4.2f,.12f,2.5f),new Color(.55f,.58f,.62f),orbiterAssembly.transform);
            reinforcedShieldVisual=CreateCube("Reinforced shield 50 kg",new Vector3(0,-.55f,0),new Vector3(4.4f,.32f,2.7f),new Color(.7f,.72f,.75f),orbiterAssembly.transform);
            powerCable=CreateCube("POWER cable · round port",new Vector3(-2.2f,.2f,.7f),new Vector3(1.5f,.09f,.09f),Orange,orbiterAssembly.transform);
            dataCable=CreateCube("DATA cable · square port",new Vector3(-2.2f,.05f,-.7f),new Vector3(1.5f,.09f,.09f),Cyan,orbiterAssembly.transform);
            CreateTarget(MarsWorldTarget.PanelKind.Assembly,"CONFIGURE / ASSEMBLE ORBITER",new Vector3(-5,2,11.9f),new Vector3(8,3,.8f));
            CreateCube("Bench test console",new Vector3(5,1.3f,15),new Vector3(5,2.6f,3),new Color(.08f,.16f,.2f));
            CreateTarget(MarsWorldTarget.PanelKind.Bench,"OPEN BENCH TEST",new Vector3(5,1.8f,13.3f),new Vector3(4,3,.8f));
        }

        private void BuildLoaderArea()
        {
            loaderStart=new GameObject("Loader lane start").transform; loaderStart.SetParent(world.transform); loaderStart.position=new Vector3(-5,.5f,23);
            loaderEnd=new GameObject("Loader lane cradle end").transform; loaderEnd.SetParent(world.transform); loaderEnd.position=new Vector3(6,.5f,29);
            CreateCube("Marked loader lane",new Vector3(.5f,.09f,26),new Vector3(16,.08f,3),new Color(.62f,.34f,.08f));
            loader=CreateCube("Fixed-path loader",loaderStart.position,new Vector3(5,1,3),new Color(.36f,.30f,.18f));
            CreateCube("Ship orbiter cradle · 300 KG",new Vector3(7,.9f,30),new Vector3(7,1.8f,5),new Color(.18f,.21f,.24f));
            leftClampVisual=CreateCube("LEFT CLAMP",new Vector3(4.5f,2,30),new Vector3(.5f,2.5f,3),Orange);
            rightClampVisual=CreateCube("RIGHT CLAMP",new Vector3(9.5f,2,30),new Vector3(.5f,2.5f,3),Orange);
            CreateTarget(MarsWorldTarget.PanelKind.Loader,"OPEN LOADER / CRADLE",new Vector3(7,2,26.9f),new Vector3(7,3,.8f));
        }

        private void BuildOptionalStations()
        {
            CreateCube("Weather mast",new Vector3(-11,2,7),new Vector3(.4f,4,.4f),new Color(.55f,.58f,.6f));
            CreateTarget(MarsWorldTarget.PanelKind.Weather,"OPTIONAL: DUST DIARY",new Vector3(-11,1.5f,5.8f),new Vector3(3,3,.8f));
            CreateCube("Packet practice console",new Vector3(10,1.2f,15),new Vector3(4,2.4f,3),new Color(.1f,.18f,.24f));
            CreateTarget(MarsWorldTarget.PanelKind.Practice,"OPTIONAL: WHAT GOES FIRST?",new Vector3(10,1.8f,13.3f),new Vector3(4,3,.8f));
        }

        private void CreateWorldLabels()
        {
            WorldLabel("LANDING PAD",new Vector3(0,2,-27),34,Color.white);
            WorldLabel("AIRLOCK · OPERATING",new Vector3(0,5.2f,.4f),34,Color.white);
            WorldLabel("ASSEMBLY BAY · BLOCKOUT ART",new Vector3(0,5.3f,8),30,Color.white);
            WorldLabel("WEATHER MAST",new Vector3(-11,4.6f,7),20,Color.white);
            WorldLabel("LOADER LANE → SHIP CRADLE",new Vector3(1,3.7f,28),22,Color.white);
        }

        private void CreatePlayer()
        {
            playerObject=new GameObject("Mars player",typeof(CharacterController)); playerObject.transform.SetParent(world.transform); playerObject.transform.position=new Vector3(0,1.1f,-25);
            var controller=playerObject.GetComponent<CharacterController>(); controller.height=1.8f; controller.radius=.35f; controller.center=new Vector3(0,.9f,0);
            var cameraObject=new GameObject("Mars player camera",typeof(Camera),typeof(AudioListener)); cameraObject.transform.SetParent(playerObject.transform); cameraObject.transform.localPosition=new Vector3(0,1.65f,0);
            viewCamera=cameraObject.GetComponent<Camera>(); viewCamera.nearClipPlane=.05f;
            viewCamera.farClipPlane=750;viewCamera.clearFlags=CameraClearFlags.SolidColor;viewCamera.backgroundColor=new Color(.32f,.18f,.12f);
            controller.stepOffset=.3f;controller.slopeLimit=45;
            if(environment!=null&&environment.Loaded)
            {
                playerObject.transform.position=environment.Spawn;
                var heading=environment.Point(4,-6,0)-playerObject.transform.position;heading.y=0;
                playerObject.transform.rotation=Quaternion.LookRotation(heading);
            }
            player=new MarsFirstPersonRig(controller,viewCamera,LayerMask.GetMask("Default") | (1<<TargetLayer),OpenTarget,3.71f,1.05f);
        }

        private void CreateWorldHud()
        {
            objective=Label(canvasObject.transform,"",.025f,.88f,.88f,.08f,26,Color.white);
            prompt=Label(canvasObject.transform,"+",.25f,.43f,.5f,.08f,24,Color.white); prompt.alignment=TextAnchor.MiddleCenter;
            notice=Label(canvasObject.transform,"",.025f,.03f,.95f,.09f,18,Color.white);
        }

        private void UpdateWorldHud()
        {
            if (objective == null) return;
            var c=stage.Context;
            objective.text="MARS STAGING · " + (c.testPassed ? c.orbiterSeated ? c.leftClamp&&c.rightClamp ? c.planReceived ? "Ready for Ganymede" : "Receive deployment plan" : "Close both distinct clamps" : "Load tested orbiter" : c.sensorInstalled&&c.antennaInstalled&&c.shieldInstalled ? "Run Mars Bench Test" : "Build the Jupiter orbiter");
            var focus=player==null?null:player.FocusedTarget;
            prompt.text=focus==null?"+":(focus.IsPickup?"RIGHT CLICK · ":"LEFT CLICK · ")+focus.Prompt;
            notice.text=(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice)+"\nWASD move · Space jump · Left use · Right pick up · H help · Esc close · Tab menu";
        }

        private void UpdateWorldVisuals()
        {
            if (stage == null || world == null) return;
            var c=stage.Context;
            if(environment!=null&&environment.Loaded){UpdateAuthoredVisuals(c);return;}
            sensorVisual.SetActive(c.sensorInstalled); sensorVisual.transform.localScale=c.sensorMode=="frequent"?new Vector3(1.15f,.9f,1.15f):Vector3.one;
            lightAntennaVisual.SetActive(c.antennaInstalled&&c.antenna=="light"); directionalAntennaVisual.SetActive(c.antennaInstalled&&c.antenna=="directional");
            basicShieldVisual.SetActive(c.shieldInstalled&&c.shield=="basic"); reinforcedShieldVisual.SetActive(c.shieldInstalled&&c.shield=="reinforced");
            powerCable.SetActive(c.powerConnected); dataCable.SetActive(c.dataConnected);
            leftClampVisual.SetActive(c.leftClamp); rightClampVisual.SetActive(c.rightClamp);
            airlockLeft.transform.position=airlockLeftClosed+(airlockOpen?Vector3.left*2.2f:Vector3.zero);
            airlockRight.transform.position=airlockRightClosed+(airlockOpen?Vector3.right*2.2f:Vector3.zero);
        }

        private void OpenTarget(MarsWorldTarget target)
        {
            if (target == null) return;
            if(HandleAuthoredTarget(target))return;
            if (target.Panel == MarsWorldTarget.PanelKind.Airlock) { airlockOpen=!airlockOpen; localNotice=airlockOpen?"Airlock doors opening normally.":"Airlock doors closed."; return; }
            ShowTaskPanel(target.Panel);
        }

        private void ShowTaskPanel(MarsWorldTarget.PanelKind kind)
        {
            ClosePanel(false); openPanelKind=kind;
            player.Paused=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            var panel=Box(canvasObject.transform,kind+" panel",.12f,.08f,.76f,.84f,Navy); activePanel=panel.gameObject;
            ActionButton(panel,"X",.92f,.91f,.055f,.065f,()=>ClosePanel(true),Red);
            if (kind==MarsWorldTarget.PanelKind.Assembly && environment!=null&&environment.Loaded) BuildAuthoredAssemblyPanel(panel);
            else if (kind==MarsWorldTarget.PanelKind.Assembly) BuildAssemblyPanel(panel);
            else if (kind==MarsWorldTarget.PanelKind.Bench) BuildBenchPanel(panel);
            else if (kind==MarsWorldTarget.PanelKind.Loader) BuildLoaderPanel(panel);
            else if (kind==MarsWorldTarget.PanelKind.Weather) BuildWeatherPanel(panel);
            else if (kind==MarsWorldTarget.PanelKind.Practice) BuildPracticePanel(panel);
        }

        private void BuildAssemblyPanel(RectTransform panel)
        {
            var c=stage.Context;
            Label(panel,"ORBITER CONFIGURATION + SNAP ASSEMBLY",.05f,.88f,.84f,.07f,31,Color.white);
            Color massColor=c.withinCapacity?Green:Red;
            Label(panel,"LIVE TOTAL  "+c.massKg+" / 300 kg",.05f,.79f,.40f,.07f,29,massColor);
            Label(panel,c.withinCapacity?"Cradle capacity is separate from Earth launch totals.":"5 kg over the cradle limit. Switch to the light antenna or basic shield.",.45f,.78f,.47f,.09f,18,massColor);
            Label(panel,"SENSOR · 210 kg frame + sensor",.05f,.69f,.28f,.05f,19,Color.white);
            ActionButton(panel,"STANDARD · 6",.05f,.61f,.18f,.07f,()=>PanelAct(MarsActionKind.SetStandardSensor));
            ActionButton(panel,"FREQUENT · 12",.24f,.61f,.18f,.07f,()=>PanelAct(MarsActionKind.SetFrequentSensor));
            Label(panel,"ANTENNA",.45f,.69f,.18f,.05f,19,Color.white);
            ActionButton(panel,"LIGHT · 20 kg",.45f,.61f,.18f,.07f,()=>PanelAct(MarsActionKind.SelectLightAntenna));
            ActionButton(panel,"DIRECTIONAL · 45 kg",.64f,.61f,.28f,.07f,()=>PanelAct(MarsActionKind.SelectDirectionalAntenna));
            Label(panel,"SHIELD",.45f,.52f,.18f,.05f,19,Color.white);
            ActionButton(panel,"BASIC · 20 kg",.45f,.44f,.18f,.07f,()=>PanelAct(MarsActionKind.SelectBasicShield));
            ActionButton(panel,"REINFORCED · 50 kg",.64f,.44f,.28f,.07f,()=>PanelAct(MarsActionKind.SelectReinforcedShield));
            Label(panel,"1 · SELECT FROM SHELF",.05f,.48f,.34f,.05f,18,Cyan);
            ActionButton(panel,"SENSOR",.05f,.39f,.12f,.07f,()=>PanelAct(MarsActionKind.SelectSensorPart));
            ActionButton(panel,"ANTENNA",.18f,.39f,.14f,.07f,()=>PanelAct(MarsActionKind.SelectAntennaPart));
            ActionButton(panel,"SHIELD",.33f,.39f,.12f,.07f,()=>PanelAct(MarsActionKind.SelectShieldPart));
            Label(panel,"Selected: "+(c.selectedPart??"none"),.05f,.33f,.38f,.05f,18,Color.white);
            Label(panel,"2 · PLACE ON LABELLED KEYED MOUNT",.05f,.26f,.52f,.05f,18,Orange);
            ActionButton(panel,"SENSOR MOUNT "+(c.sensorInstalled?"✓":""),.05f,.16f,.25f,.08f,()=>PanelAct(MarsActionKind.PlaceSensorMount));
            ActionButton(panel,"ANTENNA MOUNT "+(c.antennaInstalled?"✓":""),.32f,.16f,.27f,.08f,()=>PanelAct(MarsActionKind.PlaceAntennaMount));
            ActionButton(panel,"SHIELD MOUNT "+(c.shieldInstalled?"✓":""),.61f,.16f,.25f,.08f,()=>PanelAct(MarsActionKind.PlaceShieldMount));
            Label(panel,(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice),.05f,.04f,.84f,.08f,17,Color.white);
        }

        private void BuildBenchPanel(RectTransform panel)
        {
            var c=stage.Context;
            Label(panel,"MARS BENCH TEST",.05f,.88f,.84f,.07f,32,Color.white);
            Label(panel,"The sensor takes the reading. POWER feeds it. DATA carries the reading to the computer.",.05f,.81f,.84f,.05f,18,Color.white);
            ActionButton(panel,"POWER · ROUND PORT "+(c.powerConnected?"CONNECTED":"OPEN"),.05f,.70f,.39f,.08f,()=>PanelAct(MarsActionKind.ConnectPower),c.powerConnected?Green:Card);
            ActionButton(panel,"DATA · SQUARE PORT "+(c.dataConnected?"CONNECTED":"OPEN"),.47f,.70f,.43f,.08f,()=>PanelAct(MarsActionKind.ConnectData),c.dataConnected?Cyan:Card);
            ActionButton(panel,"1 · POWER ON",.05f,.58f,.25f,.08f,()=>PanelAct(MarsActionKind.BenchPowerOn));
            ActionButton(panel,"2 · TAKE SAMPLE",.32f,.58f,.25f,.08f,()=>PanelAct(MarsActionKind.BenchSample));
            ActionButton(panel,"3 · TRANSMIT",.59f,.58f,.25f,.08f,()=>PanelAct(MarsActionKind.BenchTransmit),Orange);
            var graph=Box(panel,"Simulated graph",.08f,.17f,.82f,.34f,new Color(.025f,.04f,.055f));
            Label(graph,"Simulated particle count",.01f,.24f,.22f,.65f,15,Color.white);
            Label(graph,"Sample",.42f,.01f,.20f,.12f,15,Color.white);
            if (c.benchGraph!=null&&c.benchGraph.samples!=null&&c.benchGraph.samples.Length>0)
            {
                int max=Mathf.Max(1,c.benchGraph.samples.Max());
                for(int i=0;i<c.benchGraph.samples.Length;i++)
                { float w=.68f/c.benchGraph.samples.Length; float h=.7f*c.benchGraph.samples[i]/max; Box(graph,"Sample "+(i+1),.22f+i*w,.16f,w*.65f,h,Green); }
            }
            else Label(graph,"BLANK GRAPH\nRecovery: connect DATA, then power → sample → transmit.",.25f,.28f,.65f,.40f,19,Red);
            Label(panel,(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice),.05f,.05f,.86f,.09f,17,Color.white);
        }

        private void BuildLoaderPanel(RectTransform panel)
        {
            var c=stage.Context;
            Label(panel,"LOAD + LOCK",.05f,.88f,.84f,.07f,32,Color.white);
            Label(panel,"Exact tested orbiter: "+c.sensorMode+" sensor · "+c.antenna+" antenna · "+c.shield+" shield · "+c.massKg+" kg",.05f,.79f,.86f,.07f,20,c.withinCapacity?Color.white:Red);
            ActionButton(panel,c.orbiterSeated?"RETURN TO ASSEMBLY":"MOVE LOADER + SEAT",.05f,.66f,.33f,.09f,BeginLoading,Orange).interactable=!loaderMoving;
            ActionButton(panel,"LEFT CLAMP "+(c.leftClamp?"LOCKED":"OPEN"),.05f,.53f,.33f,.09f,()=>PanelAct(MarsActionKind.CloseLeftClamp),c.leftClamp?Green:Card);
            ActionButton(panel,"RIGHT CLAMP "+(c.rightClamp?"LOCKED":"OPEN"),.40f,.53f,.33f,.09f,()=>PanelAct(MarsActionKind.CloseRightClamp),c.rightClamp?Green:Card);
            ActionButton(panel,"RECEIVE DEPLOYMENT PLAN",.05f,.40f,.42f,.09f,()=>PanelAct(MarsActionKind.ReceiveDeploymentPlan));
            Label(panel,(c.deploymentPlan??"Plan not received.")+"\n"+(c.firstContactSchedule??"Schedule not received."),.05f,.24f,.86f,.13f,18,Color.white);
            ActionButton(panel,"CONTINUE TO GANYMEDE",.52f,.08f,.39f,.11f,()=>PanelAct(MarsActionKind.CompleteMars),c.canDepart?Green:new Color(.25f,.25f,.25f));
            Label(panel,(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice),.05f,.06f,.45f,.12f,17,Color.white);
        }

        private void BuildWeatherPanel(RectTransform panel)
        {
            var c=stage.Context;
            Label(panel,"OPTIONAL · DUST DIARY",.05f,.88f,.84f,.07f,32,Color.white);
            Label(panel,"Compare the authored ridge examples. Which time logged more airborne dust?\n\n09:00 · sharp ridge · Simulated dust index 2\n14:00 · hazier ridge · Simulated dust index 7",.07f,.49f,.82f,.31f,24,Color.white);
            ActionButton(panel,c.dustDiaryComplete?"DUST DIARY COMPLETE":"OPEN 2D DUST TASK",.25f,.34f,.50f,.10f,()=>StartMarsMinigame(MarsDustDiaryMinigame.IdValue),c.dustDiaryComplete?Green:Orange);
            Label(panel,c.dustDiaryComplete?"SAVED TO FLIGHT LOG · 14:00 / index 7":(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice),.08f,.16f,.78f,.10f,20,c.dustDiaryComplete?Green:Color.white);
        }

        private void BuildPracticePanel(RectTransform panel)
        {
            var c=stage.Context;
            Label(panel,"OPTIONAL · WHAT GOES FIRST? · 10-SECOND WINDOW",.05f,.88f,.86f,.07f,30,Color.white);
            Label(panel,"Pass requires HEALTH (2 s) and SMALL READING (4 s). FULL GRAPH takes 9 s.",.05f,.80f,.86f,.06f,19,Color.white);
            ActionButton(panel,"HEALTH · 2 s",.05f,.67f,.23f,.08f,()=>PanelAct(MarsActionKind.PracticeQueueHealth));
            ActionButton(panel,"SMALL READING · 4 s",.30f,.67f,.29f,.08f,()=>PanelAct(MarsActionKind.PracticeQueueSample));
            ActionButton(panel,"FULL GRAPH · 9 s",.61f,.67f,.25f,.08f,()=>PanelAct(MarsActionKind.PracticeQueueGraph));
            Label(panel,"ORDER: "+(c.practiceQueue.Length==0?"none":string.Join(" → ",c.practiceQueue)),.05f,.54f,.86f,.08f,21,Color.white);
            ActionButton(panel,"SEND PRACTICE QUEUE",.05f,.40f,.35f,.09f,()=>PanelAct(MarsActionKind.PracticeSend),Orange);
            ActionButton(panel,"CLEAR / RETRY",.43f,.40f,.25f,.09f,()=>PanelAct(MarsActionKind.PracticeClear));
            ActionButton(panel,c.practicePassed?"2D TASK COMPLETE":"OPEN 2D TASK",.70f,.40f,.20f,.09f,()=>StartMarsMinigame(MarsPacketPracticeMinigame.IdValue),c.practicePassed?Green:Orange);
            Label(panel,"DELIVERED: "+(c.practiceDelivered.Length==0?"none":string.Join(", ",c.practiceDelivered))+"\n"+(c.practicePassed?"PASS · editable Ganymede suggestion saved":(string.IsNullOrEmpty(localNotice)?stage.Notice:localNotice)),.05f,.18f,.86f,.16f,20,c.practicePassed?Green:Color.white);
        }

        private MarsActionResult PanelAct(MarsActionKind kind)
        { MarsActionResult result=Act(kind); if(stage.Phase==MarsPhase.MarsSurface)ShowTaskPanel(openPanelKind); else RenderForPhase(true); return result; }
        private MarsActionResult Act(MarsActionKind kind)
        { var result=stage.Act(kind); localNotice=result.message; return result; }

        private void StartMarsMinigame(string minigameId)
        {
            if (minigameOpen || stage == null || !stage.Initialized) return;
            minigameOpen = true;
            if (player != null) player.Paused = true;
            MinigameContext context = MarsMinigameCampaignAdapter.CreateContext(minigameId, stage.Context);
            MinigameManager.Instance.StartMinigame(context, result =>
            {
                minigameOpen = false;
                if (result != null && result.Outcome == MinigameOutcome.Success)
                {
                    MarsActionResult commit = MarsMinigameCampaignAdapter.Commit(stage, result);
                    localNotice = commit.message;
                    if (minigameId == MarsOrbiterBenchMinigame.IdValue && commit.Accepted && loader != null)
                        StartCoroutine(AnimateLoader());
                }
                else localNotice = result == null ? "Mars task closed without a result." : result.Summary;

                if (stage.Phase == MarsPhase.MarsSurface && activePanel != null) ShowTaskPanel(openPanelKind);
                else RenderForPhase(true);
            });
        }

        private IEnumerator AnimateLoader()
        {
            if(environment!=null&&environment.Loaded){yield return AnimateAuthoredLoader();yield break;}
            if(loader==null)yield break; Vector3 from=loaderStart.position,to=loaderEnd.position;
            for(float t=0;t<1;t+=Time.unscaledDeltaTime/2.5f){loader.transform.position=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t));yield return null;}
            loader.transform.position=to;
        }

        private void ShowCompletion()
        {
            if (stage == null || canvasObject == null || (activePanel!=null&&activePanel.name=="Mars completion")) return;
            ClosePanel(false); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            var handoff=stage.GetGanymedeHandoff();
            var panel=Box(canvasObject.transform,"Mars completion",.16f,.13f,.68f,.74f,Navy); activePanel=panel.gameObject;
            Label(panel,"MARS STAGING COMPLETE",.06f,.82f,.88f,.10f,38,Color.white);
            string text=handoff==null?"Handoff unavailable.":"Commander "+handoff.playerName+" assembled and tested the Jupiter orbiter.\n\n"+
                "Exact orbiter: "+handoff.sensorMode+" sensor / "+handoff.antenna+" antenna / "+handoff.shield+" shield / "+handoff.orbiterMassKg+" kg\n"+
                "Bench graph: "+handoff.benchGraph.samples.Length+" simulated samples received\nClamps: left + right locked\n"+
                "Dust Diary: "+(handoff.dustDiaryComplete?handoff.dustSelectedTime+" / index "+handoff.dustSelectedIndex:"skipped")+"\n"+
                "Packet practice: "+(handoff.practicePassed?"passing suggestion saved":"skipped")+"\n\nThe ascent capsule returns to the expedition transfer ship in Mars orbit. After docking, its long-range propulsion carries the crew and orbiter to Ganymede.";
            Label(panel,text,.06f,.25f,.88f,.53f,22,Color.white);
            ActionButton(panel,isolated?"EXIT TEST":"MAIN MENU",.06f,.08f,.35f,.11f,Exit);
            ActionButton(panel,"BEGIN GANYMEDE TRANSIT",.45f,.08f,.49f,.11f,StartGanymede,Orange).interactable = handoff != null;
        }

        private void StartGanymede()
        {
            var handoff = stage?.GetGanymedeHandoff(); if (handoff == null) return;
            bool test = isolated; Exit();
            var next = GetComponent<GanymedePlayableExperience>() ?? gameObject.AddComponent<GanymedePlayableExperience>();
            ReleaseServices.Film("MarsTakeoff",()=>next.Begin(handoff, test ? (IGanymedeStore)new MemoryGanymedeStore() : new PlayerPrefsGanymedeStore(), test));
        }

        private void ClosePanel(bool resume)
        {
            if(activePanel!=null){activePanel.SetActive(false);Destroy(activePanel);activePanel=null;}
            if(resume&&player!=null){player.Paused=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        private void ClearScreen()
        {
            if(canvasObject==null)return; for(int i=canvasObject.transform.childCount-1;i>=0;i--)Destroy(canvasObject.transform.GetChild(i).gameObject);
            activePanel=null; objective=prompt=notice=transitStatus=transitDetail=null; antennaNeedle=antennaTargetBand=null;
        }

        public void Exit()
        {
            StopAllCoroutines(); if(player!=null){player.Dispose();player=null;} Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(world!=null)Destroy(world); if(canvasObject!=null)Destroy(canvasObject); if(owner!=null)Destroy(owner);
            if(transitWorld!=null){transitWorld.SetActive(false);Destroy(transitWorld);transitWorld=null;}
            if(world!=null)world.SetActive(false); if(canvasObject!=null)canvasObject.SetActive(false);
            if(ownedEvents!=null){ownedEvents.SetActive(false);Destroy(ownedEvents);ownedEvents=null;}
            if(transitCamera!=null){transitCamera.SetActive(false);Destroy(transitCamera);transitCamera=null;}
            foreach(var item in hiddenPresentation)if(item!=null)item.enabled=true;hiddenPresentation.Clear();
            if(renderSettingsSaved){RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.fog=previousFog;RenderSettings.fogColor=previousFogColor;RenderSettings.fogDensity=previousFogDensity;RenderSettings.fogMode=previousFogMode;renderSettingsSaved=false;}
            foreach(var material in materials)if(material!=null)Destroy(material); materials.Clear(); stage=null; world=null; canvasObject=null;
            environment=null;heldPart=null;heldPartId=null;targets.Clear();renderedPhase=(MarsPhase)(-1);airlockOpen=false;loaderMoving=false;minigameOpen=false;sendingPacket=false;spareConfirm=false;localNotice=null;authoredRevision=-1;
        }
        private void OnDestroy(){Exit();}

        private MarsWorldTarget CreateTarget(MarsWorldTarget.PanelKind kind,string text,Vector3 position,Vector3 scale)
        {
            var go=CreateCube("Interaction · "+text,position,scale,new Color(Orange.r,Orange.g,Orange.b,.18f)); go.layer=TargetLayer;
            var target=go.AddComponent<MarsWorldTarget>();target.Configure(kind,text);targets.Add(target);return target;
        }
        private GameObject CreateCube(string name,Vector3 position,Vector3 scale,Color color,Transform parent=null)
        {return CreatePrimitive(name,PrimitiveType.Cube,position,scale,color,parent);}
        private GameObject CreatePrimitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent??world.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");var material=new Material(shader);material.color=color;materials.Add(material);go.GetComponent<Renderer>().material=material;return go;
        }
        private void WorldLabel(string text,Vector3 position,int size,Color color)
        {
            var go=new GameObject(text,typeof(TextMesh));go.transform.SetParent(world.transform);go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,180,0);
            var mesh=go.GetComponent<TextMesh>();mesh.text=text;mesh.fontSize=size;mesh.characterSize=.06f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
        }
        private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;
            rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(x+w,y+h);rect.offsetMin=rect.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return rect;
        }
        private static Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;
            rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(x+w,y+h);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var label=go.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.color=color;
            label.alignment=TextAnchor.MiddleLeft;label.supportRichText=false;label.raycastTarget=false;return label;
        }
        private static Button ActionButton(Transform parent,string text,float x,float y,float w,float h,Action action,Color? color=null)
        {
            var rect=Box(parent,text,x,y,w,h,color??Card);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>action());
            var label=Label(rect,text,.04f,.05f,.92f,.9f,18,Color.white);label.alignment=TextAnchor.MiddleCenter;return button;
        }
    }

    internal sealed class MarsFirstPersonRig : IDisposable
    {
        private readonly CharacterController character; private readonly Camera camera; private readonly LayerMask layers;
        private readonly Action<MarsWorldTarget> interact; private readonly float gravity,jumpHeight; private float vertical,pitch;
        public bool Paused { get; set; }
        public MarsWorldTarget FocusedTarget { get; private set; }
        public MarsFirstPersonRig(CharacterController value,Camera view,LayerMask mask,Action<MarsWorldTarget> action,float gravityValue,float jump)
        {character=value;camera=view;layers=mask;interact=action;gravity=gravityValue;jumpHeight=jump;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public void Tick()
        {
            if(Paused||character==null||camera==null)return;var keyboard=Keyboard.current;var mouse=Mouse.current;if(keyboard==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            if(Cursor.lockState!=CursorLockMode.Locked){if(mouse!=null&&mouse.leftButton.wasPressedThisFrame){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}return;}
            if(mouse!=null){Vector2 d=mouse.delta.ReadValue()*(.1f*SagittariusA.Gameplay.Flow.ReleaseServices.Sensitivity);character.transform.Rotate(0,d.x,0);pitch=Mathf.Clamp(pitch-d.y,-85,85);camera.transform.localRotation=Quaternion.Euler(pitch,0,0);}
            Vector2 input=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));input=Vector2.ClampMagnitude(input,1);
            if(character.isGrounded){if(vertical<0)vertical=-.5f;if(keyboard.spaceKey.wasPressedThisFrame)vertical=Mathf.Sqrt(2*gravity*jumpHeight);}vertical-=gravity*Time.deltaTime;
            character.Move((character.transform.right*input.x*4+character.transform.forward*input.y*4+Vector3.up*vertical)*Time.deltaTime);
            RaycastHit hit;FocusedTarget=Physics.Raycast(camera.transform.position,camera.transform.forward,out hit,3.5f,layers,QueryTriggerInteraction.Collide)?hit.collider.GetComponentInParent<MarsWorldTarget>():null;
            if(FocusedTarget!=null)
            {
                bool use=(mouse!=null&&mouse.leftButton.wasPressedThisFrame)||keyboard.eKey.wasPressedThisFrame;
                bool pickup=mouse!=null&&mouse.rightButton.wasPressedThisFrame;
                if(FocusedTarget.IsPickup?pickup:use)interact(FocusedTarget);
            }
        }
        public void Dispose(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;FocusedTarget=null;}
    }
}
