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
    // Interactive blockout: all mutations go through the same session as the garage.
    public sealed class EarthAssemblyView : MonoBehaviour
    {
        private static readonly string[] Groups = { "Launcher", "Nozzle", "Cover", "Engine", "Cargo bay", "Power", "Cargo" };
        private static readonly Vector3[] Positions = {
            new Vector3(0, -2.5f, 0), new Vector3(0, -4.1f, 0), new Vector3(0, 3.8f, 0),
            new Vector3(0, -.7f, 0), new Vector3(0, 1f, 0), new Vector3(2.5f, 1.8f, 0),
            new Vector3(-2.6f, 1f, 0)
        };
        private static readonly Color Accent = new Color(1, .43f, .04f);
        private EarthGameplaySession session;
        private EarthOverlay overlay;
        private RectTransform controls, viewport;
        private Text totals, feedback, candidateLabel;
        private RawImage display;
        private GameObject studio, ship;
        private Camera previewCamera;
        private RenderTexture texture;
        private Material normalMaterial, selectedMaterial, cargoMaterial;
        private readonly List<(Collider collider, int group)> sockets = new List<(Collider, int)>();
        private readonly List<(Text label, int group)> socketLabels = new List<(Text, int)>();
        private int group;
        private string candidate;
        private bool dirty;
        private float yaw = 22, pitch = 8, zoom = 6.4f;

        public static void Open(Transform owner, EarthGameplaySession session)
        {
            var overlay = EarthOverlay.Open(owner, "Earth 3D assembly");
            var view = overlay.gameObject.AddComponent<EarthAssemblyView>();
            view.overlay = overlay; view.session = session;
            view.candidate = session.Build.parts[0];
            view.CreateInterface(); view.CreateStudio();
            session.Changed += view.OnChanged;
            view.Refresh();
        }
        private void OnChanged() { dirty = true; }
        private void LateUpdate()
        {
            if (dirty) { dirty = false; Refresh(); }
            if (previewCamera == null) return;
            previewCamera.aspect = viewport.rect.width / Mathf.Max(1, viewport.rect.height);
            foreach (var entry in socketLabels)
            {
                var p = previewCamera.WorldToViewportPoint(ship.transform.TransformPoint(Positions[entry.group]));
                var rect = entry.label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(p.x, p.y);
            }
        }
        private void CreateInterface()
        {
            Label(transform, "EARTH / SNAP-FIT ASSEMBLY", .025f, .92f, .7f, .05f, 20, Accent);
            Label(transform, "BUILD YOUR SHIP", .025f, .845f, .6f, .07f, 38, Color.white);
            var close = ActionButton(transform, "BACK TO GARAGE", .78f, .89f, .195f, .07f, overlay.Close, Accent);
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
            for (int i = 0; i < Groups.Length; i++)
            {
                int index = i;
                ActionButton(transform, Groups[i], .025f + i * .137f, .77f, .129f, .06f, () => ChooseGroup(index), new Color(.13f, .16f, .21f));
            }
            controls = Box(transform, "Part options", .025f, .16f, .26f, .58f, new Color(.055f, .07f, .10f));
            var previewObject = new GameObject("3D viewport", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(transform, false);
            viewport = (RectTransform)previewObject.transform;
            viewport.anchorMin = new Vector2(.30f, .16f); viewport.anchorMax = new Vector2(.74f, .74f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            display = previewObject.GetComponent<RawImage>();
            previewObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<EarthAssemblyInput>().View = this;
            ActionButton(transform, "ROTATE LEFT", .305f, .695f, .105f, .035f, () => Orbit(-70, 0));
            ActionButton(transform, "ROTATE RIGHT", .412f, .695f, .105f, .035f, () => Orbit(70, 0));
            ActionButton(transform, "ZOOM IN", .519f, .695f, .105f, .035f, () => Zoom(1));
            ActionButton(transform, "ZOOM OUT", .626f, .695f, .105f, .035f, () => Zoom(-1));
            Label(transform, "Drag to rotate · Scroll to zoom · Click the orange attachment point to fit", .30f, .105f, .44f, .045f, 17, Color.white);
            Label(transform, "PROTOTYPE SHAPES / EXPLODED VIEW", .30f, .065f, .44f, .035f, 15, new Color(.65f, .72f, .8f));
            var dash = Box(transform, "Fitted ship totals", .755f, .16f, .22f, .58f, new Color(.055f, .07f, .10f));
            Label(dash, "FITTED SHIP / LIVE TOTALS", .06f, .87f, .88f, .09f, 20, Accent);
            totals = Label(dash, "", .06f, .40f, .88f, .46f, 24, Color.white);
            feedback = Label(dash, "", .06f, .04f, .88f, .34f, 20, Color.white);
            candidateLabel = Label(transform, "", .025f, .035f, .26f, .10f, 18, Color.white);
        }
        private void ChooseGroup(int index)
        {
            group = index;
            candidate = group == 6 ? session.Catalog.cargo[0].id : session.Build.parts[group];
            Refresh();
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
            texture = new RenderTexture(960, 960, 24) { name = "Earth assembly preview" };
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
            var focused = EventSystem.current?.currentSelectedGameObject;
            string focusName = focused != null && focused.transform.IsChildOf(controls) ? focused.name : null;
            foreach (Transform child in controls) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var build = session.Build;
            var parts = session.Catalog.Group(group);
            float row = group == 6 ? .13f : .185f;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                bool fitted = group == 6 ? build.cargo.Contains(part.id) : build.parts[group] == part.id;
                float y = .96f - (i + 1) * row;
                string label = (fitted ? "[FITTED] " : "") + part.id.ToUpperInvariant() + " / " + part.name + "\n" + Money(part.costUsd);
                var choose = ActionButton(controls, label, .04f, y, .71f, row - .015f,
                    () => { candidate = part.id; Refresh(); },
                    part.id == candidate ? new Color(.36f, .20f, .10f) : new Color(.10f, .13f, .18f));
                choose.GetComponentInChildren<Text>().fontSize = 18;
                choose.name = "Choose-" + part.id;
                ActionButton(controls, "INFO", .77f, y, .19f, row - .015f,
                    () => EarthPartDescription.Open(transform, part, group), new Color(.16f, .20f, .27f));
            }
            var fit = ActionButton(controls, "FIT TO MATCHING POINT", .04f, .055f, group == 6 ? .59f : .92f, .085f, () => Fit(group), Accent);
            fit.GetComponentInChildren<Text>().fontSize = 17;
            fit.interactable = !session.Busy && !session.Launched;
            if (group == 6)
            {
                var remove = ActionButton(controls, "REMOVE", .65f, .055f, .31f, .085f, () => session.SetCargo(candidate, false), new Color(.35f, .12f, .10f));
                remove.interactable = !session.Busy && !session.Launched && build.cargo.Contains(candidate);
                remove.GetComponentInChildren<Text>().fontSize = 16;
            }
            var p = parts.First(x => x.id == candidate);
            candidateLabel.text = "Selected: " + p.name + "\nChoose a part, then fit it. Browsing does not change totals.";
            var a = session.Assessment;
            totals.text = $"Cost  {Money(a.costUsd)} / {Money(session.Catalog.budgetUsd)}\n\nMass  {a.massKg:N0} kg\nCapacity  {a.payloadKg:N0} kg\nMargin  {a.MassMarginKg:N0} kg\n\nCargo  {a.slotsUsed} / {a.slotsAvailable}\nPower  {session.Catalog.busPowerKw} / {a.supplyKw} kW\nBattery  {a.batteryKwh} kWh\nFuel  {a.fuelUnits} units";
            var hardware = a.issues.FirstOrDefault(x => x.code != "cargo-check" && x.code != "power-check" && x.code != "landing-check");
            feedback.text = hardware != null ? hardware.message : "Choose a part and click its highlighted attachment point.\n\nCargo locks and launch checks are completed separately.";
            feedback.color = hardware?.blocksLaunch == true ? new Color(1, .45f, .35f) : Color.white;
            RebuildShip(build);
            if (focusName != null && EventSystem.current != null)
            {
                var buttons = controls.GetComponentsInChildren<Button>();
                var next = buttons.FirstOrDefault(b => b.name == focusName && b.interactable) ?? buttons.FirstOrDefault(b => b.interactable);
                EventSystem.current.SetSelectedGameObject(next?.gameObject);
            }
        }
        private void RebuildShip(EarthBuild build)
        {
            sockets.Clear();
            foreach (Transform child in ship.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int i = 0; i < 7; i++)
            {
                var socket = new GameObject(Groups[i] + " attachment");
                socket.transform.SetParent(ship.transform, false); socket.transform.localPosition = Positions[i];
                if (i == 6)
                {
                    // Cargo is only staged here; packing/locking stays with the cargo minigame.
                    int count = Math.Max(1, build.cargo.Count);
                    for (int j = 0; j < count; j++)
                        Primitive(socket.transform, PrimitiveType.Cube, new Vector3((j % 2) * .62f - .3f, (j / 2) * .62f, 0), Vector3.one * .52f, i);
                    continue;
                }
                var part = session.Catalog.Group(i).First(x => x.id == build.parts[i]);
                // Optional final artwork: Resources/EarthParts/<stable part ID>. Prefab origin = attachment.
                var prefab = Resources.Load<GameObject>("EarthParts/" + part.id);
                if (prefab != null)
                {
                    var visual = Instantiate(prefab, socket.transform, false);
                    foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    // Dedicated click target keeps artistic collider choices out of assembly interaction.
                    var target = socket.AddComponent<BoxCollider>(); target.size = Vector3.one * 1.7f;
                    sockets.Add((target, i));
                    continue;
                }
                float variant = Array.FindIndex(session.Catalog.Group(i), x => x.id == part.id);
                switch (i)
                {
                    case 0: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.25f + variant * .17f, 1.15f, 1.25f + variant * .17f), i); break;
                    case 1: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.65f + variant * .12f, .28f + variant * .05f, .65f + variant * .12f), i); break;
                    case 2: Primitive(socket.transform, PrimitiveType.Capsule, Vector3.zero, new Vector3(1.2f + variant * .12f, .65f, 1.2f + variant * .12f), i); break;
                    case 3: Primitive(socket.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1, .35f + variant * .06f, 1), i); break;
                    case 4: Primitive(socket.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.1f + part.size * .09f, 1.2f, 1.1f), i); break;
                    case 5:
                        Primitive(socket.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.65f, .8f, .6f), i);
                        Primitive(socket.transform, PrimitiveType.Cube, new Vector3(.8f, 0, 0), new Vector3(.8f, 1.5f + variant * .2f, .12f), i); break;
                }
            }
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
        internal void Zoom(float amount) { zoom = Mathf.Clamp(zoom - amount * .4f, 4.5f, 10); Orbit(0, 0); }
        internal void Pick(PointerEventData input)
        {
            Physics.SyncTransforms();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, input.position, input.pressEventCamera, out var local)) return;
            var rect = viewport.rect;
            var ray = previewCamera.ViewportPointToRay(new Vector3((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
            float nearest = float.MaxValue; int picked = -1;
            foreach (var target in sockets)
                if (target.collider.Raycast(ray, out var hit, 60) && hit.distance < nearest) { picked = target.group; nearest = hit.distance; }
            if (picked >= 0) Fit(picked);
        }
        private void Fit(int socket)
        {
            if (session.Busy || session.Launched) { feedback.text = "Assembly is locked during an activity or after launch."; return; }
            if (socket != group) { feedback.text = "That is the " + Groups[socket] + " point. Fit this part to the highlighted " + Groups[group] + " point."; return; }
            if (group == 6) session.SetCargo(candidate, true); else session.SelectPart(group, candidate);
            // Physics sync makes another immediate socket click use the fitted geometry.
            dirty = false; Refresh(); Physics.SyncTransforms();
            feedback.text = candidate.ToUpperInvariant() + " fitted. Live totals updated.\n\n" + feedback.text;
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
