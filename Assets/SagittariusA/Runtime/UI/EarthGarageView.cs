using System;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.UI
{
    public sealed class EarthGarageView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.035f, .05f, .075f, .98f);
        private static readonly Color Card = new Color(.075f, .095f, .13f, 1f);
        private static readonly Color Orange = new Color(1f, .43f, .04f, 1f);
        private static readonly Color Muted = new Color(.66f, .73f, .81f, 1f);
        private static readonly Color Green = new Color(.35f, .85f, .65f, 1f);
        private static readonly Color Red = new Color(1f, .48f, .42f, 1f);
        private static readonly string[] Categories = { "Launcher", "Nozzle", "Launch cover", "Flight engine", "Cargo bay", "Power system", "Cargo" };
        private EarthGameplaySession session;
        private GameFlowController flow;
        private GameScreen screen;
        private RectTransform body;
        private Text status, subtitle;
        private Button primary;
        private int category;
        private bool redraw;

        public static void Create(Transform parent, GameScreen screen)
        {
            var panel = Box(parent, screen + "Gameplay", .025f, .04f, .95f, .92f, Ink);
            var view = panel.gameObject.AddComponent<EarthGarageView>();
            view.flow = parent.GetComponentInParent<GameFlowController>();
            view.session = view.flow.GetComponent<EarthGameplaySession>();
            view.screen = screen;
            view.BuildShell();
            panel.gameObject.AddComponent<GameScreenView>().Configure(screen, null, screen, null, screen);
            view.session.Changed += view.RequestRefresh;
            panel.gameObject.SetActive(false);
        }
        private void OnEnable() { redraw = true; }
        private void OnDestroy() { if (session != null) session.Changed -= RequestRefresh; }
        private void RequestRefresh() { redraw = true; }
        private void LateUpdate() { if (redraw && session != null) { redraw = false; Render(); } }
        private void BuildShell()
        {
            Label(transform, "FAR SIGNAL  /  EARTH  /  THE MOON CALL", .03f, .92f, .94f, .045f, 18, Orange);
            string title = screen == GameScreen.Configuration ? "SHIP GARAGE" : screen == GameScreen.Review ? "LAUNCH CHECKLIST" :
                screen == GameScreen.Simulation ? "LAUNCH AUTHORIZED" : screen == GameScreen.Results ? "EARTH FLIGHT LOG" : "MOON — POLE-7 ARRIVAL";
            Label(transform, title, .03f, .84f, .94f, .075f, 38, Color.white);
            subtitle = Label(transform, screen == GameScreen.Configuration ? "Compare parts, fit the ship, and prepare the cargo for Pole-7." :
                "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex"), .03f, .79f, .94f, .045f, 19, Muted);
            body = Box(transform, "Content", .03f, .14f, .94f, .63f, Color.clear);
            status = Label(transform, "", .03f, .025f, .52f, .09f, 17, Muted);
            bool garage = screen == GameScreen.Configuration;
            string back = garage ? "BRIEFING" : "SHIP GARAGE";
            Action backAction = () => flow.Show(garage ? GameScreen.Briefing : GameScreen.Configuration);
            ActionButton(transform, back, .57f, .035f, .17f, .075f, backAction);
            primary = ActionButton(transform, "", .76f, .035f, .21f, .075f, Primary, Orange);
        }
        private void Primary()
        {
            if (screen == GameScreen.Configuration) flow.Show(GameScreen.Review);
            else if (screen == GameScreen.Review) { if (session.TryLaunch()) flow.Show(GameScreen.Simulation); }
            else if (screen == GameScreen.Simulation) flow.Show(GameScreen.Results);
            else if (screen == GameScreen.Results)
            {
                if (session.TransitCompleted) flow.Show(GameScreen.MoonArrival);
                else session.BeginActivity(EarthActivity.Transit);
            }
            else flow.Show(GameScreen.Results);
        }
        private void Render()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            string selectedName = selected != null && selected.transform.IsChildOf(body) ? selected.name : null;
            // Deactivate immediately; Destroy is deferred until the end of the frame.
            foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var a = session.Assessment;
            if (screen != GameScreen.Configuration)
                subtitle.text = "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex");
            status.text = session.Notice ?? "Draft game values · simulation adapter awaiting team review";
            if (screen == GameScreen.Configuration) RenderGarage(a);
            else RenderReport(a);
            primary.GetComponentInChildren<Text>().text = screen == GameScreen.Configuration ? "REVIEW LAUNCH" :
                screen == GameScreen.Review ? (session.Launched ? "VIEW LAUNCH" : "LAUNCH") :
                screen == GameScreen.Simulation ? "VIEW FLIGHT LOG" : screen == GameScreen.Results ?
                (session.TransitCompleted ? "MOON ARRIVAL" : "START TRANSIT") : "EARTH LOG";
            primary.interactable = !session.Busy && (screen != GameScreen.Review || a.CanLaunch) &&
                (screen != GameScreen.Results || session.TransitCompleted || session.CanRun(EarthActivity.Transit));
            if (EventSystem.current != null && (selectedName != null || selected == null || !selected.activeInHierarchy))
            {
                var buttons = GetComponentsInChildren<Button>().Where(b => b.interactable).ToArray();
                var focus = buttons.FirstOrDefault(b => b.name == selectedName) ?? buttons.FirstOrDefault();
                EventSystem.current.SetSelectedGameObject(focus == null ? null : focus.gameObject);
            }
        }
        private void RenderGarage(EarthAssessment a)
        {
            var b = session.Build;
            string[] presets = { "STARTER · $93M", "LASER · $114M", "FUEL · OVERWEIGHT" };
            for (int i = 0; i < 3; i++)
            { int preset = i; var button = ActionButton(body, presets[i], i * .215f, .91f, .205f, .085f, () => session.LoadPreset(preset)); button.interactable = !session.Launched && !session.Busy; }
            Label(body, "BUILD OPTIONS", 0, .83f, .19f, .06f, 16, Orange);
            for (int i = 0; i < Categories.Length; i++)
            {
                int index = i;
                ActionButton(body, Categories[i], 0, .72f - i * .102f, .19f, .087f, () => { category = index; redraw = true; }, category == i ? new Color(.36f, .20f, .11f) : Card);
            }
            Label(body, Categories[category].ToUpperInvariant() + "  /  SELECT A PART", .215f, .82f, .43f, .07f, 18, Orange);
            var options = session.Catalog.Group(category);
            float height = category == 6 ? .115f : .18f;
            for (int i = 0; i < options.Length; i++)
            {
                var part = options[i]; bool selected = category == 6 ? b.cargo.Contains(part.id) : b.parts[category] == part.id;
                var button = ActionButton(body, "", .215f, .80f - (i + 1) * (height + .016f), .43f, height,
                    () => { if (category == 6) session.ToggleCargo(part.id); else session.SelectPart(category, part.id); }, selected ? new Color(.23f, .16f, .10f) : Card);
                button.interactable = !session.Launched && !session.Busy;
                button.name = part.id;
                Label(button.transform, (selected ? "[SELECTED]  " : "") + part.id.ToUpperInvariant() + "  ·  " + part.name,
                    .025f, .54f, .95f, .40f, category == 6 ? 17 : 20, Color.white);
                Label(button.transform, Specs(part, category), .025f, .04f, .95f, .49f, category == 6 ? 16 : 18, Muted);
            }
            var dashboard = Box(body, "Resources", .67f, 0, .33f, 1, Card);
            Label(dashboard, "LIVE SHIP TOTALS", .06f, .91f, .88f, .07f, 20, Orange);
            Label(dashboard, $"Cost       {Money(a.costUsd)} / {Money(session.Catalog.budgetUsd)}\n" +
                $"Mass     {a.massKg:N0} / {a.payloadKg:N0} kg\n" +
                $"Margin   {a.MassMarginKg:N0} kg\nCargo    {a.slotsUsed} / {a.slotsAvailable} spaces\n" +
                $"Base load / output   3 / {a.supplyKw} kW\nBattery   {a.batteryKwh} kWh\nFuel         {a.fuelUnits} units", .06f, .47f, .88f, .43f, 21, Color.white);
            Label(dashboard, "CHECKS & NEXT ACTIONS · SCROLL", .06f, .39f, .88f, .065f, 16, Orange);
            Issues(dashboard, a, .04f, .025f, .92f, .36f);
        }
        private void RenderReport(EarthAssessment a)
        {
            var b = session.Build; var checks = session.Checks;
            var manifest = Box(body, "Manifest", 0, 0, .46f, 1, Card);
            Label(manifest, "SHIP MANIFEST", .05f, .90f, .9f, .075f, 22, Orange);
            string parts = string.Join("\n", Enumerable.Range(0, 6).Select(i => Categories[i] + "  ·  " + session.Catalog.Group(i).First(p => p.id == b.parts[i]).name));
            Label(manifest, parts, .05f, .49f, .9f, .39f, 21, Color.white);
            var pad = session.Catalog.landingMap.FirstOrDefault(p => p.id == checks.padId);
            Label(manifest, "CARGO: " + string.Join(", ", b.cargo.Select(id => session.Catalog.cargo.First(p => p.id == id).name)) +
                $"\n\n{Money(a.costUsd)} used · {a.MassMarginKg:N0} kg mass margin\n{a.batteryKwh} kWh battery · {a.fuelUnits} fuel units\n" +
                "Landing: " + (pad == null ? "Not planned" : pad.name + " · minute " + checks.arrivalMinute.ToString("0.#")), .05f, .06f, .9f, .41f, 21, Muted);
            var right = Box(body, "Checks", .48f, 0, .52f, 1, Card);
            if (screen == GameScreen.Review)
            {
                Label(right, a.CanLaunch ? "READY TO LAUNCH" : "LAUNCH ON HOLD", .05f, .90f, .9f, .07f, 24, a.CanLaunch ? Green : Red);
                Issues(right, a, .04f, .37f, .92f, .50f);
                string[] labels = { "PACK & LOCK CARGO", "TEST POWER", "PLAN LANDING" };
                for (int i = 0; i < 3; i++)
                {
                    var activity = (EarthActivity)i; bool available = session.CanRun(activity);
                    var button = ActionButton(right, labels[i] + (available ? "" : " · MODULE PENDING"), .05f, .25f - .10f * i, .9f, .085f, () => session.BeginActivity(activity));
                    button.interactable = available;
                }
            }
            else if (screen == GameScreen.MoonArrival)
            {
                Label(right, "TRANSIT COMPLETE", .06f, .79f, .88f, .12f, 28, Green);
                Label(right, "Khulan's map is ready.\n\nConfirm the landing plan before descent.\n\nMoon arrival is waiting for the team's playable scene. Your ship manifest and checks are preserved.", .06f, .15f, .88f, .60f, 24, Color.white);
            }
            else
            {
                Label(right, session.Launched ? "EARTH LAUNCH RECORDED" : "LAUNCH NOT COMPLETED", .06f, .79f, .88f, .12f, 26, session.Launched ? Green : Red);
                Label(right, "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex") +
                    (session.Launched ? ", your validated manifest is saved.\n\nNext: cabin transit, then Moon arrival.\n\n" : ", complete all launch checks first.\n\n") +
                    (session.TransitCompleted ? "Transit complete. Open Moon arrival." : session.CanRun(EarthActivity.Transit) ? "The transit module is connected." : "The transit minigame is awaiting the teammate's module."), .06f, .15f, .88f, .60f, 24, Color.white);
            }
        }
        private void Issues(Transform parent, EarthAssessment a, float x, float y, float w, float h)
        {
            var viewport = Box(parent, "CheckScroll", x, y, w, h, new Color(.035f, .045f, .065f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Box(viewport, "Rows", 0, 0, 1, 1, Color.clear);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(10, 10, 8, 8); layout.spacing = 8;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.scrollSensitivity = 32; scroll.movementType = ScrollRect.MovementType.Clamped;
            if (a.issues.Count == 0)
            {
                var text = Label(content, "[READY] All checks passed.", 0, 0, 1, 1, 20, Green);
                text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            }
            foreach (var issue in a.issues)
            {
                var button = ActionButton(content, (issue.blocksLaunch ? "[HOLD] " : "[NOTE] ") + issue.message, 0, 0, 1, 1,
                    () => { category = issue.category; flow.Show(GameScreen.Configuration); var garage = flow.GetComponentsInChildren<EarthGarageView>(true).First(v => v.screen == GameScreen.Configuration); garage.category = issue.category; garage.RequestRefresh(); });
                var label = button.GetComponentInChildren<Text>(); label.fontSize = 19; label.alignment = TextAnchor.MiddleLeft; label.color = issue.blocksLaunch ? Red : Orange;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = screen == GameScreen.Configuration ? 105 : 85;
            }
        }
        private static string Money(int value) => "$" + (value / 1000000f).ToString("0.##") + "M";
        private static string Specs(EarthPart p, int group)
        {
            string cost = Money(p.costUsd) + "  ·  ";
            switch (group)
            {
                case 0: return cost + $"{p.basePayloadKg:N0} kg base capacity · mounts {string.Join(" / ", p.nozzleMounts)}";
                case 1: return cost + $"mount {p.mount} · +{p.payloadGainKg} kg capacity";
                case 2: return cost + $"fits size {p.maxModuleSize} · −{p.payloadLossKg} kg capacity";
                case 3: return cost + $"{p.massKg:N0} kg · {p.fuelUnits} fuel" + (p.freeLandingCorrections > 0 ? " · 1 free landing correction" : "");
                case 4: return cost + $"{p.massKg} kg · size {p.size} · {p.slots} cargo spaces";
                case 5: return cost + $"{p.massKg} kg · {p.maxPowerKw} kW output · {p.batteryKwh} kWh storage";
                default: return cost + $"{p.massKg} kg · {p.slots} spaces" + (p.required ? " · REQUIRED" : p.extraPowerKw > 0 ? " · +3 kW when on" : p.extraFuelUnits > 0 ? " · +8 fuel" : " · optional");
            }
        }
        private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; Place(rect, x, y, w, h); go.GetComponent<Image>().color = color; return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(x + w, y + h); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place((RectTransform)go.transform, x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text;
            label.fontSize = size; label.color = color; label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false; label.raycastTarget = false;
            return label;
        }
        private static Button ActionButton(Transform parent, string text, float x, float y, float w, float h, Action action, Color? color = null)
        {
            var rect = Box(parent, text, x, y, w, h, color ?? Card); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            var label = Label(rect, text, .04f, .05f, .92f, .9f, 19, Color.white); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
