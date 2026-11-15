using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SagittariusA.Minigames.UI
{
    public enum FeedbackKind { Info, Good, Bad }

    // Shared visual shell: own overlay canvas, full-screen click blocker, header, progress, timer, content area,
    // feedback line, Restart/Close buttons and the result panel. Contains no minigame rules.
    public sealed class MinigameShellView : MonoBehaviour
    {
        public const float Width = 1600, Height = 920;
        public const float ContentWidth = 1520, ContentHeight = 610;
        public const int SortingOrder = 1000;

        public RectTransform Content { get; private set; }
        public Canvas Canvas { get; private set; }
        public event Action RestartRequested;
        public event Action CloseRequested;
        public bool ShowingResult => resultView != null;

        private RectTransform panel;
        private MinigameInstructionView instruction;
        private MinigameProgressView progress;
        private Text timerText; private RectTransform timerFill; private Image timerFillImage; private GameObject timerRoot;
        private Text feedbackText, feedbackChip; private Image feedbackChipBg; private CanvasGroup feedbackGroup;
        private Button restartButton, closeButton;
        private MinigameResultView resultView;
        private Coroutine feedbackFade;

        public static MinigameShellView Create(MinigameDefinition definition, MinigameContext context, bool showTimer, bool allowRestart)
        {
            var root = new GameObject("MinigameShell_" + definition.Id, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var shell = root.AddComponent<MinigameShellView>();
            shell.Canvas = canvas;
            shell.Build(definition, context, showTimer, allowRestart);
            return shell;
        }

        private void Build(MinigameDefinition d, MinigameContext context, bool showTimer, bool allowRestart)
        {
            // Full-screen blocker: clicks never reach the 3D world or UI behind the minigame.
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(transform, false);
            Ui.Stretch((RectTransform)blocker.transform);
            var bi = blocker.GetComponent<Image>(); bi.color = MinigameTheme.Backdrop; bi.raycastTarget = true;

            var panelImg = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelImg.transform.SetParent(transform, false);
            panel = (RectTransform)panelImg.transform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(Width, Height);
            var pi = panelImg.GetComponent<Image>(); pi.sprite = MinigameTheme.Rounded; pi.type = Image.Type.Sliced; pi.color = MinigameTheme.Panel;

            string mission = string.IsNullOrEmpty(d.MissionLabel) ? (context.MissionId ?? "").ToUpperInvariant() : d.MissionLabel.ToUpperInvariant();
            Ui.Label(panel, mission + "  ·  MISSION TASK", 40, 30, 900, 22, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel, d.Title, 40, 54, 1000, 50, 36, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            instruction = MinigameInstructionView.Create(panel, d.Instruction, d.EducationalSubtitle, 40, 106, 1050);

            progress = MinigameProgressView.Create(panel, 1140, 34, 420);
            timerRoot = Ui.Rect(panel, "Timer", 1140, 104, 420, 50).gameObject;
            timerText = Ui.Label(timerRoot.transform, "", 0, 0, 420, 24, 17, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            timerFill = Ui.Meter(timerRoot.transform, "TimerBar", 0, 28, 420, 8, MinigameTheme.SoftGray, out timerFillImage);
            Ui.SetMeter(timerFill, 1);
            timerRoot.SetActive(showTimer);

            var line = Ui.Panel(panel, "Divider", 40, 176, Width - 80, 2, MinigameTheme.Line, MinigameTheme.RoundedSmall);
            Content = Ui.Rect(panel, "Content", 40, 196, ContentWidth, ContentHeight);

            var footer = Ui.Rect(panel, "Footer", 40, Height - 96, Width - 80, 64);
            feedbackGroup = footer.gameObject.AddComponent<CanvasGroup>();
            feedbackChip = Ui.Chip(footer, "INFO", 0, 16, 84, 32, MinigameTheme.SoftGray, out feedbackChipBg);
            feedbackText = Ui.Label(footer, "", 100, 8, 1000, 50, 20, MinigameTheme.WarmWhite, TextAnchor.MiddleLeft);
            float bx = Width - 80;
            if (context.AllowCancel) { bx -= 170; closeButton = Ui.Button(panel, "Close", 40 + bx, Height - 92, 160, 56, () => CloseRequested?.Invoke(), Ui.ButtonStyle.Ghost); }
            if (allowRestart) { bx -= 180; restartButton = Ui.Button(panel, "Restart", 40 + bx, Height - 92, 170, 56, () => RestartRequested?.Invoke(), Ui.ButtonStyle.Secondary); }
        }

        public void SetProgress(float value, string label) { progress?.Set(value, label); }

        public void SetTimer(float remaining, float limit)
        {
            if (timerRoot == null || !timerRoot.activeSelf) return;
            int s = Mathf.CeilToInt(Mathf.Max(0, remaining));
            timerText.text = $"TIME LEFT  {s / 60}:{s % 60:00}";
            Ui.SetMeter(timerFill, limit > 0 ? remaining / limit : 1);
            bool low = limit > 0 && remaining <= Mathf.Min(10f, limit * .2f);
            timerText.color = timerFillImage.color = low ? MinigameTheme.Warn : MinigameTheme.SoftGray;
            if (low) timerText.text += "  · hurry";
        }

        public void ShowFeedback(string message, FeedbackKind kind)
        {
            feedbackText.text = message;
            var color = kind == FeedbackKind.Good ? MinigameTheme.Good : kind == FeedbackKind.Bad ? MinigameTheme.Bad : MinigameTheme.SoftGray;
            Ui.SetChip(feedbackChip, feedbackChipBg, kind == FeedbackKind.Good ? "OK" : kind == FeedbackKind.Bad ? "CHECK" : "INFO", color);
            if (feedbackFade != null) StopCoroutine(feedbackFade);
            feedbackFade = StartCoroutine(Pulse());
        }
        private IEnumerator Pulse()
        {
            // Short, non-flashing emphasis: slide the line in from the left.
            var rt = (RectTransform)feedbackText.transform; float x0 = 100;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .14f)
            { rt.anchoredPosition = new Vector2(x0 - 12 * (1 - t), rt.anchoredPosition.y); feedbackGroup.alpha = .4f + .6f * t; yield return null; }
            rt.anchoredPosition = new Vector2(x0, rt.anchoredPosition.y); feedbackGroup.alpha = 1;
        }

        public void SetInteractable(bool value)
        {
            if (restartButton != null) restartButton.interactable = value;
            var cg = Content.GetComponent<CanvasGroup>();
            if (cg == null) cg = Content.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = value;
        }

        public void ShowResult(MinigameResult result, Action onContinue)
        {
            SetInteractable(false);
            if (closeButton != null) closeButton.interactable = false;
            resultView = MinigameResultView.Create(panel, result, onContinue);
        }

        public Selectable CloseButton => closeButton;
        public Selectable RestartButton => restartButton;
    }
}
