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
            var session = GetComponent<EarthGameplaySession>() ?? gameObject.AddComponent<EarthGameplaySession>();
            session.Initialize();
            var moon = GetComponent<MoonStageController>() ?? gameObject.AddComponent<MoonStageController>();
            moon.Connect(session);
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
            if (GetComponent<EarthGameplaySession>().Busy) return false;
            if (!views.TryGetValue(screen, out GameScreenView nextView))
            {
                Debug.LogError($"No UI view is registered for {screen}.", this);
                return false;
            }

            var session = GetComponent<EarthGameplaySession>();
            if (screen == GameScreen.Simulation && !session.TryLaunch()) return false;
            if (screen == GameScreen.Results && !session.Launched) return false;
            if (screen == GameScreen.MoonArrival && !session.FlightChecksCompleted) return false;

            GameScreen previous = CurrentScreen;

            foreach (GameScreenView view in views.Values)
            {
                view.gameObject.SetActive(view == nextView);
            }

            CurrentScreen = screen;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                foreach (var button in nextView.GetComponentsInChildren<Button>())
                    if (button.interactable) { EventSystem.current.SetSelectedGameObject(button.gameObject); break; }
            }
            ScreenChanged?.Invoke(previous, screen);
            return true;
        }
    }

    internal sealed class CommanderSetupView : MonoBehaviour
    {
        private const string CommanderIdKey = "FarSignal.CommanderId";
        private const string CommanderNameKey = "FarSignal.CommanderName";

        private Button maleButton;
        private Button femaleButton;
        private Image maleCard;
        private Image femaleCard;
        private Text maleStatus;
        private Text femaleStatus;
        private InputField nameInput;
        private Button saveButton;
        private Text saveStatus;
        private Button continueButton;
        private string selectedId;
        private Color selectedColor;
        private Color normalColor;
        private Color accentColor;
        private Color mutedColor;

        public void Configure(
            Button configuredMaleButton,
            Button configuredFemaleButton,
            Image configuredMaleCard,
            Image configuredFemaleCard,
            Text configuredMaleStatus,
            Text configuredFemaleStatus,
            InputField configuredNameInput,
            Button configuredSaveButton,
            Text configuredSaveStatus,
            Color configuredSelectedColor,
            Color configuredNormalColor,
            Color configuredAccentColor,
            Color configuredMutedColor)
        {
            maleButton = configuredMaleButton;
            femaleButton = configuredFemaleButton;
            maleCard = configuredMaleCard;
            femaleCard = configuredFemaleCard;
            maleStatus = configuredMaleStatus;
            femaleStatus = configuredFemaleStatus;
            nameInput = configuredNameInput;
            saveButton = configuredSaveButton;
            saveStatus = configuredSaveStatus;
            selectedColor = configuredSelectedColor;
            normalColor = configuredNormalColor;
            accentColor = configuredAccentColor;
            mutedColor = configuredMutedColor;

            selectedId = PlayerPrefs.GetString(CommanderIdKey, "commander_male");
            string defaultName = selectedId == "commander_female" ? "Ari" : "Alex";
            nameInput.text = PlayerPrefs.GetString(CommanderNameKey, defaultName);

            maleButton.onClick.AddListener(SelectMale);
            femaleButton.onClick.AddListener(SelectFemale);
            saveButton.onClick.AddListener(SaveCurrentProfile);
            nameInput.onValueChanged.AddListener(MarkUnsaved);
            RefreshSelection();
            ShowSavedStatus();
        }

        public void BindContinueButton(Button configuredContinueButton)
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(SaveCurrentProfile);
            }

            continueButton = configuredContinueButton;
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(SaveCurrentProfile);
            }
        }

        private void SelectMale()
        {
            Select("commander_male", "Alex", "Ari");
        }

        private void SelectFemale()
        {
            Select("commander_female", "Ari", "Alex");
        }

        private void Select(string commanderId, string suggestedName, string otherSuggestedName)
        {
            selectedId = commanderId;
            if (string.IsNullOrWhiteSpace(nameInput.text) ||
                string.Equals(nameInput.text, otherSuggestedName, StringComparison.OrdinalIgnoreCase))
            {
                nameInput.text = suggestedName;
            }

            PlayerPrefs.SetString(CommanderIdKey, selectedId);
            SaveName(nameInput.text);
            RefreshSelection();
            ShowSavedStatus();
        }

        private void SaveName(string value)
        {
            string fallback = selectedId == "commander_female" ? "Ari" : "Alex";
            string playerName = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            nameInput.SetTextWithoutNotify(playerName);
            PlayerPrefs.SetString(CommanderNameKey, playerName);
            PlayerPrefs.Save();
        }

        private void MarkUnsaved(string value)
        {
            if (saveStatus == null)
            {
                return;
            }

            saveStatus.text = "UNSAVED CHANGES";
            saveStatus.color = mutedColor;
        }

        private void SaveCurrentProfile()
        {
            PlayerPrefs.SetString(CommanderIdKey, selectedId);
            SaveName(nameInput.text);
            ShowSavedStatus();
        }

        private void ShowSavedStatus()
        {
            if (saveStatus == null)
            {
                return;
            }

            saveStatus.text = $"PROFILE SAVED · {nameInput.text.ToUpperInvariant()}";
            saveStatus.color = accentColor;
        }

        private void RefreshSelection()
        {
            bool maleSelected = selectedId != "commander_female";
            maleCard.color = maleSelected ? selectedColor : normalColor;
            femaleCard.color = maleSelected ? normalColor : selectedColor;
            maleStatus.text = maleSelected ? "SELECTED" : "AVAILABLE";
            femaleStatus.text = maleSelected ? "AVAILABLE" : "SELECTED";
            maleStatus.color = maleSelected ? accentColor : mutedColor;
            femaleStatus.color = maleSelected ? mutedColor : accentColor;
        }

        private void OnDestroy()
        {
            if (maleButton != null)
            {
                maleButton.onClick.RemoveListener(SelectMale);
            }

            if (femaleButton != null)
            {
                femaleButton.onClick.RemoveListener(SelectFemale);
            }

            if (nameInput != null)
            {
                nameInput.onValueChanged.RemoveListener(MarkUnsaved);
            }
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(SaveCurrentProfile);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(SaveCurrentProfile);
            }
        }
    }

    internal sealed class CommanderNameTextView : MonoBehaviour
    {
        private Text target;
        private string textTemplate;

        public void Configure(Text configuredTarget, string configuredTemplate)
        {
            target = configuredTarget;
            textTemplate = configuredTemplate;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (target == null || string.IsNullOrEmpty(textTemplate))
            {
                return;
            }

            string playerName = PlayerPrefs.GetString("FarSignal.CommanderName", "Alex");
            target.text = textTemplate.Replace("{playerName}", playerName);
        }
    }

    internal sealed class CampaignRouteStatusView : MonoBehaviour
    {
        private Image moonCard;
        private Text moonStatus;
        private Color readyColor;
        private Color lockedColor;
        private Color readyCardColor;
        private Color lockedCardColor;

        public void Configure(Image configuredMoonCard, Text configuredMoonStatus, Color configuredReadyColor, Color configuredLockedColor, Color configuredReadyCardColor, Color configuredLockedCardColor)
        {
            moonCard = configuredMoonCard;
            moonStatus = configuredMoonStatus;
            readyColor = configuredReadyColor;
            lockedColor = configuredLockedColor;
            readyCardColor = configuredReadyCardColor;
            lockedCardColor = configuredLockedCardColor;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (moonCard == null || moonStatus == null)
            {
                return;
            }

            bool earthComplete = GetComponentInParent<EarthGameplaySession>().Launched;
            moonCard.color = earthComplete ? readyCardColor : lockedCardColor;
            moonStatus.text = earthComplete ? "HANDOFF READY" : "LOCKED";
            moonStatus.color = earthComplete ? readyColor : lockedColor;
        }
    }

    internal static class PlaceholderUiFactory
    {
        private static readonly Color Background = new Color(0.025f, 0.045f, 0.09f, 1f);
        private static readonly Color Panel = new Color(0.035f, 0.055f, 0.09f, 0.98f);
        private static readonly Color Card = new Color(0.065f, 0.085f, 0.12f, 1f);
        private static readonly Color CardSelected = new Color(0.13f, 0.12f, 0.105f, 1f);
        private static readonly Color Accent = new Color(1f, 0.43f, 0.04f, 1f);
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

            CreateScreen(canvasObject.transform, GameScreen.MainMenu, "FAR SIGNAL", "One crew. Three stops. One signal that has to make it home.", "Start New Expedition", GameScreen.CommanderSetup, null, GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.CommanderSetup, "CHOOSE YOUR COMMANDER", "Choose a portrait and call sign. Both commanders begin with the same capabilities.", "Begin Briefing", GameScreen.MissionSelection, "Back", GameScreen.MainMenu);
            CreateScreen(canvasObject.transform, GameScreen.MissionSelection, "EXPEDITION ROUTE", "Build on Earth, restore Pole-7, recover the orbiter on Mars, then carry the first signal home.", "Open The Moon Call", GameScreen.Briefing, "Back", GameScreen.CommanderSetup);
            CreateScreen(canvasObject.transform, GameScreen.Briefing, "EARTH — THE MOON CALL", "Pole-7 is on battery power. Finish the ship and launch with the P-4 panel and repair kit.", "Configure Ship", GameScreen.Configuration, "Back", GameScreen.MissionSelection);
            CreateScreen(canvasObject.transform, GameScreen.Configuration, "SHIP CONFIGURATION", "Choose compatible launch hardware, protect the required cargo, and preserve enough margin for the Moon.", "Review Launch", GameScreen.Review, "Back", GameScreen.Briefing);
            CreateScreen(canvasObject.transform, GameScreen.Review, "EARTH LAUNCH REPORT", "Verify the ship, both repair supplies, and the Beacon Pad plan before committing to launch.", "Commit Launch", GameScreen.Simulation, "Back", GameScreen.Configuration);
            CreateScreen(canvasObject.transform, GameScreen.Simulation, "LAUNCH AND ORBIT", "Complete the final checks, launch, and establish contact with Pole-7 from Earth orbit.", "Confirm Orbit", GameScreen.Results, null, GameScreen.Simulation);
            CreateScreen(canvasObject.transform, GameScreen.Results, "EARTH FLIGHT LOG", "The repair cargo is secure. The expedition can begin the Earth-to-Moon transit.", "Return to Route", GameScreen.MissionSelection, "Reconfigure", GameScreen.Configuration);
            EarthGarageView.Create(canvasObject.transform, GameScreen.MoonArrival);

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

            if (screen == GameScreen.Configuration || screen == GameScreen.Review || screen == GameScreen.Simulation || screen == GameScreen.Results)
            {
                EarthGarageView.Create(parent, screen);
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

            if (screen == GameScreen.CommanderSetup)
            {
                content.GetComponent<CommanderSetupView>()?.BindContinueButton(primary);
            }

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
            CreateLabel(panel.transform, "F A R   S I G N A L", 42, FontStyle.Bold, TextColor, new Vector2(0.075f, 0.82f), new Vector2(0.52f, 0.93f));
            CreateLabel(panel.transform, "O N E   C R E W  ·  T H R E E   S T O P S  ·  2 1 6 8", 17, FontStyle.Normal, MutedText, new Vector2(0.047f, 0.75f), new Vector2(0.55f, 0.82f));

            CreateTopTab(panel.transform, "E A R T H", 0.68f, true);
            CreateTopTab(panel.transform, "M O O N", 0.76f, false);
            CreateTopTab(panel.transform, "M A R S", 0.84f, false);
            CreateTopTab(panel.transform, "J U P I T E R", 0.91f, false);

            Image accentBar = CreateImage("ActiveMenuAccent", panel.transform, new Color(1f, 0.43f, 0.04f, 1f));
            Stretch(accentBar.rectTransform, new Vector2(0.047f, 0.596f), new Vector2(0.05f, 0.654f), Vector2.zero, Vector2.zero);

            Button startButton = CreateButton("CommanderProfile", panel.transform, "COMMANDER PROFILE", new Color(0f, 0f, 0f, 0f));
            Stretch(startButton.GetComponent<RectTransform>(), new Vector2(0.055f, 0.58f), new Vector2(0.36f, 0.67f), Vector2.zero, Vector2.zero);
            Text startLabel = startButton.GetComponentInChildren<Text>();
            startLabel.fontSize = 25;
            startLabel.alignment = TextAnchor.MiddleLeft;
            startLabel.fontStyle = FontStyle.Bold;
            CreateLabel(panel.transform, "→", 32, FontStyle.Normal, new Color(1f, 0.43f, 0.04f, 1f), new Vector2(0.325f, 0.585f), new Vector2(0.37f, 0.665f));

            Button resume = CreateButton("ContinueExpedition", panel.transform, "CONTINUE EXPEDITION", Color.clear);
            Stretch(resume.GetComponent<RectTransform>(), new Vector2(0.055f, 0.49f), new Vector2(0.36f, 0.56f), Vector2.zero, Vector2.zero);
            resume.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            resume.onClick.AddListener(() => {
                var flow = parent.GetComponentInParent<GameFlowController>();
                var session = flow.GetComponent<EarthGameplaySession>();
                flow.Show(session.FlightChecksCompleted ? GameScreen.MoonArrival : session.Launched ? GameScreen.Results : GameScreen.Configuration);
            });
            CreateMenuItem(panel.transform, "MISSION ARCHIVE", 0.43f);
            CreateMenuItem(panel.transform, "SETTINGS", 0.36f);
            CreateMenuItem(panel.transform, "CREDITS", 0.29f);
            CreateMenuItem(panel.transform, "EXIT", 0.22f);
#if UNITY_EDITOR
            Button moonTest = CreateButton("MoonIsolatedTest", panel.transform, "MOON SURFACE TEST", Color.clear);
            Stretch(moonTest.GetComponent<RectTransform>(), new Vector2(.055f, .12f), new Vector2(.36f, .19f), Vector2.zero, Vector2.zero);
            moonTest.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            moonTest.onClick.AddListener(() => {
                var owner = parent.GetComponentInParent<GameFlowController>();
                var moon = owner.GetComponent<MoonPlayableExperience>() ?? owner.gameObject.AddComponent<MoonPlayableExperience>();
                moon.StartIsolatedTest();
            });
#endif

            Image statusBar = CreateImage("ProgramStatusAccent", panel.transform, new Color(1f, 0.43f, 0.04f, 1f));
            Stretch(statusBar.rectTransform, new Vector2(0.047f, 0.075f), new Vector2(0.049f, 0.11f), Vector2.zero, Vector2.zero);
            CreateLabel(panel.transform, "EXPEDITION STATUS: SHIP ASSEMBLY AVAILABLE", 15, FontStyle.Normal, TextColor, new Vector2(0.06f, 0.06f), new Vector2(0.53f, 0.125f));

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
                GameScreen.MainMenu => "FAR SIGNAL  /  CAMPAIGN",
                GameScreen.CommanderSetup => "EXPEDITION  /  COMMANDER PROFILE",
                GameScreen.MissionSelection => "EXPEDITION  /  CAMPAIGN ROUTE",
                GameScreen.Briefing => "EARTH  /  01 THE MOON CALL",
                GameScreen.Configuration => "EARTH  /  02 SHIP CONFIGURATION",
                GameScreen.Review => "EARTH  /  03 LAUNCH REVIEW",
                GameScreen.Simulation => "EARTH  /  04 LAUNCH OPS",
                GameScreen.Results => "EARTH  /  05 FLIGHT LOG",
                _ => "FAR SIGNAL"
            };
        }

        private static void BuildScreenContent(Transform parent, GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.MainMenu:
                    BuildCampaignRoute(parent);
                    break;
                case GameScreen.CommanderSetup:
                    BuildCommanderSetup(parent);
                    break;
                case GameScreen.MissionSelection:
                    BuildMissionCards(parent);
                    break;
                case GameScreen.Briefing:
                    BuildBriefing(parent);
                    break;
            }
        }

        private static void BuildCampaignRoute(Transform parent)
        {
            CreateSectionTitle(parent, "EXPEDITION ROUTE", new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.96f));
            CreateMissionCard(parent, new Vector2(0.03f, 0.12f), new Vector2(0.25f, 0.78f), "01", "EARTH", "The Moon Call", "AVAILABLE", Accent);
            CreateMissionCard(parent, new Vector2(0.27f, 0.12f), new Vector2(0.49f, 0.78f), "02", "MOON", "Dustbound Delivery", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.51f, 0.12f), new Vector2(0.73f, 0.78f), "03", "MARS", "Orbiter Assembly", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.75f, 0.12f), new Vector2(0.97f, 0.78f), "04", "GANYMEDE", "The First Signal", "LOCKED", MutedText);
        }

        private static void BuildCommanderSetup(Transform parent)
        {
            Image male = CreateCard(parent, "MaleCommander", new Vector2(0.03f, 0.16f), new Vector2(0.28f, 0.9f), CardSelected);
            Button maleButton = male.gameObject.AddComponent<Button>();
            CreateLabel(male.transform, "01", 15, FontStyle.Bold, Accent, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.93f));
            CreateLabel(male.transform, "COMMANDER", 14, FontStyle.Bold, MutedText, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.78f));
            CreateLabel(male.transform, "ALEX", 28, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.64f));
            Text maleStatus = CreateLabel(male.transform, "SELECTED", 14, FontStyle.Bold, Success, new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.27f));

            Image female = CreateCard(parent, "FemaleCommander", new Vector2(0.3f, 0.16f), new Vector2(0.55f, 0.9f), Card);
            Button femaleButton = female.gameObject.AddComponent<Button>();
            CreateLabel(female.transform, "02", 15, FontStyle.Bold, Accent, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.93f));
            CreateLabel(female.transform, "COMMANDER", 14, FontStyle.Bold, MutedText, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.78f));
            CreateLabel(female.transform, "ARI", 28, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.64f));
            Text femaleStatus = CreateLabel(female.transform, "AVAILABLE", 14, FontStyle.Bold, MutedText, new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.27f));

            Image profile = CreateCard(parent, "CommanderProfile", new Vector2(0.59f, 0.16f), new Vector2(0.97f, 0.9f), Card);
            CreateLabel(profile.transform, "COMMANDER PROFILE", 17, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.8f), new Vector2(0.93f, 0.93f));
            CreateLabel(profile.transform, "CALL SIGN", 13, FontStyle.Bold, Accent, new Vector2(0.07f, 0.63f), new Vector2(0.93f, 0.76f));
            Image nameField = CreateCard(profile.transform, "CommanderName", new Vector2(0.07f, 0.43f), new Vector2(0.93f, 0.63f), new Color(0.025f, 0.04f, 0.065f, 1f));
            Text nameText = CreateLabel(nameField.transform, "CommanderNameText", 22, FontStyle.Bold, TextColor, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f));
            nameText.text = string.Empty;
            Text placeholder = CreateLabel(nameField.transform, "Enter call sign", 18, FontStyle.Normal, MutedText, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f));
            InputField input = nameField.gameObject.AddComponent<InputField>();
            input.targetGraphic = nameField;
            input.textComponent = nameText;
            input.placeholder = placeholder;
            input.characterLimit = 24;
            Button saveButton = CreateButton("SaveProfile", profile.transform, "SAVE PROFILE", Accent);
            Stretch(saveButton.GetComponent<RectTransform>(), new Vector2(0.56f, 0.3f), new Vector2(0.93f, 0.41f), Vector2.zero, Vector2.zero);
            saveButton.GetComponentInChildren<Text>().fontSize = 15;
            Text saveStatus = CreateLabel(profile.transform, "PROFILE SAVED", 12, FontStyle.Bold, Success, new Vector2(0.07f, 0.29f), new Vector2(0.52f, 0.41f));
            CreateLabel(profile.transform, "The chosen name and portrait remain with the expedition through Earth, Moon, Mars, and Ganymede.", 15, FontStyle.Normal, MutedText, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.27f));

            CommanderSetupView setup = parent.gameObject.AddComponent<CommanderSetupView>();
            setup.Configure(maleButton, femaleButton, male, female, maleStatus, femaleStatus, input, saveButton, saveStatus, CardSelected, Card, Success, MutedText);
        }

        private static void BuildMissionCards(Transform parent)
        {
            CreateMissionCard(parent, new Vector2(0.03f, 0.1f), new Vector2(0.27f, 0.9f), "01", "EARTH", "The Moon Call", "ACTIVE", Accent);
            Image moonCard = CreateMissionCard(parent, new Vector2(0.29f, 0.1f), new Vector2(0.53f, 0.9f), "02", "MOON", "The Dustbound Delivery", "LOCKED", MutedText);
            Text moonStatus = moonCard.transform.Find("LOCKED").GetComponent<Text>();
            CampaignRouteStatusView routeStatus = parent.gameObject.AddComponent<CampaignRouteStatusView>();
            routeStatus.Configure(moonCard, moonStatus, Accent, MutedText, CardSelected, Card);
            CreateMissionCard(parent, new Vector2(0.55f, 0.1f), new Vector2(0.77f, 0.9f), "03", "MARS", "Orbiter Assembly", "LOCKED", MutedText);
            CreateMissionCard(parent, new Vector2(0.79f, 0.1f), new Vector2(0.97f, 0.9f), "04", "GANYMEDE", "The First Signal", "LOCKED", MutedText);
        }

        private static void BuildBriefing(Transform parent)
        {
            Image call = CreateCard(parent, "Pole7Call", new Vector2(0.03f, 0.5f), new Vector2(0.64f, 0.92f), CardSelected);
            CreateLabel(call.transform, "INCOMING · POLE-7", 15, FontStyle.Bold, Accent, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.92f));
            const string briefingCall = "“Commander {playerName}, this is Khulan at Pole-7. Our main power panel burned out. The crew is safe, but our beacons and main relay are down.”";
            Text briefingText = CreateLabel(call.transform, briefingCall, 19, FontStyle.Bold, TextColor, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.76f));
            briefingText.gameObject.AddComponent<CommanderNameTextView>().Configure(briefingText, briefingCall);
            CreateLabel(call.transform, "KHULAN · POLE-7 OPERATOR", 14, FontStyle.Bold, MutedText, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.28f));

            Image cargo = CreateCard(parent, "RequiredCargo", new Vector2(0.67f, 0.5f), new Vector2(0.97f, 0.92f), Card);
            CreateLabel(cargo.transform, "REQUIRED CARGO", 15, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.76f), new Vector2(0.93f, 0.92f));
            CreateStatusLine(cargo.transform, "2 SLOTS", "P-4 power panel", 0.48f, Accent);
            CreateStatusLine(cargo.transform, "1 SLOT", "Insulated repair kit", 0.2f, Accent);

            CreateConstraintCard(parent, new Vector2(0.03f, 0.08f), new Vector2(0.25f, 0.44f), "BUDGET", "$115M", "Complete launch package");
            CreateConstraintCard(parent, new Vector2(0.27f, 0.08f), new Vector2(0.49f, 0.44f), "FUEL", "10+ units", "Beacon Pad minimum");
            CreateConstraintCard(parent, new Vector2(0.51f, 0.08f), new Vector2(0.73f, 0.44f), "BATTERY", "4+ kWh", "Shadow transit reserve");
            CreateConstraintCard(parent, new Vector2(0.75f, 0.08f), new Vector2(0.97f, 0.44f), "DESTINATION", "Pole-7", "Lunar south pole");
        }


        private static Image CreateCard(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            Image card = CreateImage(name, parent, color);
            Stretch(card.rectTransform, min, max, Vector2.zero, Vector2.zero);
            return card;
        }

        private static Image CreateMissionCard(Transform parent, Vector2 min, Vector2 max, string number, string title, string subtitle, string status, Color statusColor)
        {
            Image card = CreateCard(parent, title, min, max, status == "ACTIVE" || status == "AVAILABLE" ? CardSelected : Card);
            CreateLabel(card.transform, number, 17, FontStyle.Bold, Accent, new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.92f));
            CreateLabel(card.transform, title, 28, FontStyle.Bold, TextColor, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.76f));
            CreateLabel(card.transform, subtitle, 16, FontStyle.Normal, MutedText, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.5f));
            CreateLabel(card.transform, status, 14, FontStyle.Bold, statusColor, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.22f));
            return card;
        }

        private static void CreateConstraintCard(Transform parent, Vector2 min, Vector2 max, string label, string value, string note)
        {
            Image card = CreateCard(parent, label, min, max, Card);
            CreateLabel(card.transform, label, 14, FontStyle.Bold, Accent, new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.9f));
            CreateLabel(card.transform, value, 24, FontStyle.Bold, TextColor, new Vector2(0.07f, 0.32f), new Vector2(0.93f, 0.68f));
            CreateLabel(card.transform, note, 15, FontStyle.Normal, MutedText, new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.32f));
        }





        private static void CreateStatusLine(Transform parent, string status, string message, float y, Color color)
        {
            Image row = CreateCard(parent, status, new Vector2(0.07f, y), new Vector2(0.93f, y + 0.15f), new Color(color.r, color.g, color.b, 0.14f));
            CreateLabel(row.transform, status, 13, FontStyle.Bold, color, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.9f));
            CreateLabel(row.transform, message, 15, FontStyle.Normal, TextColor, new Vector2(0.3f, 0.1f), new Vector2(0.95f, 0.9f));
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
            text.supportRichText = false;
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
