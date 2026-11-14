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
        private static readonly Color Card = new Color(0.095f, 0.145f, 0.225f, 1f);
        private static readonly Color CardSelected = new Color(0.07f, 0.28f, 0.46f, 1f);
        private static readonly Color Accent = new Color(0.12f, 0.55f, 0.92f, 1f);
        private static readonly Color Success = new Color(0.2f, 0.78f, 0.52f, 1f);
        private static readonly Color Warning = new Color(1f, 0.67f, 0.2f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.7f, 0.8f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.96f, 1f, 1f);

        public static void Build(Transform flowRoot)
        {
            if (Camera.allCamerasCount == 0)
            {
                GameObject cameraObject = new GameObject("UI Fallback Camera", typeof(Camera));
                cameraObject.transform.SetParent(flowRoot, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
                camera.cullingMask = 0;
                camera.depth = -100f;
            }

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
            Texture2D backgroundTexture = Resources.Load<Texture2D>("Art/lunar-menu-background");
            if (backgroundTexture != null)
            {
                background.sprite = Sprite.Create(
                    backgroundTexture,
                    new Rect(0f, 0f, backgroundTexture.width, backgroundTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                background.color = Color.white;
            }

            Image atmosphere = CreateImage("Atmosphere", canvasObject.transform, new Color(0.01f, 0.025f, 0.055f, 0.22f));
            Stretch(atmosphere.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateScreen(canvasObject.transform, GameScreen.MainMenu, "SAGITTARIUS A", "Design missions. Understand the trade-offs. Reach farther.", "Start Campaign", GameScreen.MissionSelection, null, GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.MissionSelection, "MISSION SELECTION", "Earth Observation is available. Moon, Mars, and Jupiter unlock as the campaign progresses.", "Open Earth Mission", GameScreen.Briefing, "Back", GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.Briefing, "EARTH OBSERVATION BRIEFING", "Configure a scientifically useful Earth-observation spacecraft while staying within mission limits.", "Start Configuration", GameScreen.Configuration, "Back", GameScreen.MissionSelection);
            CreateScreen(canvasObject.transform, GameScreen.Configuration, "SPACECRAFT CONFIGURATION", "Select instruments, spacecraft systems, an orbit, and a launcher. Resource cards will be connected next.", "Review Design", GameScreen.Review, "Back", GameScreen.Briefing);
            CreateScreen(canvasObject.transform, GameScreen.Review, "MISSION REVIEW", "Review resource totals, compatibility messages, and expected risks before launch.", "Launch", GameScreen.Simulation, "Back", GameScreen.Configuration);
            CreateScreen(canvasObject.transform, GameScreen.Simulation, "MISSION SIMULATION", "The 3D mission visualization and simulation status will appear here.", "View Results", GameScreen.Results, null, GameScreen.Simulation);
            CreateScreen(canvasObject.transform, GameScreen.Results, "POST-MISSION REPORT", "Success, score breakdown, scientific results, and explanations will appear here.", "Retry Configuration", GameScreen.Configuration, "Mission Selection", GameScreen.MissionSelection);

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem.transform.SetParent(flowRoot, false);
                InputSystemUIInputModule inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
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
            if (screen == GameScreen.MainMenu)
            {
                CreateMainMenuScreen(parent, primaryTarget);
                return;
            }

            Image panel = CreateImage($"{screen}Screen", parent, new Color(Panel.r, Panel.g, Panel.b, 0.9f));
            Stretch(panel.rectTransform, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f), Vector2.zero, Vector2.zero);

            Text eyebrow = CreateText("FlowLabel", panel.transform, GetStepLabel(screen), 18, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            Stretch(eyebrow.rectTransform, new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);

            Text title = CreateText("Heading", panel.transform, heading, 38, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            Stretch(title.rectTransform, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero);

            Text body = CreateText("Description", panel.transform, description, 18, FontStyle.Normal, MutedText, TextAnchor.UpperLeft);
            Stretch(body.rectTransform, new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);

            Image content = CreateImage("Content", panel.transform, new Color(0.035f, 0.065f, 0.115f, 0.65f));
            Stretch(content.rectTransform, new Vector2(0.04f, 0.21f), new Vector2(0.96f, 0.68f), Vector2.zero, Vector2.zero);
            BuildScreenContent(content.transform, screen);

            Button primary = CreateButton("PrimaryAction", panel.transform, primaryLabel, Accent);
            Stretch(primary.GetComponent<RectTransform>(), new Vector2(0.73f, 0.055f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero);

            Button secondary = null;
            if (!string.IsNullOrWhiteSpace(secondaryLabel))
            {
                secondary = CreateButton("SecondaryAction", panel.transform, secondaryLabel, new Color(0.18f, 0.23f, 0.31f, 1f));
                Stretch(secondary.GetComponent<RectTransform>(), new Vector2(0.54f, 0.055f), new Vector2(0.71f, 0.16f), Vector2.zero, Vector2.zero);
            }

            GameScreenView view = panel.gameObject.AddComponent<GameScreenView>();
            view.Configure(screen, primary, primaryTarget, secondary, secondaryTarget);
            panel.gameObject.SetActive(screen == GameScreen.MainMenu);
        }

        private static void CreateMainMenuScreen(Transform parent, GameScreen primaryTarget)
        {
            Image panel = CreateImage("MainMenuScreen", parent, new Color(0f, 0f, 0f, 0f));
            Stretch(panel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateLabel(panel.transform, "/", 48, FontStyle.Bold, new Color(1f, 0.43f, 0.04f, 1f), new Vector2(0.045f, 0.81f), new Vector2(0.09f, 0.94f));
            CreateLabel(panel.transform, "S A G I T T A R I U S   A", 42, FontStyle.Bold, TextColor, new Vector2(0.075f, 0.82f), new Vector2(0.52f, 0.93f));
            CreateLabel(panel.transform, "S P A C E   M I S S I O N   D E S I G N   P R O G R A M", 17, FontStyle.Normal, MutedText, new Vector2(0.047f, 0.75f), new Vector2(0.52f, 0.82f));

            CreateTopTab(panel.transform, "E A R T H", 0.68f, true);
            CreateTopTab(panel.transform, "M O O N", 0.76f, false);
            CreateTopTab(panel.transform, "M A R S", 0.84f, false);
            CreateTopTab(panel.transform, "J U P I T E R", 0.91f, false);

            Image accentBar = CreateImage("ActiveMenuAccent", panel.transform, new Color(1f, 0.43f, 0.04f, 1f));
            Stretch(accentBar.rectTransform, new Vector2(0.047f, 0.596f), new Vector2(0.05f, 0.654f), Vector2.zero, Vector2.zero);

            Button startButton = CreateButton("StartNewProgram", panel.transform, "START NEW PROGRAM", new Color(0f, 0f, 0f, 0f));
            Stretch(startButton.GetComponent<RectTransform>(), new Vector2(0.055f, 0.58f), new Vector2(0.36f, 0.67f), Vector2.zero, Vector2.zero);
            Text startLabel = startButton.GetComponentInChildren<Text>();
            startLabel.fontSize = 25;
            startLabel.alignment = TextAnchor.MiddleLeft;
            startLabel.fontStyle = FontStyle.Bold;
            CreateLabel(panel.transform, "→", 32, FontStyle.Normal, new Color(1f, 0.43f, 0.04f, 1f), new Vector2(0.325f, 0.585f), new Vector2(0.37f, 0.665f));

            CreateMenuItem(panel.transform, "MISSION ARCHIVE", 0.5f);
            CreateMenuItem(panel.transform, "ACHIEVEMENTS", 0.43f);
            CreateMenuItem(panel.transform, "SETTINGS", 0.36f);
            CreateMenuItem(panel.transform, "CREDITS", 0.29f);
            CreateMenuItem(panel.transform, "EXIT", 0.22f);

            Image statusBar = CreateImage("ProgramStatusAccent", panel.transform, new Color(1f, 0.43f, 0.04f, 1f));
            Stretch(statusBar.rectTransform, new Vector2(0.047f, 0.075f), new Vector2(0.049f, 0.11f), Vector2.zero, Vector2.zero);
            CreateLabel(panel.transform, "PROGRAM STATUS: EARTH MISSION AVAILABLE", 15, FontStyle.Normal, TextColor, new Vector2(0.06f, 0.06f), new Vector2(0.43f, 0.125f));

            GameScreenView view = panel.gameObject.AddComponent<GameScreenView>();
            view.Configure(GameScreen.MainMenu, startButton, primaryTarget, null, GameScreen.MainMenu);
            panel.gameObject.SetActive(true);
        }

        private static void CreateTopTab(Transform parent, string label, float x, bool active)
        {
            CreateLabel(parent, label, 15, FontStyle.Bold, active ? new Color(1f, 0.43f, 0.04f, 1f) : TextColor, new Vector2(x, 0.88f), new Vector2(Mathf.Min(x + 0.08f, 0.98f), 0.95f), TextAnchor.MiddleCenter);
            if (!active)
            {
                return;
            }

            Image underline = CreateImage(label + "Underline", parent, new Color(1f, 0.43f, 0.04f, 1f));
            Stretch(underline.rectTransform, new Vector2(x, 0.875f), new Vector2(Mathf.Min(x + 0.075f, 0.98f), 0.878f), Vector2.zero, Vector2.zero);
        }

        private static void CreateMenuItem(Transform parent, string label, float y)
        {
            CreateLabel(parent, label, 21, FontStyle.Normal, new Color(0.78f, 0.81f, 0.86f, 1f), new Vector2(0.065f, y), new Vector2(0.35f, y + 0.065f));
        }

        private static string GetStepLabel(GameScreen screen)
        {
            return screen switch
            {
                GameScreen.MainMenu => "MISSION DESIGN LAB  /  CAMPAIGN",
                GameScreen.MissionSelection => "CAMPAIGN  /  SELECT ASSIGNMENT",
                GameScreen.Briefing => "EARTH OBSERVATION  /  01 BRIEFING",
                GameScreen.Configuration => "EARTH OBSERVATION  /  02 CONFIGURATION",
                GameScreen.Review => "EARTH OBSERVATION  /  03 DESIGN REVIEW",
                GameScreen.Simulation => "EARTH OBSERVATION  /  04 MISSION OPS",
                GameScreen.Results => "EARTH OBSERVATION  /  05 REPORT",
                _ => "MISSION DESIGN LAB"
            };
        }

        private static void BuildScreenContent(Transform parent, GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.MainMenu:
                    BuildCampaignRoute(parent);
                    break;
                case GameScreen.MissionSelection:
                    BuildMissionCards(parent);
                    break;
                case GameScreen.Briefing:
                    BuildBriefing(parent);
                    break;
                case GameScreen.Configuration:
                    BuildConfiguration(parent);
                    break;
                case GameScreen.Review:
                    BuildReview(parent);
                    break;
                case GameScreen.Simulation:
                    BuildSimulation(parent);
                    break;
                case GameScreen.Results:
                    BuildResults(parent);
                    break;
            }
        }

        private static void BuildCampaignRoute(Transform parent)
        {
            CreateSectionTitle(parent, "CAMPAIGN ROUTE", new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.96f));
            CreateMissionCard(parent, new Vector2(0.03f, 0.12f), new Vector2(0.25f, 0.78f), "01", "EARTH", "Observation", "AVAILABLE", Accent);
            CreateMissionCard(parent, new Vector2(0.27f, 0.12f), new Vector2(0.49f, 0.78f), "02", "MOON", "Mapping", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.51f, 0.12f), new Vector2(0.73f, 0.78f), "03", "MARS", "Orbiter", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.75f, 0.12f), new Vector2(0.97f, 0.78f), "04", "JUPITER", "Deep space", "LOCKED", MutedText);
        }

        private static void BuildMissionCards(Transform parent)
        {
            CreateMissionCard(parent, new Vector2(0.03f, 0.1f), new Vector2(0.27f, 0.9f), "01", "EARTH", "Measure climate and land change", "ACTIVE", Accent);
            CreateMissionCard(parent, new Vector2(0.29f, 0.1f), new Vector2(0.53f, 0.9f), "02", "MOON", "Map surface resources", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.55f, 0.1f), new Vector2(0.77f, 0.9f), "03", "MARS", "Study atmosphere and dust", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.79f, 0.1f), new Vector2(0.97f, 0.9f), "04", "JUPITER", "Survive radiation", "LOCKED", MutedText);
        }

        private static void BuildBriefing(Transform parent)
        {
            Image objective = CreateCard(parent, "Primary Objective", new Vector2(0.03f, 0.52f), new Vector2(0.97f, 0.9f), CardSelected);
            CreateLabel(objective.transform, "PRIMARY OBJECTIVE", 16, FontStyle.Bold, Accent, new Vector2(0.04f, 0.67f), new Vector2(0.96f, 0.9f));
            CreateLabel(objective.transform, "Monitor coastal and vegetation change with useful resolution while returning most collected data to Earth.", 24, FontStyle.Bold, TextColor, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.67f));

            CreateConstraintCard(parent, new Vector2(0.03f, 0.1f), new Vector2(0.34f, 0.46f), "BUDGET", "120 credits", "All hardware + launch");
            CreateConstraintCard(parent, new Vector2(0.355f, 0.1f), new Vector2(0.665f, 0.46f), "MISSION", "180 days", "Target lifetime");
            CreateConstraintCard(parent, new Vector2(0.68f, 0.1f), new Vector2(0.97f, 0.46f), "SUCCESS", "70+ science", "Risk below 35%");
        }

        private static void BuildConfiguration(Transform parent)
        {
            Image categories = CreateCard(parent, "Categories", new Vector2(0.02f, 0.08f), new Vector2(0.17f, 0.92f), Card);
            CreateLabel(categories.transform, "BUILD", 15, FontStyle.Bold, Accent, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f));
            CreateCategory(categories.transform, "Instruments", 0.64f, true);
            CreateCategory(categories.transform, "Spacecraft", 0.48f, false);
            CreateCategory(categories.transform, "Orbit", 0.32f, false);
            CreateCategory(categories.transform, "Launcher", 0.16f, false);

            Image choices = CreateCard(parent, "Instrument Choices", new Vector2(0.19f, 0.08f), new Vector2(0.69f, 0.92f), Card);
            CreateLabel(choices.transform, "SELECT AN INSTRUMENT", 17, FontStyle.Bold, TextColor, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.96f));
            CreateComponentCard(choices.transform, new Vector2(0.04f, 0.12f), new Vector2(0.34f, 0.78f), "MULTISPECTRAL", "12 kg  /  38 W", "+42 science", true);
            CreateComponentCard(choices.transform, new Vector2(0.36f, 0.12f), new Vector2(0.66f, 0.78f), "RADAR MAPPER", "28 kg  /  74 W", "+55 science", false);
            CreateComponentCard(choices.transform, new Vector2(0.68f, 0.12f), new Vector2(0.96f, 0.78f), "THERMAL IR", "16 kg  /  46 W", "+36 science", false);

            Image dashboard = CreateCard(parent, "Resource Dashboard", new Vector2(0.71f, 0.08f), new Vector2(0.98f, 0.92f), Card);
            CreateLabel(dashboard.transform, "LIVE RESOURCES", 17, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.84f), new Vector2(0.93f, 0.96f));
            CreateMetric(dashboard.transform, "BUDGET", "82 / 120", 0.67f, Success);
            CreateMetric(dashboard.transform, "MASS", "96 / 140 kg", 0.52f, Success);
            CreateMetric(dashboard.transform, "POWER", "112 / 125 W", 0.37f, Warning);
            CreateMetric(dashboard.transform, "DOWNLINK", "18 / 24 Mbps", 0.22f, Success);
            CreateMetric(dashboard.transform, "RISK", "24%", 0.07f, Success);
        }

        private static void BuildReview(Transform parent)
        {
            Image summary = CreateCard(parent, "Design Summary", new Vector2(0.03f, 0.08f), new Vector2(0.58f, 0.92f), Card);
            CreateLabel(summary.transform, "DESIGN SUMMARY", 18, FontStyle.Bold, TextColor, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));
            CreateReviewLine(summary.transform, "Instrument", "Multispectral Camera", 0.66f);
            CreateReviewLine(summary.transform, "Orbit", "Sun-synchronous / 620 km", 0.5f);
            CreateReviewLine(summary.transform, "Launcher", "Aquila Medium", 0.34f);
            CreateReviewLine(summary.transform, "Science value", "78 points", 0.18f);

            Image checks = CreateCard(parent, "Validation", new Vector2(0.61f, 0.08f), new Vector2(0.97f, 0.92f), Card);
            CreateLabel(checks.transform, "VALIDATION", 18, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.95f));
            CreateStatusLine(checks.transform, "READY", "Mass and launcher compatible", 0.61f, Success);
            CreateStatusLine(checks.transform, "READY", "Power margin: 13 W", 0.41f, Success);
            CreateStatusLine(checks.transform, "WARNING", "Downlink margin is limited", 0.21f, Warning);
        }

        private static void BuildSimulation(Transform parent)
        {
            CreateSectionTitle(parent, "MISSION TIMELINE", new Vector2(0.04f, 0.79f), new Vector2(0.96f, 0.94f));
            CreatePhase(parent, "01", "LAUNCH", "Complete", 0.04f, Success);
            CreatePhase(parent, "02", "DEPLOY", "Complete", 0.24f, Success);
            CreatePhase(parent, "03", "CALIBRATE", "Complete", 0.44f, Success);
            CreatePhase(parent, "04", "OBSERVE", "In progress", 0.64f, Accent);
            CreatePhase(parent, "05", "DOWNLINK", "Queued", 0.84f, MutedText);
        }

        private static void BuildResults(Transform parent)
        {
            Image score = CreateCard(parent, "Score", new Vector2(0.03f, 0.08f), new Vector2(0.3f, 0.92f), CardSelected);
            CreateLabel(score.transform, "MISSION SUCCESS", 17, FontStyle.Bold, Success, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.92f));
            CreateLabel(score.transform, "84", 72, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.78f));
            CreateLabel(score.transform, "TOTAL SCORE / 100", 15, FontStyle.Bold, MutedText, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.34f));

            Image breakdown = CreateCard(parent, "Breakdown", new Vector2(0.33f, 0.08f), new Vector2(0.68f, 0.92f), Card);
            CreateLabel(breakdown.transform, "SCORE BREAKDOWN", 17, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.8f), new Vector2(0.93f, 0.93f));
            CreateReviewLine(breakdown.transform, "Science return", "38 / 40", 0.62f);
            CreateReviewLine(breakdown.transform, "Engineering", "27 / 35", 0.43f);
            CreateReviewLine(breakdown.transform, "Budget", "19 / 25", 0.24f);

            Image lesson = CreateCard(parent, "Lesson", new Vector2(0.71f, 0.08f), new Vector2(0.97f, 0.92f), Card);
            CreateLabel(lesson.transform, "WHAT HAPPENED", 17, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.93f));
            CreateLabel(lesson.transform, "Strong instrument and orbit pairing produced useful coverage. Limited downlink caused 12% data loss.", 19, FontStyle.Normal, TextColor, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.76f));
            CreateLabel(lesson.transform, "MOON MISSION UNLOCKED", 15, FontStyle.Bold, Success, new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.3f));
        }

        private static Image CreateCard(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            Image card = CreateImage(name, parent, color);
            Stretch(card.rectTransform, min, max, Vector2.zero, Vector2.zero);
            return card;
        }

        private static void CreateMissionCard(Transform parent, Vector2 min, Vector2 max, string number, string title, string subtitle, string status, Color statusColor)
        {
            Image card = CreateCard(parent, title, min, max, status == "ACTIVE" || status == "AVAILABLE" ? CardSelected : Card);
            CreateLabel(card.transform, number, 17, FontStyle.Bold, Accent, new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.92f));
            CreateLabel(card.transform, title, 28, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.76f));
            CreateLabel(card.transform, subtitle, 16, FontStyle.Normal, MutedText, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.5f));
            CreateLabel(card.transform, status, 14, FontStyle.Bold, statusColor, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.22f));
        }

        private static void CreateConstraintCard(Transform parent, Vector2 min, Vector2 max, string label, string value, string note)
        {
            Image card = CreateCard(parent, label, min, max, Card);
            CreateLabel(card.transform, label, 14, FontStyle.Bold, Accent, new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.9f));
            CreateLabel(card.transform, value, 24, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.32f), new Vector2(0.93f, 0.68f));
            CreateLabel(card.transform, note, 15, FontStyle.Normal, MutedText, new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.32f));
        }

        private static void CreateCategory(Transform parent, string label, float y, bool selected)
        {
            Image item = CreateCard(parent, label, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.12f), selected ? CardSelected : new Color(0.08f, 0.12f, 0.18f, 1f));
            CreateLabel(item.transform, label, 15, selected ? FontStyle.Bold : FontStyle.Normal, selected ? TextColor : MutedText, new Vector2(0.08f, 0f), new Vector2(0.92f, 1f));
        }

        private static void CreateComponentCard(Transform parent, Vector2 min, Vector2 max, string title, string stats, string science, bool selected)
        {
            Image card = CreateCard(parent, title, min, max, selected ? CardSelected : new Color(0.075f, 0.115f, 0.18f, 1f));
            CreateLabel(card.transform, selected ? "SELECTED" : "AVAILABLE", 13, FontStyle.Bold, selected ? Success : MutedText, new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.93f));
            CreateLabel(card.transform, title, 19, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.79f));
            CreateLabel(card.transform, stats, 14, FontStyle.Normal, MutedText, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.5f));
            CreateLabel(card.transform, science, 15, FontStyle.Bold, Accent, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.27f));
        }

        private static void CreateMetric(Transform parent, string label, string value, float y, Color color)
        {
            CreateLabel(parent, label, 13, FontStyle.Bold, MutedText, new Vector2(0.08f, y), new Vector2(0.46f, y + 0.11f));
            CreateLabel(parent, value, 15, FontStyle.Bold, color, new Vector2(0.46f, y), new Vector2(0.92f, y + 0.11f), TextAnchor.MiddleRight);
        }

        private static void CreateReviewLine(Transform parent, string label, string value, float y)
        {
            CreateLabel(parent, label, 16, FontStyle.Normal, MutedText, new Vector2(0.06f, y), new Vector2(0.45f, y + 0.13f));
            CreateLabel(parent, value, 17, FontStyle.Bold, TextColor, new Vector2(0.45f, y), new Vector2(0.94f, y + 0.13f), TextAnchor.MiddleRight);
        }

        private static void CreateStatusLine(Transform parent, string status, string message, float y, Color color)
        {
            Image row = CreateCard(parent, status, new Vector2(0.07f, y), new Vector2(0.93f, y + 0.15f), new Color(color.r, color.g, color.b, 0.14f));
            CreateLabel(row.transform, status, 13, FontStyle.Bold, color, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.9f));
            CreateLabel(row.transform, message, 15, FontStyle.Normal, TextColor, new Vector2(0.3f, 0.1f), new Vector2(0.95f, 0.9f));
        }

        private static void CreatePhase(Transform parent, string number, string title, string status, float x, Color color)
        {
            Image phase = CreateCard(parent, title, new Vector2(x, 0.16f), new Vector2(x + 0.16f, 0.7f), new Color(color.r, color.g, color.b, 0.16f));
            CreateLabel(phase.transform, number, 16, FontStyle.Bold, color, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));
            CreateLabel(phase.transform, title, 18, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.72f));
            CreateLabel(phase.transform, status, 14, FontStyle.Normal, color, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.35f));
        }

        private static void CreateSectionTitle(Transform parent, string value, Vector2 min, Vector2 max)
        {
            CreateLabel(parent, value, 17, FontStyle.Bold, TextColor, min, max);
        }

        private static Text CreateLabel(Transform parent, string value, int size, FontStyle style, Color color, Vector2 min, Vector2 max, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = CreateText(value, parent, value, size, style, color, alignment);
            Stretch(text.rectTransform, min, max, Vector2.zero, Vector2.zero);
            return text;
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
