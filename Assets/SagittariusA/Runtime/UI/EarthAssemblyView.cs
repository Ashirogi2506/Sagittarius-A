using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.UI
{
    // The sole configuration screen. Selecting a part immediately updates the saved ship.
    public sealed class EarthAssemblyView : MonoBehaviour
    {
        private static readonly string[] Groups = { "Launcher", "Nozzle", "Cover", "Engine", "Cargo bay", "Power", "Cargo" };
        private readonly Vector3[] positions = new Vector3[7];
        private static readonly Color Accent = new Color(1, .43f, .04f);
        private EarthGameplaySession session;
        private GameFlowController flow;
        private RectTransform controls, viewport, presets;
        private Text totals, feedback, candidateLabel;
        private Button packCargo, exteriorToggle;
        private bool showFlightExterior = true;
        private RawImage display;
        private GameObject studio, ship;
        private Camera previewCamera;
        private RenderTexture texture;
        private Material normalMaterial, selectedMaterial, cargoMaterial;
        private readonly List<(Collider collider, int group)> sockets = new List<(Collider, int)>();
        private readonly List<(Text label, int group)> socketLabels = new List<(Text, int)>();
        private int group;
        private bool dirty;
        private float yaw = 22, pitch = 8, zoom = 3.8f;

        public static void Create(Transform parent)
        {
            var root = Box(parent, "ShipAssembly", 0, 0, 1, 1, new Color(.025f, .035f, .055f));
            var view = root.gameObject.AddComponent<EarthAssemblyView>();
            view.flow = parent.GetComponentInParent<GameFlowController>();
            view.session = view.flow.GetComponent<EarthGameplaySession>();
            view.CreateInterface(); view.CreateStudio();
            view.session.Changed += view.OnChanged;
            view.Refresh();
            root.gameObject.AddComponent<GameScreenView>().Configure(GameScreen.Configuration, null, GameScreen.Configuration, null, GameScreen.Configuration);
            root.gameObject.SetActive(false);
        }
        private void OnEnable() { if (studio != null) studio.SetActive(true); dirty = true; }
        private void OnDisable() { if (studio != null) studio.SetActive(false); }
        private void OnChanged() { dirty = true; }
        private void LateUpdate()
        {
            if (dirty) { dirty = false; Refresh(); }
            if (previewCamera == null) return;
            previewCamera.aspect = viewport.rect.width / Mathf.Max(1, viewport.rect.height);
            foreach (var entry in socketLabels)
            {
                entry.label.gameObject.SetActive(!showFlightExterior && entry.group == group);
                var p = previewCamera.WorldToViewportPoint(ship.transform.TransformPoint(positions[entry.group]));
                var rect = entry.label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(p.x, p.y);
            }
        }
        private void CreateInterface()
        {
            Label(transform, "EARTH / SHIP ASSEMBLY", .025f, .945f, .6f, .035f, 18, Accent);
            Label(transform, "CHOOSE YOUR SHIP", .025f, .875f, .6f, .065f, 38, Color.white);
            ActionButton(transform, "BRIEFING", .65f, .90f, .12f, .065f, () => flow.Show(GameScreen.Briefing));
            ActionButton(transform, "REVIEW LAUNCH", .78f, .90f, .195f, .065f, () => flow.Show(GameScreen.Review), Accent);
            presets = Box(transform, "Ready-made ships", .025f, .785f, .95f, .085f, Color.clear);
            for (int i = 0; i < Groups.Length; i++)
            {
                int index = i;
                ActionButton(transform, Groups[i], .025f + i * .137f, .71f, .129f, .055f, () => SelectCategory(index), new Color(.13f, .16f, .21f));
            }
            controls = Box(transform, "Part options", .025f, .16f, .26f, .52f, new Color(.055f, .07f, .10f));
            var previewObject = new GameObject("3D viewport", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(transform, false);
            viewport = (RectTransform)previewObject.transform;
            viewport.anchorMin = new Vector2(.30f, .16f); viewport.anchorMax = new Vector2(.74f, .68f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            display = previewObject.GetComponent<RawImage>();
            previewObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<EarthAssemblyInput>().View = this;
            ActionButton(transform, "ROTATE LEFT", .305f, .635f, .105f, .035f, () => Orbit(-70, 0));
            ActionButton(transform, "ROTATE RIGHT", .412f, .635f, .105f, .035f, () => Orbit(70, 0));
            ActionButton(transform, "ZOOM IN", .519f, .635f, .105f, .035f, () => Zoom(1));
            ActionButton(transform, "ZOOM OUT", .626f, .635f, .105f, .035f, () => Zoom(-1));
            Label(transform, "Click a part to swap instantly · Drag to rotate · Scroll to zoom", .30f, .105f, .44f, .045f, 17, Color.white);
            exteriorToggle=ActionButton(transform,"SHOW MODULES",.30f,.065f,.44f,.035f,()=>{showFlightExterior=!showFlightExterior;dirty=true;});
            var dash = Box(transform, "Fitted ship totals", .755f, .16f, .22f, .52f, new Color(.055f, .07f, .10f));
            Label(dash, "FITTED SHIP / LIVE TOTALS", .06f, .87f, .88f, .09f, 20, Accent);
            totals = Label(dash, "", .06f, .40f, .88f, .46f, 22, Color.white);
            feedback = Label(dash, "", .06f, .04f, .88f, .34f, 20, Color.white);
            candidateLabel = Label(transform, "", .025f, .035f, .26f, .10f, 18, Color.white);
            packCargo = ActionButton(transform,"PACK & LOCK CARGO",.755f,.075f,.22f,.06f,()=>session.BeginExplainedActivity(EarthActivity.Cargo),Accent);
        }
        public void SelectCategory(int index)
        {
            if (index < 0 || index > 6) return;
            group = index; showFlightExterior=false; dirty = true;
        }
        private void CreateStudio()
        {
            studio = new GameObject("Earth assembly preview studio");
            studio.transform.position = new Vector3(10000, 0, 0);
            ship = new GameObject("Attachment points");
            ship.transform.SetParent(studio.transform, false);
            var cameraObject = new GameObject("Assembly preview camera", typeof(Camera));
            cameraObject.transform.SetParent(studio.transform, false);
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.035f, .05f, .075f);
            previewCamera.cullingMask = 1 << 31;
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 60;
            cameraObject.transform.localPosition = new Vector3(0, 0, -20);
            texture = new RenderTexture(1536, 1536, 24) { name = "Earth assembly preview", antiAliasing = 4 };
            texture.Create(); previewCamera.targetTexture = texture; display.texture = texture;
            var lightObject = new GameObject("Assembly light", typeof(Light));
            lightObject.transform.SetParent(studio.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35, -35, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 31;
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            normalMaterial = new Material(shader) { color = new Color(.65f, .75f, .85f) };
            selectedMaterial = new Material(shader) { color = Accent };
            cargoMaterial = new Material(shader) { color = new Color(.25f, .65f, .58f) };
            Orbit(0, 0);
            for (int i = 0; i < Groups.Length; i++)
            {
                var label = Label(viewport, Groups[i], 0, 0, 0, 0, 16, Color.white);
                label.alignment = TextAnchor.MiddleCenter;
                label.rectTransform.sizeDelta = new Vector2(160, 32);
                label.rectTransform.anchoredPosition = new Vector2(0, -48);
                socketLabels.Add((label, i));
            }
        }
        private void Refresh()
        {
            if(packCargo!=null){packCargo.interactable=session.CanRun(EarthActivity.Cargo);packCargo.GetComponentInChildren<Text>().text=session.Checks.placements.Count>0&&session.Checks.placements.All(p=>p.locked)?"RECHECK CARGO LOCKS":"PACK & LOCK CARGO";}
            var focused = EventSystem.current?.currentSelectedGameObject;
            string focusName = focused != null && (focused.transform.IsChildOf(controls) || focused.transform.IsChildOf(presets)) ? focused.name : null;
            foreach (Transform child in controls) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var build = session.Build;
            foreach (Transform child in presets) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int i = 0; i < EarthReadyBuild.All.Count; i++)
            {
                var ready = EarthReadyBuild.All[i];
                var button = ActionButton(presets, ready.Name + "\n" + ready.Summary,
                    i * .252f, 0, .244f, 1, () => session.LoadReadyBuild(ready.Id),
                    ready.Matches(build) ? new Color(.36f, .20f, .10f) : new Color(.13f, .16f, .21f));
                button.name = "Ready-" + ready.Id;
                button.interactable = !session.Busy && !session.Launched;
                button.GetComponentInChildren<Text>().fontSize = 18;
            }
            var parts = session.Catalog.Group(group);
            float row = group == 6 ? .13f : .185f;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                bool fitted = group == 6 ? build.cargo.Contains(part.id) : build.parts[group] == part.id;
                float y = .96f - (i + 1) * row;
                string label = (fitted ? "[FITTED] " : "") + part.id.ToUpperInvariant() + " / " + part.name + "\n" + Money(part.costUsd);
                var choose = ActionButton(controls, label, .04f, y, .71f, row - .015f,
                    () => { if (group == 6) session.ToggleCargo(part.id); else session.SelectPart(group, part.id); },
                    fitted ? new Color(.36f, .20f, .10f) : new Color(.10f, .13f, .18f));
                choose.GetComponentInChildren<Text>().fontSize = 18;
                choose.name = "Choose-" + part.id;
                choose.interactable = !session.Busy && !session.Launched;
                ActionButton(controls, "INFO", .77f, y, .19f, row - .015f,
                    () => EarthPartDescription.Open(transform, part, group), new Color(.16f, .20f, .27f));
            }
            Label(controls, group == 6 ? "Click to add or remove cargo." : "Click a part to replace it immediately.",
                .04f, .045f, .92f, .10f, 18, Color.white);
            var matching = EarthReadyBuild.All.FirstOrDefault(p => p.Matches(build));
            candidateLabel.text = matching == null ? "CUSTOM BUILD\nLive model and totals follow your choices." :
                matching.Name + "\n" + matching.Summary;
            var a = session.Assessment;
            totals.text = $"Cost  {Money(a.costUsd)} / {Money(session.Catalog.budgetUsd)}\n\nMass  {a.massKg:N0} kg\nCapacity  {a.payloadKg:N0} kg\nMargin  {a.MassMarginKg:N0} kg\n\nCargo  {a.slotsUsed} / {a.slotsAvailable}\nPower  {session.Catalog.busPowerKw} / {a.supplyKw} kW\nBattery  {a.batteryKwh} kWh\nFuel  {a.fuelUnits} units";
            var hardware = a.issues.FirstOrDefault(x => x.code != "cargo-check" && x.code != "power-check" && x.code != "landing-check");
            feedback.text = hardware != null ? hardware.message : "Hardware ready.\n\nSecure cargo before launch. Power and landing checks happen in flight.";
            feedback.color = hardware?.blocksLaunch == true ? new Color(1, .45f, .35f) : Color.white;
            RebuildShip(build);
            if (focusName != null && EventSystem.current != null)
            {
                var buttons = controls.GetComponentsInChildren<Button>().Concat(presets.GetComponentsInChildren<Button>()).ToArray();
                var next = buttons.FirstOrDefault(b => b.name == focusName && b.interactable) ?? buttons.FirstOrDefault(b => b.interactable);
                EventSystem.current.SetSelectedGameObject(next?.gameObject);
            }
        }
        private void RebuildShip(EarthBuild build)
        {
            sockets.Clear();
            foreach (Transform child in ship.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            // Measure with no preview rotation so each part keeps the model's authored proportions.
            Quaternion previousRotation = ship.transform.localRotation;
            ship.transform.localRotation = Quaternion.identity;
            exteriorToggle.GetComponentInChildren<Text>().text=showFlightExterior?"SHOW CONFIGURABLE MODULES":"SHOW FLIGHT EXTERIOR";
            if(showFlightExterior)
            {
                var exterior=ExpeditionShipVisual.Create(ship.transform,ship.transform.position+Vector3.down*3.2f,6.4f,"Ships/LaunchStack");
                if(exterior!=null)
                {
                    foreach(var t in exterior.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    foreach(var c in exterior.GetComponentsInChildren<Collider>())c.enabled=false;
                    ship.transform.localRotation=previousRotation;
                    return;
                }
            }
            var partsInView = new Transform[6];
            var localBounds = new Bounds[6];
            for (int i = 0; i < 6; i++)
            {
                var socket = new GameObject(Groups[i] + " attachment");
                socket.transform.SetParent(ship.transform, false);
                partsInView[i] = socket.transform;
                var part = session.Catalog.Group(i).First(x => x.id == build.parts[i]);
                var prefab = Resources.Load<GameObject>("EarthParts/" + part.id);
                if (prefab != null)
                {
                    var visual = Instantiate(prefab, socket.transform, false);
                    if (i == 4)
                    {
                        // A cylindrical cargo container, sized from the selected bay model.
                        var bay = MeasureLocalBounds(socket.transform);
                        visual.SetActive(false); Destroy(visual);
                        float width = Mathf.Max(.2f, Mathf.Max(bay.size.x, bay.size.z));
                        float height = Mathf.Max(.2f, bay.size.y);
                        Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(width, height * .5f, width), i);
                        Primitive(socket.transform, PrimitiveType.Cylinder, new Vector3(0, height * .46f, 0), new Vector3(width * 1.05f, height * .035f, width * 1.05f), i);
                        Primitive(socket.transform, PrimitiveType.Cylinder, new Vector3(0, -height * .46f, 0), new Vector3(width * 1.05f, height * .035f, width * 1.05f), i);
                    }
                    else AddArtworkTarget(socket.transform, visual, i);
                }
                else
                {
                    float variant = Array.FindIndex(session.Catalog.Group(i), x => x.id == part.id);
                    switch (i)
                    {
                        case 0: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.46f, 1.7f, .46f), i); break;
                        case 1: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.16f, .1f, .16f), i); break;
                        case 2: Primitive(socket.transform, PrimitiveType.Capsule, Vector3.zero, new Vector3(.27f, .13f, .27f), i); break;
                        case 3: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.4f, .19f, .4f), i); break;
                        case 4: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.24f + part.size * .03f, .11f, .24f + part.size * .03f), i); break;
                        case 5: Primitive(socket.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.5f, .12f + variant * .02f, .4f), i); break;
                    }
                }
                localBounds[i] = MeasureLocalBounds(socket.transform);
            }
            float cursor = 0;
            foreach (int index in new[] { 1, 0, 3, 4, 5, 2 })
            {
                var bounds = localBounds[index];
                partsInView[index].localPosition = new Vector3(0, cursor - bounds.min.y, 0);
                cursor += bounds.size.y + .06f;
            }
            float centerY = (cursor - .06f) * .5f;
            for (int i = 0; i < 6; i++)
            {
                partsInView[i].localPosition -= new Vector3(0, centerY, 0);
                positions[i] = partsInView[i].localPosition + localBounds[i].center;
            }
            // Cargo was exported eight times larger for the old exploded view.
            // Restore the common art scale and stage it next to the cargo cylinder.
            var cargo = new GameObject("Cargo attachment"); cargo.transform.SetParent(ship.transform, false);
            cargo.transform.localPosition = positions[4] + new Vector3(localBounds[4].extents.x + .24f, 0, 0);
            positions[6] = cargo.transform.localPosition;
            for (int j = 0; j < build.cargo.Count; j++)
            {
                var id = build.cargo[j];
                var holder = new GameObject(id + " staged cargo"); holder.transform.SetParent(cargo.transform, false);
                holder.transform.localPosition = new Vector3((j % 2) * .19f, (j / 2) * .16f - .08f, 0);
                var prefab = Resources.Load<GameObject>("EarthParts/" + id);
                if (prefab == null) Primitive(holder.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one * .08f, 6);
                else
                {
                    var visual = Instantiate(prefab, holder.transform, false);
                    visual.transform.localScale *= .125f;
                    AddArtworkTarget(holder.transform, visual, 6);
                }
            }
            ship.transform.localRotation = previousRotation;
        }
        private static Bounds MeasureLocalBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * .2f);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            bounds.center = root.InverseTransformPoint(bounds.center);
            return bounds;
        }
        private void AddArtworkTarget(Transform root, GameObject visual, int socket)
        {
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var target = root.gameObject.AddComponent<BoxCollider>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                target.center = root.InverseTransformPoint(bounds.center);
                var worldSize = bounds.size;
                target.size = new Vector3(
                    Mathf.Max(.35f, worldSize.x / Mathf.Max(.001f, root.lossyScale.x)),
                    Mathf.Max(.35f, worldSize.y / Mathf.Max(.001f, root.lossyScale.y)),
                    Mathf.Max(.35f, worldSize.z / Mathf.Max(.001f, root.lossyScale.z)));
            }
            else target.size = Vector3.one;
            sockets.Add((target, socket));
        }
        private void Primitive(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, int socket)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.layer = 31;
            go.GetComponent<Renderer>().sharedMaterial = socket == group ? selectedMaterial : socket == 6 ? cargoMaterial : normalMaterial;
            sockets.Add((go.GetComponent<Collider>(), socket));
        }
        internal void Orbit(float dx, float dy)
        {
            yaw += dx * .35f; pitch = Mathf.Clamp(pitch - dy * .25f, -45, 45);
            ship.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            previewCamera.orthographicSize = zoom;
        }
        internal void Zoom(float amount) { zoom = Mathf.Clamp(zoom - amount * .3f, 2.6f, 10); Orbit(0, 0); }
        internal void Pick(PointerEventData input)
        {
            Physics.SyncTransforms();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, input.position, input.pressEventCamera, out var local)) return;
            var rect = viewport.rect;
            var ray = previewCamera.ViewportPointToRay(new Vector3((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
            float nearest = float.MaxValue; int picked = -1;
            foreach (var target in sockets)
                if (target.collider.Raycast(ray, out var hit, 60) && hit.distance < nearest) { picked = target.group; nearest = hit.distance; }
            if (picked >= 0) SelectCategory(picked);
        }
        private void OnDestroy()
        {
            if (session != null) session.Changed -= OnChanged;
            if (studio != null) { studio.SetActive(false); Destroy(studio); }
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (normalMaterial != null) Destroy(normalMaterial);
            if (selectedMaterial != null) Destroy(selectedMaterial);
            if (cargoMaterial != null) Destroy(cargoMaterial);
        }
    }

    public sealed class EarthAssemblyInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerClickHandler, IScrollHandler
    {
        public EarthAssemblyView View;
        private bool dragged;
        public void OnPointerDown(PointerEventData e) { dragged = false; }
        public void OnBeginDrag(PointerEventData e) { dragged = true; }
        public void OnDrag(PointerEventData e) { View.Orbit(e.delta.x, e.delta.y); }
        public void OnPointerClick(PointerEventData e) { if (!dragged) View.Pick(e); }
        public void OnScroll(PointerEventData e) { View.Zoom(e.scrollDelta.y); }
    }
}
