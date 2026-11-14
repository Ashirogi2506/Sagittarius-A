using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SagittariusA.Gameplay.UI;

namespace SagittariusA.Gameplay.Flow
{
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private GameScreen startScreen = GameScreen.MainMenu;

        private readonly Dictionary<GameScreen, GameScreenView> views = new Dictionary<GameScreen, GameScreenView>();

        public GameScreen CurrentScreen { get; private set; }
        public event Action<GameScreen, GameScreen> ScreenChanged;

        private void Awake()
        {
            RebuildViewMap();

            if (views.Count == 0)
            {
                PlaceholderUiFactory.Build(transform);
                RebuildViewMap();
            }

            Show(startScreen);
        }

        public void RebuildViewMap()
        {
            views.Clear();

            foreach (GameScreenView view in GetComponentsInChildren<GameScreenView>(true))
            {
                if (!views.TryAdd(view.Screen, view))
                {
                    Debug.LogError($"Duplicate UI view registered for {view.Screen}.", view);
                }
            }
        }

        public bool Show(GameScreen screen)
        {
            if (!views.TryGetValue(screen, out GameScreenView nextView))
            {
                Debug.LogError($"No UI view is registered for {screen}.", this);
                return false;
            }

            GameScreen previous = CurrentScreen;

            foreach (GameScreenView view in views.Values)
            {
                view.gameObject.SetActive(view == nextView);
            }

            CurrentScreen = screen;
            ScreenChanged?.Invoke(previous, screen);
            return true;
        }
    }

    internal static class PlaceholderUiFactory
    {
        private static readonly Color Background = new Color(0.025f, 0.045f, 0.09f, 1f);
        private static readonly Color Panel = new Color(0.07f, 0.11f, 0.18f, 0.98f);
        private static readonly Color Accent = new Color(0.12f, 0.55f, 0.92f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.96f, 1f, 1f);

        public static void Build(Transform flowRoot)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(flowRoot, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasObject.transform, Background);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateScreen(canvasObject.transform, GameScreen.MainMenu, "SAGITTARIUS A", "Design missions. Understand the trade-offs. Reach farther.", "Start Campaign", GameScreen.MissionSelection, null, GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.MissionSelection, "MISSION SELECTION", "Earth Observation is available. Moon, Mars, and Jupiter unlock as the campaign progresses.", "Open Earth Mission", GameScreen.Briefing, "Back", GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.Briefing, "EARTH OBSERVATION BRIEFING", "Configure a scientifically useful Earth-observation spacecraft while staying within mission limits.", "Start Configuration", GameScreen.Configuration, "Back", GameScreen.MissionSelection);
            CreateScreen(canvasObject.transform, GameScreen.Configuration, "SPACECRAFT CONFIGURATION", "Select instruments, spacecraft systems, an orbit, and a launcher. Resource cards will be connected next.", "Review Design", GameScreen.Review, "Back", GameScreen.Briefing);
            CreateScreen(canvasObject.transform, GameScreen.Review, "MISSION REVIEW", "Review resource totals, compatibility messages, and expected risks before launch.", "Launch", GameScreen.Simulation, "Back", GameScreen.Configuration);
            CreateScreen(canvasObject.transform, GameScreen.Simulation, "MISSION SIMULATION", "The 3D mission visualization and simulation status will appear here.", "View Results", GameScreen.Results, null, GameScreen.Simulation);
            CreateScreen(canvasObject.transform, GameScreen.Results, "POST-MISSION REPORT", "Success, score breakdown, scientific results, and explanations will appear here.", "Retry Configuration", GameScreen.Configuration, "Mission Selection", GameScreen.MissionSelection);

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(flowRoot, false);
            }
        }

        private static void CreateScreen(
            Transform parent,
            GameScreen screen,
            string heading,
            string description,
            string primaryLabel,
            GameScreen primaryTarget,
            string secondaryLabel,
            GameScreen secondaryTarget)
        {
            Image panel = CreateImage($"{screen}Screen", parent, Panel);
            Stretch(panel.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);

            Text eyebrow = CreateText("FlowLabel", panel.transform, "EARTH VERTICAL SLICE", 20, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            Stretch(eyebrow.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);

            Text title = CreateText("Heading", panel.transform, heading, 48, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

            Text body = CreateText("Description", panel.transform, description, 26, FontStyle.Normal, TextColor, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero);

            Button primary = CreateButton("PrimaryAction", panel.transform, primaryLabel, Accent);
            Stretch(primary.GetComponent<RectTransform>(), new Vector2(0.62f, 0.1f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);

            Button secondary = null;
            if (!string.IsNullOrWhiteSpace(secondaryLabel))
            {
                secondary = CreateButton("SecondaryAction", panel.transform, secondaryLabel, new Color(0.18f, 0.23f, 0.31f, 1f));
                Stretch(secondary.GetComponent<RectTransform>(), new Vector2(0.34f, 0.1f), new Vector2(0.6f, 0.24f), Vector2.zero, Vector2.zero);
            }

            GameScreenView view = panel.gameObject.AddComponent<GameScreenView>();
            view.Configure(screen, primary, primaryTarget, secondary, secondaryTarget);
            panel.gameObject.SetActive(screen == GameScreen.MainMenu);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            Image image = CreateImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            Text text = CreateText("Label", image.transform, label, 22, FontStyle.Bold, TextColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f));
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
