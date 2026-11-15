using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.Flow
{
    [Serializable] public sealed class CampaignDialogueLine { public string speaker, line; }
    [Serializable] public sealed class CampaignDialogueSequence { public string trigger; public CampaignDialogueLine[] lines; }
    [Serializable] public sealed class CampaignDialogueCatalog { public CampaignDialogueSequence[] entries; }

    // A subtitle presenter only. Mission progression remains owned by the existing controllers.
    public sealed class CampaignDialogueDirector : MonoBehaviour
    {
        private GameFlowController flow;
        private EarthGameplaySession earth;
        private MoonStageController moon;
        private CampaignDialogueCatalog catalog;
        private GameObject canvasRoot;
        private Text speakerText, lineText;
        private CampaignDialogueSequence current;
        private readonly Queue<CampaignDialogueSequence> queue = new Queue<CampaignDialogueSequence>();
        private bool currentMoon, moonObserved, previousTestPassed;
        private MoonStagePhase previousPhase;
        private int lineIndex;
        private int lastCompleteFrame = -1;
        private GameObject previousSelection;
        private float remaining;

        public void Initialize(GameFlowController gameFlow, EarthGameplaySession session, MoonStageController moonStage)
        {
            flow = gameFlow; earth = session; moon = moonStage;
            var asset = Resources.Load<TextAsset>("CampaignDialogue");
            if (asset != null) catalog = JsonUtility.FromJson<CampaignDialogueCatalog>(asset.text);
            if (catalog == null || catalog.entries == null) { Debug.LogWarning("Campaign dialogue data is missing."); return; }
            flow.ScreenChanged += OnScreenChanged;
            earth.ActivityCompleted += OnActivityCompleted;
            moon.Changed += OnMoonChanged;
            ObserveMoon();
        }

        private void OnScreenChanged(GameScreen previous, GameScreen next)
        {
            if (current != null && !currentMoon && next != previous) CancelCurrent();
            if (next == GameScreen.Briefing) Enqueue("earth_opening", false);
            else if (next == GameScreen.Configuration) Enqueue("garage_first_open", false);
            else if (next == GameScreen.Results) Enqueue("orbit_reached", false);
        }
        private void OnActivityCompleted(EarthActivity activity)
        {
            if (activity == EarthActivity.Cargo) Enqueue("required_cargo_secured", false);
        }
        private void OnMoonChanged()
        {
            if (!moon.Initialized) { moonObserved = false; return; }
            if (moonObserved && previousPhase == MoonStagePhase.Descent && moon.Phase == MoonStagePhase.Pole7CartRepair)
                Enqueue("moon_arrival", true);
            if (moonObserved && !previousTestPassed && moon.CartTestPassed)
                Enqueue("moon_wheel_passed", true);
            ObserveMoon();
        }
        private void ObserveMoon()
        {
            if (!moon.Initialized) return;
            previousPhase = moon.Phase; previousTestPassed = moon.CartTestPassed; moonObserved = true;
        }
        private void Enqueue(string id, bool isMoon)
        {
            if (isMoon ? moon.HasPlayedDialogue(id) : earth.HasPlayedDialogue(id)) return;
            if ((current != null && current.trigger == id) || queue.Any(x => x.trigger == id)) return;
            var sequence = catalog.entries.FirstOrDefault(x => x != null && x.trigger == id && x.lines != null && x.lines.Length > 0);
            if (sequence == null) return;
            queue.Enqueue(sequence);
            if (current == null) StartNext();
        }
        private void StartNext()
        {
            if (queue.Count == 0) { DestroyView(); return; }
            current = queue.Dequeue(); currentMoon = current.trigger.StartsWith("moon_", StringComparison.Ordinal);
            lineIndex = 0; EnsureView(); ShowLine();
        }
        private void ShowLine()
        {
            var line = current.lines[lineIndex];
            string name = PlayerPrefs.GetString("FarSignal.CommanderName", "Alex");
            if (string.IsNullOrWhiteSpace(name)) name = "Alex";
            speakerText.text = (line.speaker ?? "").Replace("{playerName}", name);
            lineText.text = (line.line ?? "").Replace("{playerName}", name);
            remaining = Mathf.Clamp(2.5f + lineText.text.Length / 15f, 4f, 12f);
        }
        private void Update()
        {
            if (current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame) { CompleteCurrent(); return; }
            // Do not advance dialogue while mission interactions or player movement require attention.
            if (earth.Busy || (currentMoon && keyboard != null && (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed ||
                keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed))) return;
            remaining -= Time.unscaledDeltaTime;
            if (remaining > 0) return;
            if (++lineIndex < current.lines.Length) ShowLine(); else CompleteCurrent();
        }
        private void CompleteCurrent()
        {
            if (current == null || lastCompleteFrame == Time.frameCount) return;
            lastCompleteFrame = Time.frameCount;
            if (currentMoon) moon.MarkDialoguePlayed(current.trigger); else earth.MarkDialoguePlayed(current.trigger);
            current = null; StartNext();
        }
        private void CancelCurrent() { current = null; queue.Clear(); DestroyView(); }
        private void EnsureView()
        {
            if (canvasRoot != null) return;
            canvasRoot = new GameObject("Campaign subtitles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Subtitle panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasRoot.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.18f, .025f); rect.anchorMax = new Vector2(.82f, .22f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.025f, .04f, .07f, .92f);
            speakerText = Label(panel.transform, "Speaker", new Vector2(.035f, .72f), new Vector2(.82f, .96f), 22, TextAnchor.MiddleLeft);
            lineText = Label(panel.transform, "Line", new Vector2(.035f, .18f), new Vector2(.965f, .72f), 25, TextAnchor.MiddleLeft);
            var button = new GameObject("Skip dialogue", typeof(RectTransform), typeof(Image), typeof(Button)); button.transform.SetParent(panel.transform, false);
            var br = button.GetComponent<RectTransform>(); br.anchorMin = new Vector2(.83f, .73f); br.anchorMax = new Vector2(.97f, .95f); br.offsetMin = br.offsetMax = Vector2.zero;
            button.GetComponent<Image>().color = new Color(.17f, .22f, .30f, 1f); button.GetComponent<Button>().onClick.AddListener(CompleteCurrent);
            Label(button.transform, "Skip · Enter", Vector2.zero, Vector2.one, 17, TextAnchor.MiddleCenter);
            if (EventSystem.current != null) { previousSelection = EventSystem.current.currentSelectedGameObject; EventSystem.current.SetSelectedGameObject(button); }
        }
        private static Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.color = Color.white; text.alignment = alignment; text.raycastTarget = false; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private void DestroyView()
        {
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
            if (canvasRoot != null) Destroy(canvasRoot); canvasRoot = null;
        }
        private void OnDestroy()
        {
            if (flow != null) flow.ScreenChanged -= OnScreenChanged;
            if (earth != null) earth.ActivityCompleted -= OnActivityCompleted;
            if (moon != null) moon.Changed -= OnMoonChanged;
            DestroyView();
        }
    }
}
