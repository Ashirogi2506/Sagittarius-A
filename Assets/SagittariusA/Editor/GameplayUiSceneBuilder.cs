using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.UI;

namespace SagittariusA.Gameplay.Editor
{
    public static class GameplayUiSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameplayUI.unity";
        private static readonly Color Background = new Color(0.025f, 0.045f, 0.09f, 1f);
        private static readonly Color Panel = new Color(0.07f, 0.11f, 0.18f, 0.98f);
        private static readonly Color Accent = new Color(0.12f, 0.55f, 0.92f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.96f, 1f, 1f);

        [InitializeOnLoadMethod]
        private static void QueueInitialSceneBuild()
        {
            EditorApplication.update -= BuildInitialSceneWhenReady;
            EditorApplication.update += BuildInitialSceneWhenReady;
        }

        private static void BuildInitialSceneWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= BuildInitialSceneWhenReady;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildGameplayUiScene();
            }
        }

        [MenuItem("Sagittarius A/Build Gameplay UI Scene")]
        public static void BuildGameplayUiScene()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            GameObject root = new GameObject("GameplayUIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", root.transform, Background);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject cameraObject = new GameObject("UI Test Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.cullingMask = 0;
            camera.depth = -100f;

            GameObject flowRoot = new GameObject("ScreenFlow", typeof(RectTransform), typeof(GameFlowController));
            flowRoot.transform.SetParent(root.transform, false);
            Stretch(flowRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateScreen(flowRoot.transform, GameScreen.MainMenu, "SAGITTARIUS A", "Design missions. Understand the trade-offs. Reach farther.", "Start Campaign", GameScreen.MissionSelection, null, GameScreen.MainMenu);
            CreateScreen(flowRoot.transform, GameScreen.MissionSelection, "MISSION SELECTION", "Earth Observation is available. Moon, Mars, and Jupiter unlock as the campaign progresses.", "Open Earth Mission", GameScreen.Briefing, "Back", GameScreen.MainMenu);
            CreateScreen(flowRoot.transform, GameScreen.Briefing, "EARTH OBSERVATION BRIEFING", "Configure a scientifically useful Earth-observation spacecraft while staying within mission limits.", "Start Configuration", GameScreen.Configuration, "Back", GameScreen.MissionSelection);
            CreateScreen(flowRoot.transform, GameScreen.Configuration, "SPACECRAFT CONFIGURATION", "Select instruments, spacecraft systems, an orbit, and a launcher. Resource cards will be connected next.", "Review Design", GameScreen.Review, "Back", GameScreen.Briefing);
            CreateScreen(flowRoot.transform, GameScreen.Review, "MISSION REVIEW", "Review resource totals, compatibility messages, and expected risks before launch.", "Launch", GameScreen.Simulation, "Back", GameScreen.Configuration);
            CreateScreen(flowRoot.transform, GameScreen.Simulation, "MISSION SIMULATION", "The 3D mission visualization and simulation status will appear here.", "View Results", GameScreen.Results, null, GameScreen.Simulation);
            CreateScreen(flowRoot.transform, GameScreen.Results, "POST-MISSION REPORT", "Success, score breakdown, scientific results, and explanations will appear here.", "Retry Configuration", GameScreen.Configuration, "Mission Selection", GameScreen.MissionSelection);

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.transform.SetParent(root.transform, false);
            InputSystemUIInputModule inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            EditorSceneManager.CloseScene(scene, true);

            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            Debug.Log($"Created gameplay UI scene at {ScenePath}.");
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
            RectTransform panelRect = panel.rectTransform;
            Stretch(panelRect, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);

            Text eyebrow = CreateText("FlowLabel", panel.transform, "EARTH VERTICAL SLICE", 20, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            SetRect(eyebrow.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);

            Text title = CreateText("Heading", panel.transform, heading, 48, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

            Text body = CreateText("Description", panel.transform, description, 26, FontStyle.Normal, TextColor, TextAnchor.UpperLeft);
            SetRect(body.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero);

            Button primary = CreateButton("PrimaryAction", panel.transform, primaryLabel, Accent);
            SetRect(primary.GetComponent<RectTransform>(), new Vector2(0.62f, 0.1f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);

            Button secondary = null;
            if (!string.IsNullOrWhiteSpace(secondaryLabel))
            {
                secondary = CreateButton("SecondaryAction", panel.transform, secondaryLabel, new Color(0.18f, 0.23f, 0.31f, 1f));
                SetRect(secondary.GetComponent<RectTransform>(), new Vector2(0.34f, 0.1f), new Vector2(0.6f, 0.24f), Vector2.zero, Vector2.zero);
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

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(scene => scene.path == ScenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = scenes
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }
    }
}
