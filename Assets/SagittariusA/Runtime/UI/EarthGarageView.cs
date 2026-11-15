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
        private bool redraw;

        public static void Create(Transform parent, GameScreen screen)
        {
            if (screen == GameScreen.Configuration) { EarthAssemblyView.Create(parent); return; }
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
            string title = screen == GameScreen.Review ? "LAUNCH CHECKLIST" :
                screen == GameScreen.Simulation ? "LAUNCH AUTHORIZED" : screen == GameScreen.Results ? "IN-FLIGHT CHECKS" : "MOON — POLE-7 ARRIVAL";
            Label(transform, title, .03f, .84f, .94f, .075f, 38, Color.white);
            subtitle = Label(transform, "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex"), .03f, .79f, .94f, .045f, 19, Muted);
            body = Box(transform, "Content", .03f, .14f, .94f, .63f, Color.clear);
            status = Label(transform, "", .03f, .025f, .52f, .09f, 17, Muted);
            ActionButton(transform, "SHIP ASSEMBLY", .57f, .035f, .17f, .075f, () => flow.Show(GameScreen.Configuration));
            primary = ActionButton(transform, "", .76f, .035f, .21f, .075f, Primary, Orange);
        }
        private void Primary()
        {
            if (screen == GameScreen.Review) { if (session.TryLaunch()) flow.Show(GameScreen.Simulation); }
            else if (screen == GameScreen.Simulation) flow.Show(GameScreen.Results);
            else if (screen == GameScreen.Results)
            {
                if (session.FlightChecksCompleted) flow.Show(GameScreen.MoonArrival);
            }
            else
            {
                var moon = flow.GetComponent<MoonPlayableExperience>() ?? flow.gameObject.AddComponent<MoonPlayableExperience>();
                moon.StartCampaign();
            }
        }
        private void Render()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            string selectedName = selected != null && selected.transform.IsChildOf(body) ? selected.name : null;
            // Deactivate immediately; Destroy is deferred until the end of the frame.
            foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var a = session.Assessment;
            subtitle.text = "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex");
            status.text = session.Notice ?? "Draft game values · simulation adapter awaiting team review";
            RenderReport(a);
            primary.GetComponentInChildren<Text>().text = screen == GameScreen.Review ? (session.Launched ? "VIEW LAUNCH" : "LAUNCH") :
                screen == GameScreen.Simulation ? "VIEW FLIGHT LOG" : screen == GameScreen.Results ?
                (session.FlightChecksCompleted ? "MOON ARRIVAL" : "COMPLETE FLIGHT CHECKS") : "LAND ON MOON";
            primary.interactable = !session.Busy && (screen != GameScreen.Review || session.CanLaunchFromEarth) &&
                (screen != GameScreen.Results || session.FlightChecksCompleted) &&
                (screen != GameScreen.MoonArrival || session.Checks.padId == "main");
            if (EventSystem.current != null && (selectedName != null || selected == null || !selected.activeInHierarchy))
            {
                var buttons = GetComponentsInChildren<Button>().Where(b => b.interactable).ToArray();
                var focus = buttons.FirstOrDefault(b => b.name == selectedName) ?? buttons.FirstOrDefault();
                EventSystem.current.SetSelectedGameObject(focus == null ? null : focus.gameObject);
            }
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
                bool ready = session.CanLaunchFromEarth;
                Label(right, ready ? "READY TO LAUNCH" : "LAUNCH ON HOLD", .05f, .90f, .9f, .07f, 24, ready ? Green : Red);
                Issues(right, a, .04f, .39f, .92f, .48f, issue => !IsInFlightIssue(issue));
                Label(right, "Power and landing checks happen after launch, while flying.", .05f, .27f, .9f, .09f, 18, Muted);
                bool available = session.CanRun(EarthActivity.Cargo);
                var button = ActionButton(right, "PACK & LOCK CARGO" + (available ? "" : " · MODULE PENDING"), .05f, .13f, .9f, .09f, () => session.BeginActivity(EarthActivity.Cargo));
                button.interactable = available;
            }
            else if (screen == GameScreen.MoonArrival)
            {
                Label(right, "FLIGHT PHASE COMPLETE", .06f, .79f, .88f, .12f, 28, Green);
                string moonText = checks.padId == "main"
                    ? "Khulan's map is ready.\n\nContinue to the Moon, confirm the landing plan, and prepare the cart for Pole-7. Your ship manifest and checks are preserved."
                    : "The Ridge landing scene is not connected yet. Your landing plan and ship manifest are preserved.";
                Label(right, moonText, .06f, .15f, .88f, .60f, 24, Color.white);
            }
            else if (screen == GameScreen.Results)
            {
                Label(right, session.FlightChecksCompleted ? "FLIGHT CHECKS COMPLETE" : "EARTH ORBIT / MOON TRANSIT", .05f, .90f, .9f, .07f, 24, session.FlightChecksCompleted ? Green : Orange);
                Issues(right, a, .04f, .39f, .92f, .48f, IsInFlightIssue);
                var power = ActionButton(right, "TEST POWER", .05f, .25f, .9f, .09f, () => session.BeginActivity(EarthActivity.Power));
                power.interactable = session.CanRun(EarthActivity.Power);
                var landing = ActionButton(right, "PLAN LANDING", .05f, .13f, .9f, .09f, () => session.BeginActivity(EarthActivity.Landing));
                landing.interactable = session.CanRun(EarthActivity.Landing);
            }
            else
            {
                Label(right, session.Launched ? "EARTH LAUNCH RECORDED" : "LAUNCH NOT COMPLETED", .06f, .79f, .88f, .12f, 26, session.Launched ? Green : Red);
                Label(right, "Commander " + PlayerPrefs.GetString("FarSignal.CommanderName", "Alex") +
                    (session.Launched ? ", your launch manifest is saved.\n\nContinue to the in-flight power and landing checks." : ", secure the cargo and resolve any hardware holds first."), .06f, .15f, .88f, .60f, 24, Color.white);
            }
        }
        private static bool IsInFlightIssue(EarthIssue issue) => issue.code == "power-check" || issue.code == "landing-check";
        private void Issues(Transform parent, EarthAssessment a, float x, float y, float w, float h, Func<EarthIssue, bool> filter = null)
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
            var issues = filter == null ? a.issues.ToArray() : a.issues.Where(filter).ToArray();
            if (issues.Length == 0)
            {
                var text = Label(content, "[READY] All checks passed.", 0, 0, 1, 1, 20, Green);
                text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            }
            foreach (var issue in issues)
            {
                var button = ActionButton(content, (issue.blocksLaunch ? "[HOLD] " : "[NOTE] ") + issue.message, 0, 0, 1, 1,
                    () => {
                        if (issue.code == "power-check") session.BeginActivity(EarthActivity.Power);
                        else if (issue.code == "landing-check") session.BeginActivity(EarthActivity.Landing);
                        else { flow.Show(GameScreen.Configuration); flow.GetComponentInChildren<EarthAssemblyView>(true).SelectCategory(issue.category); }
                    });
                var label = button.GetComponentInChildren<Text>(); label.fontSize = 19; label.alignment = TextAnchor.MiddleLeft; label.color = issue.blocksLaunch ? Red : Orange;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = screen == GameScreen.Configuration ? 105 : 85;
            }
        }
        internal static string Money(int value) => "$" + (value / 1000000f).ToString("0.##") + "M";
        internal static string Specs(EarthPart p, int group)
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
        internal static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; Place(rect, x, y, w, h); go.GetComponent<Image>().color = color; return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(x + w, y + h); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        internal static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place((RectTransform)go.transform, x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text;
            label.fontSize = size; label.color = color; label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false; label.raycastTarget = false;
            return label;
        }
        internal static Button ActionButton(Transform parent, string text, float x, float y, float w, float h, Action action, Color? color = null)
        {
            var rect = Box(parent, text, x, y, w, h, color ?? Card); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            var label = Label(rect, text, .04f, .05f, .92f, .9f, 19, Color.white); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
