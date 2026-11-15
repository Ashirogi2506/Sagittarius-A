using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Mars;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.Ganymede
{
    public sealed class GanymedePlayableExperience : MonoBehaviour
    {
        [SerializeField] private bool autoStartIsolated;
        public GanymedeMission Mission { get; private set; }
        public Camera ViewCamera => view;
        public CharacterController Player => character;
        public bool Isolated { get; private set; }
        public GanymedeEnvironmentView Environment => environment;
        private GanymedeEnvironmentView environment;
        private Vector3 siteA=A,siteB=B,photoSpot=Overlook,spawnPoint=new Vector3(0,.15f,0);
        private string installingStation, selectedComponent="base";
        private GameObject root, surface, cabin, ui, panel, ownedEvents, instrument, ghost, orbiter;
        private GameObject leftClamp, rightClamp;
        private CharacterController character;
        private Camera view;
        private Text objective, interaction, status, panelStatus, progressText;
        private RectTransform bubble, needle, progressBar;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<AudioListener> hiddenListeners = new List<AudioListener>();
        private readonly List<Camera> hiddenCameras = new List<Camera>();
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<TextMesh> signs = new List<TextMesh>();
        private GanymedeWorldTarget focused;
        private GanymedePhase renderedPhase = (GanymedePhase)(-1);
        private int renderedRevision = -1;
        private string panelId, fieldLabelA = GanymedeMission.SiteA, fieldLabelB = GanymedeMission.SiteB;
        private bool photoLeft = true, photoRight = true, busy, exiting;
        private float pitch, vertical, nextInput;
        private bool priorFog, priorCursorVisible;
        private Color priorAmbient;
        private UnityEngine.Rendering.AmbientMode priorAmbientMode;
        private CursorLockMode priorCursor;
        private static readonly Color Navy = new Color(.025f,.043f,.065f,.98f), Card = new Color(.065f,.11f,.15f,.98f);
        private static readonly Color Cyan = new Color(.34f,.85f,.94f), Orange = new Color(1f,.47f,.12f), Muted = new Color(.69f,.79f,.84f);
        private static readonly Vector3 A = new Vector3(-12,0,17), B = new Vector3(13,0,28), Overlook = new Vector3(23,0,12);

        private void Start() { if (autoStartIsolated) StartIsolatedTest(); }
        public void StartIsolatedTest() => Begin(TestHandoff(), new MemoryGanymedeStore(), true);
        public void StartCampaign(MarsGanymedeHandoff handoff) => Begin(handoff, new PlayerPrefsGanymedeStore(), false);
        public bool TryResume()
        {
            var handoff = PlayerPrefsGanymedeStore.SavedHandoff();
            if (!GanymedeMission.ValidHandoff(handoff)) return false;
            StartCampaign(handoff); return Mission != null;
        }
        public static MarsGanymedeHandoff TestHandoff(string antenna = "light", string mode = "standard")
        {
            int[] samples = mode == "frequent" ? new[] { 12,18,16,22,25,20,17,24,29,21,18,23 } : new[] { 12,18,16,22,25,20 };
            return new MarsGanymedeHandoff { sourceRunId = "ganymede-isolated-test", missionId = "mars-staging", nextMissionId = "ganymede",
                commanderId = "commander_test", playerName = "Нара", build = new EarthBuild(), remainingFuelUnits = 5,
                batteryKwh = 6, supplyKw = 5, routeUploaded = true, relayRestored = true, antennaRecovery = "manual",
                sensorMode = mode, antenna = antenna, shield = "basic", orbiterMassKg = antenna == "light" ? 250 : 275,
                sampleCount = samples.Length, benchGraph = new MarsBenchGraph { samples = samples },
                orbiterSeated = true, leftClamp = true, rightClamp = true, deploymentPlanReceived = true,
                deploymentPlan = "Land on Ganymede → return to orbit → release at the marked point.", firstContactSchedule = "Health report, then small reading." };
        }

        public void Begin(MarsGanymedeHandoff handoff, IGanymedeStore store, bool isolated = false)
        {
            if (Mission != null) return;
            try { Mission = new GanymedeMission(handoff, store); }
            catch (ArgumentException error) { Debug.LogError(error.Message); return; }
            Isolated = isolated; exiting = false;siteA=A;siteB=B;photoSpot=Overlook;spawnPoint=new Vector3(0,.15f,0);
            priorFog = RenderSettings.fog; priorAmbient = RenderSettings.ambientLight; priorAmbientMode = RenderSettings.ambientMode;
            priorCursor = Cursor.lockState; priorCursorVisible = Cursor.visible;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) if (c.enabled) { hiddenCameras.Add(c); c.enabled = false; }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) if (listener.enabled) { hiddenListeners.Add(listener); listener.enabled=false; }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (c.enabled) { hiddenCanvases.Add(c); c.enabled = false; }
            RenderSettings.fog = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.24f,.31f,.4f);
            root = new GameObject("Ganymede playable chapter"); root.transform.SetParent(transform, false);
            BuildWorld(); BuildUI(); RenderPhase();
        }

        private void BuildUI()
        {
            if (EventSystem.current == null)
            {
                ownedEvents = new GameObject("Ganymede input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEvents.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            ui = new GameObject("Ganymede UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(root.transform, false); var canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = ui.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var top = Box(ui.transform,"Mission HUD",.025f,.86f,.68f,.115f,Navy);
            Label(top,"FAR SIGNAL  /  GANYMEDE",.025f,.67f,.95f,.25f,18,Cyan);
            objective = Label(top,"",.025f,.08f,.95f,.55f,26,Color.white);
            interaction = Label(ui.transform,"+",.20f,.39f,.60f,.12f,23,Color.white); interaction.alignment = TextAnchor.MiddleCenter;
            var bottom = Box(ui.transform,"Status",.025f,.025f,.95f,.11f,Navy);
            status = Label(bottom,"",.02f,.40f,.96f,.55f,20,Color.white);
            Label(bottom,"WASD move · Mouse look · Space jump · Left click / E use · Right click pick up / pack · H help · Esc cursor · Tab menu",.02f,.06f,.96f,.32f,17,Muted);
        }

        private void Update()
        {
            if(SagittariusA.Gameplay.Flow.ReleaseServices.Modal)return;
            if (Mission == null || exiting) return;
            var keys = Keyboard.current;
            if (keys != null && keys.tabKey.wasPressedThisFrame && !busy) { ReturnToMenu(); return; }
            if (keys != null && keys.hKey.wasPressedThisFrame && !busy) { OpenPanel("help"); return; }
            if (keys != null && keys.escapeKey.wasPressedThisFrame && !busy)
            {
                if (panel != null && Mission.State.phase == GanymedePhase.Surface) ClosePanel();
                else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            bool near = !string.IsNullOrEmpty(Mission.State.placedSite) && Vector3.Distance(character.transform.position, SitePosition(Mission.State.placedSite)) < 4.5f;
            Mission.Tick(Time.unscaledDeltaTime, near);
            if (Mission.State.phase != renderedPhase && !busy) RenderPhase();
            if (panelId == "survey" && keys != null && !Mission.State.instrumentLeveled)
            {
                float x = keys.leftArrowKey.wasPressedThisFrame ? -.1f : keys.rightArrowKey.wasPressedThisFrame ? .1f : 0;
                float y = keys.downArrowKey.wasPressedThisFrame ? -.1f : keys.upArrowKey.wasPressedThisFrame ? .1f : 0;
                if (x != 0 || y != 0) Mission.AdjustLevel(x,y);
            }
            if (panelId == "contact" && keys != null && !Mission.State.antennaLocked)
                Mission.AdjustAntenna(((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0)) * Time.unscaledDeltaTime * .6f);
            if (panel == null && Mission.State.phase == GanymedePhase.Surface && !busy) MovePlayer();
            if (Mission.State.revision != renderedRevision)
            {
                renderedRevision = Mission.State.revision; RefreshInstrument();
                if (panel != null && !busy) RefreshPanel();
            }
            UpdateReadouts();
            foreach (var sign in signs) if (sign != null && view != null) sign.transform.rotation = Quaternion.LookRotation(sign.transform.position - view.transform.position);
        }

        private void RenderPhase()
        {
            renderedPhase = Mission.State.phase;
            bool onSurface = renderedPhase == GanymedePhase.Surface;
            surface.SetActive(onSurface); cabin.SetActive(!onSurface); character.enabled = false;
            character.transform.position = onSurface ? spawnPoint : new Vector3(0,.15f,-3);
            character.transform.rotation = Quaternion.identity; pitch = vertical = 0; view.transform.localRotation = Quaternion.identity; character.enabled = onSurface;
            ClosePanel(); RefreshInstrument();
            if (onSurface) { LockMouse(); return; }
            OpenPanel(renderedPhase == GanymedePhase.Transit ? "transit" : renderedPhase == GanymedePhase.Orbit ? "deploy" : renderedPhase == GanymedePhase.Complete ? "log" : renderedPhase == GanymedePhase.Acknowledged ? "ack" : "contact");
        }

        private void MovePlayer()
        {
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) { LockMouse(); nextInput = Time.unscaledTime + .18f; }
                return;
            }
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * (.11f*SagittariusA.Gameplay.Flow.ReleaseServices.Sensitivity);
                character.transform.Rotate(0,delta.x,0); pitch = Mathf.Clamp(pitch - delta.y,-82,82);
                view.transform.localRotation = Quaternion.Euler(pitch,0,0);
            }
            float dt = Mathf.Min(Time.deltaTime,.05f);
            Vector2 axis = keys == null ? Vector2.zero : new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),(keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            if (character.isGrounded && vertical < 0) vertical = -1;
            if (keys != null && keys.spaceKey.wasPressedThisFrame && character.isGrounded) vertical = Mathf.Sqrt(2 * 1.428f * 1.4f);
            vertical -= 1.428f * dt;
            Vector3 velocity = character.transform.TransformDirection(new Vector3(axis.x,0,axis.y).normalized) * 4.2f + Vector3.up * vertical;
            character.Move(velocity * dt);
            if (character.transform.position.y < -8 || Mathf.Abs(character.transform.position.x) > 80 || Mathf.Abs(character.transform.position.z) > 80)
            { character.enabled = false; character.transform.position = spawnPoint; character.enabled = true; vertical = 0; }
            focused = null;
            if (Physics.Raycast(view.transform.position,view.transform.forward,out RaycastHit hit,4f,~0,QueryTriggerInteraction.Ignore)) focused = hit.collider.GetComponentInParent<GanymedeWorldTarget>();
            ghost.SetActive(false);
            if (focused != null && GanymedeMission.KnownSite(focused.targetId) && Mission.StationBuilt(focused.targetId) && string.IsNullOrEmpty(Mission.State.placedSite) && Mission.State.dosimeterCollected && Mission.Record(focused.targetId) == null)
            { var at=SitePosition(focused.targetId)+new Vector3(1.7f,0,-1);if(environment!=null&&environment.Loaded)at.y=environment.GroundAt(at);ghost.transform.position=at; ghost.SetActive(true); }
            if (Time.unscaledTime < nextInput) return;
            bool pick = mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool use = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (keys != null && keys.eKey.wasPressedThisFrame);
            if (focused != null && (pick || use)) Interact(focused.targetId,pick);
        }

        public void Interact(string id, bool pickup = false)
        {
            if (Mission == null || panel != null || busy) return;
            nextInput = Time.unscaledTime + .18f;
            if (id == "case") { if (pickup) Mission.Collect(); else OpenPanel("case"); }
            else if (GanymedeMission.KnownStation(id) && !Mission.StationBuilt(id))
            { installingStation=id;selectedComponent="base";OpenPanel("install"); }
            else if (GanymedeMission.KnownSite(id))
            {
                if (pickup && Mission.State.placedSite == id) { Mission.Pack(); return; }
                if (Mission.Record(id) != null && Mission.State.placedSite != id) { OpenPanel("field"); return; }
                if (string.IsNullOrEmpty(Mission.State.placedSite))
                {
                    bool valid = Vector3.Distance(character.transform.position,SitePosition(id)) <= 4f;
                    if (!Mission.Place(id,valid)) { RefreshInstrument(); return; }
                }
                if (Mission.State.placedSite == id) OpenPanel("survey");
            }
            else if (id == "lander") OpenPanel("field");
            else if (id == GanymedeMission.Relay) OpenPanel("photo");
            else if (id == "photo") OpenPanel("photo");
            UpdateReadouts();
        }

        private void UpdateReadouts()
        {
            var s = Mission.State;
            if (objective != null) objective.text = Objective();
            if (status != null) status.text = Mission.Notice;
            if (interaction != null) interaction.text = panel != null || busy ? "" : focused != null ? "+\n" + TargetPrompt(focused.targetId) : "+";
            if (panelStatus != null) panelStatus.text = Mission.Notice;
            if (bubble != null) { bubble.anchorMin = bubble.anchorMax = new Vector2(.5f + s.levelX * .40f,.5f + s.levelY * .40f); bubble.anchoredPosition = Vector2.zero; }
            if (needle != null) { needle.anchorMin = new Vector2((s.antennaPosition+1)*.5f,0); needle.anchorMax = new Vector2((s.antennaPosition+1)*.5f,1); }
            if (progressText != null)
            {
                float value = panelId == "survey" ? Mission.SampleProgress/3f : panelId == "transit" ? Mission.CheckoutProgress/3f : 0;
                if (panelId == "contact")
                    progressText.text = Mission.ContactRunning ? "CONTACT " + s.windowRemaining.ToString("0.0") + " s  /  Sending: " + GanymedeMission.PacketName(s.queue.FirstOrDefault()) + " · " + Mission.PacketProgress.ToString("0.0") + " s" : s.antennaLocked ? "Signal locked · " + Mission.WindowSeconds + "-second contact window" : "A / D or ← / → · center in the band and hold 1 second · " + Mission.AlignmentProgress.ToString("0.0") + " s";
                else if (panelId == "survey") progressText.text = Mission.Record(s.placedSite) != null ? "SAVED · " + Mission.Record(s.placedSite).simulatedDoseRate + " μSv/h (simulated)" : "Sample progress: " + Mathf.Clamp01(value).ToString("P0");
                else if (panelId == "transit") progressText.text = "Cabin + radio: 3 · total " + Mission.PowerLoad + " / 5 units   |   Checkout " + (s.checkoutComplete ? "PASSED" : Mathf.Clamp01(value).ToString("P0"));
                if (progressBar != null) progressBar.anchorMax = new Vector2(Mathf.Clamp01(value),1);
            }
        }

        private string Objective()
        {
            var s = Mission.State;
            if (s.phase == GanymedePhase.Transit) return "Protect the reserve · test the orbiter before landing";
            if (s.phase == GanymedePhase.Surface)
            {
                if (!s.dosimeterCollected) return "Collect the dosimeter beside the lander";
                if(!s.radiationStationBuilt)return "Build the radiation station · A / Crater Plain";
                if(!s.iceStationBuilt&&Mission.Record(GanymedeMission.SiteA)!=null)return "Build the ice survey station · B / Grooved Rise";
                if (!string.IsNullOrEmpty(s.placedSite)) return Mission.Record(s.placedSite) != null ? "Pack the dosimeter · right click at the site" : "Level and sample · " + GanymedeMission.SiteName(s.placedSite);
                if (!Mission.BothRecords) return Mission.Record(GanymedeMission.SiteA) == null ? "Find marker A · Crater Plain" : "Find the pale grooves · marker B";
                if(!s.uplinkBuilt)return "Build the uplink relay · C / Overlook";
                return s.comparisonSaved ? "Return to the lander · ready to ascend" : "Compare the two records at the lander console";
            }
            if (s.phase == GanymedePhase.Orbit) return "Confirm crew systems · release at the marked point";
            if (s.phase == GanymedePhase.Contact) return s.antennaLocked ? "Send health + small science reading to Earth" : "Align the directional antenna";
            return "First Signal Received · open the campaign flight log";
        }
        private string TargetPrompt(string id)
        {
            if (id == "case") return Mission.State.dosimeterCollected ? "DOSIMETER COLLECTED" : "RIGHT CLICK · Pick up dosimeter";
            if(GanymedeMission.KnownStation(id)&&!Mission.StationBuilt(id))return "LEFT CLICK · Assemble "+GanymedeMission.StationName(id);
            if(id==GanymedeMission.Relay)return "UPLINK COMMISSIONED\nLEFT CLICK · Optional terrain photo";
            if (GanymedeMission.KnownSite(id)) return GanymedeMission.SiteName(id) + (Mission.State.placedSite == id ? "\nLEFT CLICK · Instrument   |   RIGHT CLICK · Pack" : Mission.Record(id) != null ? "\nRECORD SAVED" : "\nLEFT CLICK · Place instrument on flat patch");
            return id == "lander" ? "LEFT CLICK · Field records / departure" : "LEFT CLICK · Optional terrain photo";
        }

        private void OpenPanel(string id)
        {
            ClosePanel(false); panelId = id; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var frame = Box(ui.transform,"Ganymede " + id,.11f,.15f,.78f,.68f,Navy); panel = frame.gameObject;
            Box(frame,"Accent",0,.99f,1,.01f,Cyan);
            panelStatus = Label(frame,"",.04f,.04f,.92f,.12f,20,Muted);
            if (id == "transit") Transit(frame);
            else if (id == "install") Installation(frame);
            else if (id == "survey") Survey(frame);
            else if (id == "field") Field(frame);
            else if (id == "deploy") Deployment(frame);
            else if (id == "contact") Contact(frame);
            else if (id == "ack") Acknowledgement(frame);
            else if (id == "log") FlightLog(frame);
            else if (id == "photo") Photo(frame);
            else if (id == "case")
            {
                Title(frame,"SURFACE DOSIMETER");
                Label(frame,"Collect the instrument and station tools. Build and commission the radiation station, ice survey station and uplink relay.\n\nUse one portable dosimeter at A and B. Level it, save a three-second sample, and pack it before moving on.",.05f,.37f,.87f,.39f,27,Color.white);
                Button(frame,"PICK UP",.05f,.20f,.36f,.10f,()=>{Mission.Collect();ClosePanel();});
            }
            else Help(frame);
            if (Mission.State.phase == GanymedePhase.Surface || id == "help") Button(frame,"BACK",.79f,.88f,.17f,.075f,()=>{if(Mission.State.phase == GanymedePhase.Surface)ClosePanel();else RenderPhase();},Card);
            renderedRevision = Mission.State.revision; UpdateReadouts();
            if (EventSystem.current != null)
            {
                var first = panel.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.interactable);
                EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
            }
        }
        private void RefreshPanel() { if (!string.IsNullOrEmpty(panelId)) OpenPanel(panelId); }
        private void ClosePanel(bool lockMouse = true)
        {
            if (panel != null) { panel.SetActive(false); Destroy(panel); }
            panel = null; panelId = null; bubble = needle = progressBar = null; panelStatus = progressText = null;
            if (lockMouse && Mission != null && Mission.State.phase == GanymedePhase.Surface) LockMouse();
            nextInput = Time.unscaledTime + .18f;
        }
        private void Transit(Transform p)
        {
            var s = Mission.State; Title(p,"MARS → GANYMEDE  /  PROTECT THE RESERVE");
            Label(p,"Sam: Commander " + s.handoff.playerName + ", our orbiter is still locked in. Test it before we go down.",.05f,.72f,.9f,.12f,26,Color.white);
            Button(p,s.cameraOn ? "EXTERIOR CAMERA · ON" : "EXTERIOR CAMERA · OFF",.05f,.56f,.40f,.11f,()=>Mission.SetCamera(!s.cameraOn),s.cameraOn ? Orange : Card);
            Button(p,Mission.CheckoutRunning ? "CHECKOUT RUNNING…" : "RUN ORBITER CHECKOUT",.50f,.56f,.45f,.11f,()=>Mission.BeginCheckout()).interactable = !Mission.CheckoutRunning && !s.checkoutComplete;
            progressText = Label(p,"",.05f,.44f,.90f,.09f,24,Cyan); Progress(p,.05f,.41f,.90f,.015f);
            Label(p,"APPROACH PLAN\n1  Land on Ganymede     2  Return to orbit     3  Release at the marked point",.05f,.27f,.90f,.12f,23,Color.white);
            Button(p,"CONFIRM PLAN & LAND",.54f,.17f,.41f,.085f,()=>Transition("DESCENT CONFIRMED",()=>Mission.Land()),Orange).interactable = s.checkoutComplete;
        }
        private void Survey(Transform p)
        {
            var s = Mission.State; Title(p,GanymedeMission.SiteName(s.placedSite).ToUpperInvariant());
            var level = Box(p,"Bubble level",.06f,.30f,.36f,.49f,Card);
            Box(level,"Vertical tolerance",.44f,0,.12f,1,new Color(.13f,.29f,.30f));
            Box(level,"Horizontal tolerance",0,.44f,1,.12f,new Color(.13f,.29f,.30f));
            bubble = Box(level,"Bubble",.49f,.49f,.02f,.02f,Orange); bubble.sizeDelta = new Vector2(22,22); bubble.pivot = new Vector2(.5f,.5f);
            Label(p,"CENTER THE BUBBLE\nArrow keys or adjustment buttons",.48f,.66f,.46f,.13f,25,Color.white);
            Button(p,"X −",.49f,.53f,.20f,.09f,()=>Mission.AdjustLevel(-.1f,0),Card);
            Button(p,"X +",.72f,.53f,.20f,.09f,()=>Mission.AdjustLevel(.1f,0),Card);
            Button(p,"Y −",.49f,.42f,.20f,.09f,()=>Mission.AdjustLevel(0,-.1f),Card);
            Button(p,"Y +",.72f,.42f,.20f,.09f,()=>Mission.AdjustLevel(0,.1f),Card);
            Button(p,s.instrumentLeveled ? "LEVEL CONFIRMED" : "CONFIRM LEVEL",.49f,.30f,.43f,.09f,()=>Mission.ConfirmLevel()).interactable = !s.instrumentLeveled;
            progressText = Label(p,"",.05f,.22f,.52f,.07f,21,Cyan);
            Button(p,"SWITCH ON / SAMPLE",.60f,.19f,.32f,.08f,()=>Mission.BeginSample()).interactable = s.instrumentLeveled && Mission.Record(s.placedSite) == null && !Mission.Sampling;
            Progress(p,.05f,.18f,.50f,.018f);
        }
        private void Installation(Transform p)
        {
            string station=installingStation;Title(p,"ASSEMBLE / "+GanymedeMission.StationName(station).ToUpperInvariant());
            Label(p,"Select a component, then fit its matching socket. Secure the base first. The assembled model appears when commissioning passes.",.05f,.70f,.9f,.12f,25,Color.white);
            string[] parts={"base","head","power"};string[] labels={"□ BASE / FRAME","△ INSTRUMENT HEAD","○ POWER CONNECTOR"};
            for(int i=0;i<3;i++)
            {
                string part=parts[i];bool installed=Mission.State.installedParts.Contains(station+"/"+part);float y=.55f-i*.13f;
                Button(p,labels[i],.06f,y,.36f,.10f,()=>{selectedComponent=part;RefreshPanel();},selectedComponent==part?Orange:Card).interactable=!installed;
                var socket=Button(p,installed?"✓ SECURED":"FIT TO "+labels[i],.54f,y,.40f,.10f,()=>Mission.FitStationPart(station,selectedComponent,part),installed?Cyan:Card);socket.interactable=!installed;
                Box(p,"Connection line",.44f,y+.047f,.08f,.006f,installed?Cyan:Muted);
            }
            Button(p,"COMMISSION STATION",.54f,.17f,.40f,.09f,()=>{if(Mission.CommissionStation(station)){RefreshInstrument();ClosePanel();}},Orange);
        }
        private void Field(Transform p)
        {
            Title(p,"TWO-SITE FIELD RECORD"); var a = Mission.Record(GanymedeMission.SiteA); var b = Mission.Record(GanymedeMission.SiteB);
            RecordCard(p,a,.05f); RecordCard(p,b,.52f);
            Button(p,"LEFT LABEL: " + (fieldLabelA == GanymedeMission.SiteA ? "CRATER PLAIN" : "GROOVED RISE"),.05f,.47f,.43f,.075f,()=>{fieldLabelA = fieldLabelA == GanymedeMission.SiteA ? GanymedeMission.SiteB : GanymedeMission.SiteA;RefreshPanel();},Card);
            Button(p,"RIGHT LABEL: " + (fieldLabelB == GanymedeMission.SiteB ? "GROOVED RISE" : "CRATER PLAIN"),.52f,.47f,.43f,.075f,()=>{fieldLabelB = fieldLabelB == GanymedeMission.SiteA ? GanymedeMission.SiteB : GanymedeMission.SiteA;RefreshPanel();},Card);
            Button(p,"Site B had the lower reading when we measured it.",.05f,.35f,.90f,.085f,()=>Mission.Compare(fieldLabelA,fieldLabelB,true));
            Button(p,"Site B is always safe.",.05f,.25f,.43f,.075f,()=>Mission.Compare(fieldLabelA,fieldLabelB,false),Card);
            Button(p,"ASCEND TO ORBIT",.56f,.19f,.39f,.09f,()=>Transition("ASCENT CONFIRMED",()=>Mission.Ascend()),Orange).interactable = Mission.CanAscend;
            Label(p,"Stations built: "+(Mission.State.radiationStationBuilt?"A ✓":"A pending")+"  /  "+(Mission.State.iceStationBuilt?"B ✓":"B pending")+"  /  "+(Mission.State.uplinkBuilt?"C ✓":"C pending"),.05f,.17f,.49f,.07f,18,Cyan);
        }
        private void RecordCard(Transform parent, SurfaceRecord record, float x)
        {
            var box = Box(parent,"Surface record",x,.56f,.43f,.26f,Card);
            if (record == null) { Label(box,"NO RECORD\nVisit the marked surface site.",.05f,.08f,.9f,.8f,23,Muted); return; }
            TerrainThumbnail(box,record.thumbnailId,.03f,.08f,.33f,.84f);
            Label(box,record.location + "\n" + record.timestamp + "\nSimulated dose rate: " + record.simulatedDoseRate + " μSv/h",.40f,.07f,.56f,.86f,21,Color.white);
        }
        private void Deployment(Transform p)
        {
            var s = Mission.State; Title(p,"ORBITER DEPLOYMENT");
            Label(p,"Saved Mars build: " + s.handoff.sensorMode + " / " + s.handoff.antenna + " antenna / " + s.handoff.shield + " shield\n" + s.handoff.orbiterMassKg + " kg · both clamps secure",.05f,.68f,.90f,.15f,25,Color.white);
            Box(p,"Planned route",.10f,.56f,.80f,.012f,Muted);
            Label(p,s.deploymentPointReached ? "◆ READY TO RELEASE" : "◇ APPROACHING RELEASE POINT",.31f,.48f,.62f,.10f,25,Cyan);
            Button(p,s.crewReady ? "CREW SYSTEMS READY" : "CONFIRM CREW SYSTEMS",.05f,.33f,.42f,.10f,()=>Mission.ConfirmCrew());
            Button(p,"ACCELERATE TO MARKED POINT",.51f,.33f,.44f,.10f,()=>Transition("APPROACHING DEPLOYMENT POINT",()=>Mission.ReachDeploymentPoint()),Card).interactable = s.crewReady && !s.deploymentPointReached;
            Button(p,"RELEASE BOTH CLAMPS",.51f,.19f,.44f,.10f,()=>{if(Mission.Deploy())StartCoroutine(Separation());},Orange).interactable = s.crewReady && s.deploymentPointReached;
        }
        private void Contact(Transform p)
        {
            var s = Mission.State; Title(p,"FIRST CONTACT  /  JUPITER ORBITER");
            progressText = Label(p,"",.05f,.73f,.90f,.10f,23,Cyan);
            if (!s.antennaLocked)
            {
                var track = Box(p,"Antenna track",.13f,.60f,.72f,.075f,Card); Box(track,"Target band",.585f,0,.08f,1,new Color(.1f,.5f,.4f));
                needle = Box(track,"Antenna pointer",.175f,0,.003f,1,Orange); needle.sizeDelta = new Vector2(6,0);
                Button(p,"POINT −",.13f,.45f,.31f,.09f,()=>Mission.AdjustAntenna(-.05f),Card);
                Button(p,"POINT +",.54f,.45f,.31f,.09f,()=>Mission.AdjustAntenna(.05f),Card);
                Label(p,"The directional antenna selected on Mars gives a longer contact window after alignment.",.13f,.24f,.72f,.16f,25,Color.white);
                return;
            }
            int row = 0;
            foreach (string id in new[] { "health", "sample", "full-graph" })
            {
                string packet = id; bool received = s.received.Contains(packet); int order = s.queue.IndexOf(packet);
                string state = received ? "RECEIVED" : Mission.ContactRunning && order == 0 ? "SENDING" : "QUEUED #" + (order+1);
                Label(p,GanymedeMission.PacketName(packet) + " · " + Mission.PacketSeconds(packet) + " s  /  " + state,.05f,.61f-row*.11f,.66f,.09f,24,received ? Cyan : Color.white);
                Button(p,"SEND FIRST",.73f,.61f-row*.11f,.22f,.08f,()=>Mission.MovePacketFirst(packet),Card).interactable = !received && !Mission.ContactRunning;
                row++;
            }
            Button(p,"REQUIRED PACKETS FIRST",.05f,.26f,.43f,.085f,()=>Mission.PrioritizeRequired(),Card).interactable = !Mission.ContactRunning;
            Button(p,"OPEN " + Mission.WindowSeconds + " s CONTACT",.52f,.26f,.43f,.085f,()=>Mission.BeginContact()).interactable = !Mission.ContactRunning && s.queue.Count > 0;
            if (Mission.RequiredPackets)
                Button(p,s.earthAcknowledged ? "RETURN TO FLIGHT LOG" : "TIME SKIP → EARTH'S REPLY",.52f,.16f,.43f,.075f,()=>{if(s.earthAcknowledged){Mission.OpenFlightLog();RenderPhase();}else Transition("SIGNAL TRAVEL / EARTH REPLY",()=>Mission.Acknowledge());},Orange);
        }
        private void Acknowledgement(Transform p)
        {
            Title(p,"FIRST SIGNAL RECEIVED");
            Label(p,"Earth Flight Control\n\nCommander " + Mission.State.handoff.playerName + ", first readings received. The orbiter is working on its own. You can head home.\n\nSam: I'll leave the receiver on a little longer.",.07f,.35f,.85f,.43f,30,Color.white);
            Button(p,"OPEN CAMPAIGN FLIGHT LOG",.48f,.19f,.47f,.11f,()=>{Mission.OpenFlightLog();RenderPhase();},Orange);
        }
        private void FlightLog(Transform p)
        {
            var s = Mission.State; var h = s.handoff; Title(p,"EXPEDITION FLIGHT LOG  /  " + h.playerName);
            string earth = "EARTH & POLE-7\nCargo: " + string.Join(", ",h.build.cargo) + "\nFuel carried forward: " + h.remainingFuelUnits.ToString("0.#") + " units\n" +
                "Route: " + (h.routeUploaded ? "updated" : "not recorded") + " · Relay: " + (h.relayRestored ? "restored" : "not recorded") +
                (h.batteryLeft ? "\nPortable battery left at lunar beacon." : "\nPortable battery kept aboard.");
            Label(p,earth,.05f,.57f,.43f,.24f,21,Color.white);
            Label(p,"MARS\n" + h.orbiterMassKg + " kg · " + h.sensorMode + " sensor\n" + h.antenna + " antenna / " + h.shield + " shield\nArrival antenna: " + h.antennaRecovery +
                (h.dustDiaryComplete ? "\nDust Diary: " + h.dustSelectedTime + " / " + h.dustSelectedIndex : ""),.52f,.57f,.43f,.24f,21,Color.white);
            Graph(p,"MARS BENCH TEST\nSimulated bench sample",h.benchGraph.samples,.05f,.30f,.43f,.23f);
            int[] observation = Mission.ParticleSamples(); if (!s.received.Contains("full-graph")) observation = observation.Take(2).ToArray();
            Graph(p,"FIRST READING FROM JUPITER\nIn-story orbiter observation",observation,.52f,.30f,.43f,.23f);
            string surfaceText = string.Join("   |   ",s.records.Select(r=>r.location + ": " + r.simulatedDoseRate + " μSv/h · " + r.timestamp));
            Label(p,"SURFACE · Simulated dose rate\n" + surfaceText + "\nSite B was lower when measured. " + (s.photoSaved ? "Terrain photo saved." : ""),.05f,.17f,.9f,.12f,19,Cyan);
            Button(p,"OPTIONAL: SEND REMAINING DATA",.05f,.065f,.45f,.075f,()=>OpenPanel("contact"),Card).interactable = s.queue.Count > 0;
            Button(p,"CREDITS",.54f,.065f,.18f,.075f,()=>OpenPanel("credits"),Card);
            Button(p,"MAIN MENU",.75f,.065f,.20f,.075f,ReturnToMenu,Orange);
            panelStatus.gameObject.SetActive(false);
        }
        private void Photo(Transform p)
        {
            Title(p,"OPTIONAL  /  TERRAIN PHOTO SURVEY");
            TerrainThumbnail(p,GanymedeMission.SiteA,.07f,.47f,.40f,.32f); TerrainThumbnail(p,GanymedeMission.SiteB,.53f,.47f,.40f,.32f);
            Label(p,"Tag both regions in this authored panorama. Shapes identify terrain; they do not establish radiation safety.",.07f,.33f,.86f,.11f,23,Color.white);
            Button(p,photoLeft ? "LEFT: DARK CRATERS" : "LEFT: PALE GROOVES",.07f,.23f,.40f,.08f,()=>{photoLeft=!photoLeft;RefreshPanel();},Card);
            Button(p,photoRight ? "RIGHT: PALE GROOVES" : "RIGHT: DARK CRATERS",.53f,.23f,.40f,.08f,()=>{photoRight=!photoRight;RefreshPanel();},Card);
            Button(p,"SAVE BOTH LABELS",.53f,.13f,.40f,.08f,()=>Mission.SavePhoto(photoLeft,photoRight));
            panelStatus.rectTransform.anchorMin = new Vector2(.04f,.01f); panelStatus.rectTransform.anchorMax = new Vector2(.96f,.12f);
        }
        private void Help(Transform p)
        {
            Title(p,panelId == "credits" ? "FAR SIGNAL  /  THE FIRST SIGNAL" : "MISSION CHECKLIST");
            string copy = panelId == "credits" ? "A Sagittarius-A team expedition\n\nCommander: " + Mission.State.handoff.playerName + "\n\nEarth → Pole-7 → Mars → Ganymede\nOne surface instrument. One Jupiter orbiter. First signal received.\n\nSurface environment supplied by the Far Signal scene team." :
                "1  Turn off the exterior camera; check the orbiter and land.\n2  Collect the equipment case beside the lander.\n3  Assemble and commission all three marked stations (A, B, C).\n4  At A and B, place the dosimeter, level, sample, then pack.\n5  Compare both records at the lander, then ascend.\n6  Confirm the crew, reach the release point and deploy.\n7  Send health + small reading; receive Earth’s reply and flight log.";
            Label(p,copy,.06f,.25f,.88f,.55f,27,Color.white);
            if(panelId == "credits") Button(p,"BACK TO FLIGHT LOG",.53f,.17f,.40f,.10f,()=>OpenPanel("log"));
            else Label(p,"WAYFINDING  A: dark round craters.  B: pale grooves.  C: uplink overlook.\nFollow the site labels and guide stakes; return to the lander silhouette.",.06f,.16f,.88f,.09f,20,Cyan);
        }

        private void Transition(string caption, Func<bool> complete)
        {
            if (busy) return; StartCoroutine(TransitionRoutine(caption,complete));
        }
        private IEnumerator TransitionRoutine(string caption, Func<bool> complete)
        {
            busy = true; ClosePanel(false); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var fade = Box(ui.transform,"Transition",0,0,1,1,new Color(.01f,.02f,.03f,.97f));
            Label(fade,caption,.15f,.43f,.7f,.14f,38,Color.white).alignment=TextAnchor.MiddleCenter;
            bool skip = false; Button(fade,"SKIP",.78f,.10f,.15f,.075f,()=>skip=true,Card);
            for(float t=0;t<1.8f&&!skip;t+=Time.unscaledDeltaTime) yield return null;
            complete(); fade.gameObject.SetActive(false); Destroy(fade.gameObject); busy=false; RenderPhase();
        }
        private IEnumerator Separation()
        {
            busy=true; ClosePanel(false); Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            character.transform.position=new Vector3(-4,2,-2); view.transform.rotation=Quaternion.LookRotation(new Vector3(0,2,4)-view.transform.position);
            var strip=Box(ui.transform,"Separation controls",.08f,.68f,.84f,.16f,Navy);
            Label(strip,"BOTH CLAMPS OPEN  /  ORBITER SEPARATION",.03f,.45f,.74f,.45f,28,Color.white);
            Label(strip,"Scripted transfer → Jupiter orbit insertion → first contact",.03f,.06f,.74f,.35f,21,Cyan);
            bool skip=false;Button(strip,"SKIP",.81f,.18f,.16f,.55f,()=>skip=true,Card);
            leftClamp.transform.localPosition+=Vector3.left*.65f;rightClamp.transform.localPosition+=Vector3.right*.65f;
            Vector3 from=orbiter.transform.localPosition;
            for(float t=0;t<4&&!skip;t+=Time.unscaledDeltaTime){orbiter.transform.localPosition=from+new Vector3(0,t*.5f,t*2);yield return null;}
            strip.gameObject.SetActive(false);Destroy(strip.gameObject);busy=false;RenderPhase();
        }

        private void BuildWorld()
        {
            surface=new GameObject("Ganymede surface");surface.transform.SetParent(root.transform,false);
            cabin=new GameObject("Reused cabin presentation");cabin.transform.SetParent(root.transform,false);
            var lightObject=new GameObject("Cool sunlight",typeof(Light));lightObject.transform.SetParent(root.transform,false);
            var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.color=new Color(.76f,.86f,1);light.intensity=1.6f;light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(32,-28,0);
            environment=surface.AddComponent<GanymedeEnvironmentView>();
            if(environment.Initialize())
            {
                siteA=environment.SiteA;siteB=environment.SiteB;photoSpot=environment.Overlook;spawnPoint=environment.Spawn;
                var jupiter=environment.FindMesh("Jupiter | procedural");if(jupiter!=null)jupiter.transform.SetParent(root.transform,true);
                BuildAuthoredInteractions();
            }
            else
            {
            Primitive(surface.transform,"Ice plain",PrimitiveType.Cube,new Vector3(0,-.6f,15),new Vector3(140,1.2f,140),new Color(.42f,.48f,.52f));
            Primitive(surface.transform,"Dark crater plain",PrimitiveType.Cylinder,A-new Vector3(0,.02f,0),new Vector3(22,.045f,22),new Color(.20f,.25f,.29f));
            Primitive(surface.transform,"Bright grooved rise",PrimitiveType.Cylinder,B-new Vector3(0,.02f,0),new Vector3(23,.045f,23),new Color(.64f,.72f,.77f));
            for(int i=0;i<6;i++) Primitive(surface.transform,"Pale groove",PrimitiveType.Cube,B+new Vector3(-7+i*2.7f,.06f,4),new Vector3(.5f,.10f,12),new Color(.41f,.53f,.61f));
            for(int i=0;i<7;i++)
            {
                float angle=i*2.39996f;Vector3 pos=A+new Vector3(Mathf.Sin(angle)*7,.03f,Mathf.Cos(angle)*7);
                Primitive(surface.transform,"Crater floor",PrimitiveType.Cylinder,pos,new Vector3(2.8f,.035f,2.8f),new Color(.12f,.17f,.21f));
                Ring(surface.transform,"Low crater rim",pos,1.55f,.11f,new Color(.35f,.4f,.44f),false);
            }
            Path(new Vector3(0,0,1),A);Path(A,B);Path(B,new Vector3(0,0,1));Path(B,Overlook);
            BuildLander();BuildSite(GanymedeMission.SiteA,A,false);BuildSite(GanymedeMission.SiteB,B,true);
            var photo=Primitive(surface.transform,"Uplink station",PrimitiveType.Cube,Overlook+Vector3.up*.7f,new Vector3(1,.9f,.6f),new Color(.19f,.34f,.40f));Target(photo,GanymedeMission.Relay,"Uplink relay");
            Sign(surface.transform,"OPTIONAL\nTERRAIN PHOTO",Overlook+Vector3.up*1.8f,Cyan);
            for(int i=0;i<30;i++)
            {
                float angle=i*Mathf.PI*2/30;Vector3 pos=new Vector3(Mathf.Sin(angle)*48,0,15+Mathf.Cos(angle)*48);
                var rock=Primitive(surface.transform,"Ice ridge",PrimitiveType.Sphere,pos,new Vector3(5+(i%3)*2,2+(i%4),5),new Color(.36f,.43f,.50f));rock.transform.Rotate(0,i*27,15);
            }
            }
            instrument=BuildTripod(surface.transform,"Surface dosimeter",new Color(.88f,.91f,.93f));instrument.SetActive(false);
            ghost=BuildTripod(surface.transform,"Placement preview",new Color(.27f,.8f,.85f));ghost.SetActive(false);
            foreach(var collider in ghost.GetComponentsInChildren<Collider>())Destroy(collider);
            BuildCabin();if(!environment.Loaded)BuildJupiter();
            var playerObject=new GameObject("Ganymede commander",typeof(CharacterController));playerObject.transform.SetParent(root.transform,false);
            character=playerObject.GetComponent<CharacterController>();character.height=1.8f;character.radius=.3f;character.center=new Vector3(0,.9f,0);character.stepOffset=.3f;character.slopeLimit=45;
            var cameraObject=new GameObject("Ganymede POV",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(playerObject.transform,false);cameraObject.transform.localPosition=new Vector3(0,1.62f,0);
            view=cameraObject.GetComponent<Camera>();view.fieldOfView=67;view.nearClipPlane=.05f;view.farClipPlane=1800;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.005f,.009f,.018f);
        }
        private void BuildAuthoredInteractions()
        {
            var shipAt=environment.Lander;shipAt.y=environment.GroundAt(shipAt);
            if(SagittariusA.Gameplay.Flow.ExpeditionShipVisual.Create(surface.transform,shipAt)!=null)
                foreach(var r in environment.GetComponentsInChildren<MeshRenderer>())if(r.name.StartsWith("Lander |")&&!r.name.Contains("Instrument case")&&!r.name.Contains("Ramp"))r.gameObject.SetActive(false);
            var caseMesh=environment.FindMesh("Lander | Instrument case");if(caseMesh!=null)Target(caseMesh.gameObject,"case","Pick up equipment");
            var markerA=environment.FindMesh("Marker A");var markerB=environment.FindMesh("Marker B");var markerC=environment.FindMesh("Marker C");
            if(markerA==null||markerB==null||markerC==null||caseMesh==null)throw new InvalidOperationException("Ganymede site markers or equipment case missing.");
            Target(markerA.gameObject,GanymedeMission.SiteA,"Radiation station");Target(markerB.gameObject,GanymedeMission.SiteB,"Ice survey station");Target(markerC.gameObject,GanymedeMission.Relay,"Uplink relay");
            Vector3 at=environment.Lander+new Vector3(-3.6f,0,1.2f);at.y=environment.GroundAt(at)+.65f;
            var console=Primitive(surface.transform,"Field records console",PrimitiveType.Cube,at,new Vector3(.8f,1.3f,.6f),new Color(.12f,.24f,.31f));Target(console,"lander","Field records / departure");
            Sign(surface.transform,"RECORDS / RETURN",at+Vector3.up*1.1f,Cyan);
            Sign(surface.transform,"EQUIPMENT",caseMesh.bounds.center+Vector3.up*.85f,Orange);
        }
        private void BuildLander()
        {
            Primitive(surface.transform,"Lander pad",PrimitiveType.Cylinder,new Vector3(0,.04f,-6),new Vector3(13,.04f,13),new Color(.17f,.23f,.29f));
            var body=Primitive(surface.transform,"Lander",PrimitiveType.Cylinder,new Vector3(0,2,-7),new Vector3(4,1.5f,4),new Color(.73f,.79f,.81f));
            Primitive(surface.transform,"Lander window",PrimitiveType.Cube,new Vector3(0,2.6f,-4.96f),new Vector3(2,.7f,.1f),new Color(.08f,.20f,.30f));
            Primitive(surface.transform,"Cargo compartment",PrimitiveType.Cube,new Vector3(0,1.1f,-4.9f),new Vector3(2,1.3f,.18f),new Color(.28f,.34f,.39f));
            for(int i=0;i<4;i++){float x=i%2==0?-2.3f:2.3f;float z=i<2?-5:-9;Primitive(surface.transform,"Landing leg",PrimitiveType.Cylinder,new Vector3(x,.55f,z),new Vector3(.2f,.55f,.2f),Orange);Primitive(surface.transform,"Foot",PrimitiveType.Cylinder,new Vector3(x,.08f,z),new Vector3(.9f,.08f,.9f),Card);}
            var ramp=Primitive(surface.transform,"Lander ramp",PrimitiveType.Cube,new Vector3(0,.30f,-3.8f),new Vector3(1.9f,.15f,2.3f),new Color(.5f,.6f,.65f));ramp.transform.rotation=Quaternion.Euler(15,0,0);
            var box=Primitive(surface.transform,"Dosimeter case",PrimitiveType.Cube,new Vector3(-2.6f,.52f,-.3f),new Vector3(1.1f,1,.65f),Orange);Target(box,"case","Pick up dosimeter");Sign(surface.transform,"DOSIMETER",new Vector3(-2.6f,1.3f,-.3f),Color.white);
            var console=Primitive(surface.transform,"Lander records console",PrimitiveType.Cube,new Vector3(2.6f,.7f,-.3f),new Vector3(1.2f,1.4f,.6f),new Color(.12f,.24f,.31f));Target(console,"lander","Field records / departure");Sign(surface.transform,"RECORDS / RETURN",new Vector3(2.6f,1.8f,-.3f),Cyan);
        }
        private void BuildSite(string id,Vector3 at,bool fork)
        {
            Ring(surface.transform,"Flat placement guide",at+Vector3.up*.09f,1.8f,.035f,Cyan,true);
            var control=Primitive(surface.transform,"Survey marker "+id,PrimitiveType.Cube,at+new Vector3(0,.60f,0),new Vector3(.85f,1.2f,.65f),new Color(.12f,.27f,.33f));Target(control,id,"Place / use dosimeter");
            if(fork){for(int i=-1;i<=1;i+=2)Primitive(surface.transform,"Fork arm",PrimitiveType.Cube,at+new Vector3(i*.32f,1.6f,0),new Vector3(.13f,.75f,.13f),Cyan);Primitive(surface.transform,"Fork base",PrimitiveType.Cube,at+new Vector3(0,1.25f,0),new Vector3(.78f,.13f,.13f),Cyan);}
            else Ring(surface.transform,"Round A marker",at+new Vector3(0,1.55f,0),.42f,.065f,Orange,false,true);
            Sign(surface.transform,fork?"B · GROOVED RISE":"A · CRATER PLAIN",at+Vector3.up*2.2f,Color.white);
        }
        private GameObject BuildTripod(Transform parent,string name,Color color)
        {
            var holder=new GameObject(name);holder.transform.SetParent(parent,false);
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;Vector3 foot=new Vector3(Mathf.Sin(a)*.6f,.06f,Mathf.Cos(a)*.6f);
                Beam(holder.transform,"Tripod leg",foot,new Vector3(0,1.15f,0),.055f,color);
                Primitive(holder.transform,"Foot target",PrimitiveType.Cylinder,foot,new Vector3(.2f,.025f,.2f),Orange);
            }
            Primitive(holder.transform,"Dosimeter head",PrimitiveType.Cube,new Vector3(0,1.25f,0),new Vector3(.55f,.28f,.40f),color);
            Primitive(holder.transform,"Display",PrimitiveType.Cube,new Vector3(0,1.26f,-.21f),new Vector3(.39f,.15f,.025f),Cyan);
            return holder;
        }
        private void RefreshInstrument()
        {
            if(environment!=null&&environment.Loaded)environment.SetBuilt(Mission.State.radiationStationBuilt,Mission.State.iceStationBuilt,Mission.State.uplinkBuilt);
            if(instrument==null)return;bool placed=!string.IsNullOrEmpty(Mission.State.placedSite);instrument.SetActive(placed);
            if(placed){Vector3 at=SitePosition(Mission.State.placedSite)+new Vector3(1.7f,0,-1);if(environment!=null&&environment.Loaded)at.y=environment.GroundAt(at);instrument.transform.position=at;}
            if(orbiter!=null)orbiter.SetActive(!Mission.State.deployed||busy);
            if(leftClamp!=null){leftClamp.SetActive(!Mission.State.deployed||busy);rightClamp.SetActive(!Mission.State.deployed||busy);}
        }
        private void BuildCabin()
        {
            Primitive(cabin.transform,"Cabin deck",PrimitiveType.Cube,new Vector3(0,-.2f,1),new Vector3(12,.4f,15),new Color(.09f,.14f,.19f));
            for(int i=-1;i<=1;i+=2){Primitive(cabin.transform,"Cabin side",PrimitiveType.Cube,new Vector3(i*5,2,1),new Vector3(.25f,4,14),new Color(.13f,.20f,.27f));Primitive(cabin.transform,"Window frame",PrimitiveType.Cube,new Vector3(i*3.8f,2,6),new Vector3(.2f,4,.3f),Orange);}
            Primitive(cabin.transform,"Cradle",PrimitiveType.Cube,new Vector3(0,.7f,4),new Vector3(3,.6f,2.1f),new Color(.18f,.25f,.30f));
            orbiter=new GameObject("Saved Mars orbiter");orbiter.transform.SetParent(cabin.transform,false);orbiter.transform.localPosition=new Vector3(0,1.2f,4);
            Primitive(orbiter.transform,"Common frame",PrimitiveType.Cube,Vector3.zero,new Vector3(2,.35f,1.3f),new Color(.74f,.79f,.81f));
            Primitive(orbiter.transform,"Particle sensor",PrimitiveType.Cylinder,new Vector3(-.55f,.4f,0),new Vector3(.4f,.3f,.4f),Orange);
            bool dish=Mission.State.handoff.antenna=="directional";
            Primitive(orbiter.transform,"Antenna mast",PrimitiveType.Cylinder,new Vector3(.55f,.5f,0),new Vector3(.08f,.5f,.08f),Cyan);
            if(dish){var antenna=Primitive(orbiter.transform,"Directional dish",PrimitiveType.Sphere,new Vector3(.55f,.95f,0),new Vector3(.85f,.17f,.85f),new Color(.83f,.87f,.90f));antenna.transform.rotation=Quaternion.Euler(35,0,0);}
            Primitive(orbiter.transform,"Shield",PrimitiveType.Cube,new Vector3(0,-.28f,0),new Vector3(2.2f,Mission.State.handoff.shield=="reinforced"?.3f:.12f,1.5f),new Color(.35f,.43f,.50f));
            leftClamp=Primitive(cabin.transform,"Left clamp",PrimitiveType.Cube,new Vector3(-1.25f,1.25f,4),new Vector3(.15f,1.1f,1.3f),Orange);
            rightClamp=Primitive(cabin.transform,"Right clamp",PrimitiveType.Cube,new Vector3(1.25f,1.25f,4),new Vector3(.15f,1.1f,1.3f),Orange);
        }
        private void BuildJupiter()
        {
            var planet=new GameObject("Jupiter · stylized banded horizon");planet.transform.SetParent(root.transform,false);planet.transform.position=new Vector3(58,46,105);planet.transform.localScale=Vector3.one*19;
            Color[] colors={new Color(.71f,.59f,.48f),new Color(.92f,.83f,.68f),new Color(.66f,.44f,.31f),new Color(.91f,.77f,.60f),new Color(.73f,.53f,.37f),new Color(.94f,.85f,.70f),new Color(.70f,.53f,.43f),new Color(.86f,.76f,.63f)};
            for(int band=0;band<colors.Length;band++)
            {
                var obj=new GameObject("Cloud band "+band,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(planet.transform,false);
                var vertices=new List<Vector3>();var triangles=new List<int>();
                for(int row=0;row<=4;row++)for(int col=0;col<=64;col++){float lat=-Mathf.PI/2+(band+row/4f)*Mathf.PI/colors.Length;float lon=col*Mathf.PI*2/64;vertices.Add(new Vector3(Mathf.Cos(lat)*Mathf.Cos(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(lon)));}
                for(int row=0;row<4;row++)for(int col=0;col<64;col++){int v=row*65+col;triangles.AddRange(new[]{v,v+65,v+1,v+1,v+65,v+66});}
                var mesh=new Mesh();mesh.name="Jupiter band";mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();generatedMeshes.Add(mesh);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
                obj.GetComponent<MeshRenderer>().sharedMaterial=Material(colors[band],true);
            }
        }
        private void Path(Vector3 from,Vector3 to)
        {
            Vector3 direction=(to-from).normalized;Vector3 side=Vector3.Cross(direction,Vector3.up)*1.5f;float distance=Vector3.Distance(from,to);
            for(float t=2;t<distance-2;t+=4)for(int s=-1;s<=1;s+=2)
            {Vector3 at=from+direction*t+side*s;Primitive(surface.transform,"Guide post",PrimitiveType.Cylinder,at+Vector3.up*.3f,new Vector3(.09f,.3f,.09f),Muted);Primitive(surface.transform,"Guide light",PrimitiveType.Sphere,at+Vector3.up*.61f,new Vector3(.17f,.17f,.17f),Cyan,false,true);}
        }
        private void Ring(Transform parent,string name,Vector3 at,float radius,float width,Color color,bool dashed,bool upright=false)
        {
            for(int i=0;i<32;i++){if(dashed&&i%2==1)continue;float a=i*Mathf.PI*2/32;float b=(i+1)*Mathf.PI*2/32;Vector3 v1=upright?new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0):new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);Vector3 v2=upright?new Vector3(Mathf.Sin(b)*radius,Mathf.Cos(b)*radius,0):new Vector3(Mathf.Sin(b)*radius,0,Mathf.Cos(b)*radius);Beam(parent,name,at+v1,at+v2,width,color,false);}
        }
        private void Beam(Transform parent,string name,Vector3 from,Vector3 to,float width,Color color,bool collision=true)
        {var go=Primitive(parent,name,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(width,Vector3.Distance(from,to)*.5f,width),color,collision);go.transform.up=(to-from).normalized;}
        private GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 at,Vector3 size,Color color,bool collision=true,bool unlit=false)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=Material(color,unlit);if(!collision)Destroy(go.GetComponent<Collider>());return go;}
        private Material Material(Color color,bool unlit=false)
        {var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")??Shader.Find(unlit?"Unlit/Color":"Standard");var mat=new Material(shader);mat.color=color;materials.Add(mat);return mat;}
        private void Target(GameObject go,string id,string prompt){var target=go.AddComponent<GanymedeWorldTarget>();target.targetId=id;target.prompt=prompt;}
        private void Sign(Transform parent,string text,Vector3 at,Color color)
        {var go=new GameObject(text,typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=at;var mesh=go.GetComponent<TextMesh>();mesh.text=text;mesh.fontSize=48;mesh.characterSize=.035f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;signs.Add(mesh);}
        public Vector3 SitePosition(string id)=>id==GanymedeMission.SiteA?siteA:id==GanymedeMission.SiteB?siteB:photoSpot;

        private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(x+w,y+h);rect.offsetMin=rect.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return rect;}
        private static Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color)
        {var go=new GameObject(text.Length<45?text:"Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(x+w,y+h);rect.offsetMin=rect.offsetMax=Vector2.zero;var label=go.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAnchor.MiddleLeft;label.supportRichText=false;label.raycastTarget=false;return label;}
        private static Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,Color? color=null)
        {var rect=Box(parent,text,x,y,w,h,color??Cyan);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>action());var label=Label(rect,text,.035f,.04f,.93f,.92f,21,color==Card?Color.white:new Color(.02f,.05f,.07f));label.alignment=TextAnchor.MiddleCenter;var colors=button.colors;colors.disabledColor=new Color(.25f,.3f,.34f);button.colors=colors;return button;}
        private static void Title(Transform p,string title)=>Label(p,title,.04f,.86f,.91f,.10f,32,Color.white);
        private void Progress(Transform p,float x,float y,float w,float h){var track=Box(p,"Progress",x,y,w,h,Card);progressBar=Box(track,"Fill",0,0,0,1,Cyan);}
        private static void TerrainThumbnail(Transform p,string id,float x,float y,float w,float h)
        {
            bool crater=id==GanymedeMission.SiteA;var box=Box(p,crater?"Authored crater thumbnail":"Authored groove thumbnail",x,y,w,h,crater?new Color(.18f,.24f,.29f):new Color(.58f,.70f,.76f));
            if(crater){for(int i=0;i<5;i++){var spot=Box(box,"Crater",.07f+(i%3)*.28f,.12f+(i%2)*.42f,.23f,.30f,new Color(.09f,.15f,.20f));Box(spot,"Floor",.12f,.13f,.76f,.74f,new Color(.26f,.32f,.36f));}}
            else for(int i=0;i<6;i++){var line=Box(box,"Groove",.08f+i*.15f,.05f,.045f,.90f,new Color(.31f,.46f,.55f));line.localRotation=Quaternion.Euler(0,0,-15);}
        }
        private static void Graph(Transform p,string title,int[] values,float x,float y,float w,float h)
        {
            var box=Box(p,title,x,y,w,h,Card);Label(box,title,.03f,.62f,.94f,.35f,19,Color.white);
            if(values==null||values.Length==0)return;float max=Mathf.Max(1,values.Max());
            for(int i=0;i<values.Length;i++)Box(box,"Sample "+(i+1),.09f+i*.84f/values.Length,.14f,.6f/values.Length,.42f*values[i]/max,Cyan);
            Label(box,"Particle counts / sample index 1–"+values.Length,.04f,.01f,.92f,.14f,14,Muted);
        }
        private static void LockMouse(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        private void ReturnToMenu(){Exit();GetComponent<GameFlowController>()?.Show(GameScreen.MainMenu);}
        public void Exit()
        {
            if(Mission==null)return;exiting=true;StopAllCoroutines();Mission.Flush();Mission=null;
            if(root!=null){root.SetActive(false);Destroy(root);}if(ownedEvents!=null)Destroy(ownedEvents);
            foreach(var mat in materials)if(mat!=null)Destroy(mat);materials.Clear();signs.Clear();
            foreach(var mesh in generatedMeshes)if(mesh!=null)Destroy(mesh);generatedMeshes.Clear();
            foreach(var listener in hiddenListeners)if(listener!=null)listener.enabled=true;hiddenListeners.Clear();
            foreach(var c in hiddenCameras)if(c!=null)c.enabled=true;hiddenCameras.Clear();foreach(var c in hiddenCanvases)if(c!=null)c.enabled=true;hiddenCanvases.Clear();
            RenderSettings.fog=priorFog;RenderSettings.ambientMode=priorAmbientMode;RenderSettings.ambientLight=priorAmbient;
            Cursor.lockState=priorCursor;Cursor.visible=priorCursorVisible;root=surface=cabin=ui=panel=instrument=ghost=orbiter=null;focused=null;panelId=null;renderedPhase=(GanymedePhase)(-1);renderedRevision=-1;busy=false;
        }
        private void OnDestroy()=>Exit();
    }
}
