using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Minigames.UI
{
    // One-sentence instruction (always visible) plus the optional educational subtitle.
    public sealed class MinigameInstructionView : MonoBehaviour
    {
        public static MinigameInstructionView Create(Transform parent, string instruction, string subtitle, float x, float y, float w)
        {
            var root = Ui.Rect(parent, "Instruction", x, y, w, 64);
            var v = root.gameObject.AddComponent<MinigameInstructionView>();
            Ui.Label(root, instruction, 0, 0, w, 30, 21, MinigameTheme.WarmWhite);
            if (!string.IsNullOrEmpty(subtitle)) Ui.Label(root, subtitle, 0, 34, w, 26, 16, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Italic);
            return v;
        }
    }

    // "Calibration 2 / 3" with a meter and percentage. Animates toward the new value.
    public sealed class MinigameProgressView : MonoBehaviour
    {
        private Text label, percent; private RectTransform fill; private float shown, target;

        public static MinigameProgressView Create(Transform parent, float x, float y, float w)
        {
            var root = Ui.Rect(parent, "Progress", x, y, w, 60);
            var v = root.gameObject.AddComponent<MinigameProgressView>();
            v.label = Ui.Label(root, "PROGRESS", 0, 0, w - 80, 24, 17, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            v.percent = Ui.Label(root, "0%", w - 80, 0, 80, 24, 17, MinigameTheme.WarmWhite, TextAnchor.UpperRight, FontStyle.Bold);
            v.fill = Ui.Meter(root, "ProgressBar", 0, 30, w, 14, MinigameTheme.Orange, out _);
            Ui.SetMeter(v.fill, 0);
            return v;
        }

        public void Set(float value, string text)
        {
            value = Mathf.Clamp01(value);
            if (value > target + 1e-3f) MinigameAudio.Play(MinigameCue.Progress, .6f);
            target = value;
            label.text = (text ?? "PROGRESS").ToUpperInvariant();
            percent.text = Mathf.RoundToInt(value * 100) + "%";
        }

        private void Update()
        {
            if (Mathf.Abs(shown - target) < 1e-3f) return;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
            Ui.SetMeter(fill, shown);
        }
    }

    // Outcome card: icon shape + word (never color alone), summary, stats, educational note, suggested effects, Continue.
    public sealed class MinigameResultView : MonoBehaviour
    {
        public static MinigameResultView Create(RectTransform panel, MinigameResult r, Action onContinue)
        {
            var dim = Ui.Panel(panel, "ResultDim", 0, 0, MinigameShellView.Width, MinigameShellView.Height, new Color(.03f, .045f, .08f, .82f), null, true);
            var v = dim.gameObject.AddComponent<MinigameResultView>();
            const float W = 900, H = 640;
            var card = Ui.Panel(dim.transform, "ResultCard", (MinigameShellView.Width - W) / 2, (MinigameShellView.Height - H) / 2, W, H, MinigameTheme.Card);

            Color tone = r.Outcome == MinigameOutcome.Success ? MinigameTheme.Good : r.Outcome == MinigameOutcome.PartialSuccess ? MinigameTheme.Warn
                : r.Outcome == MinigameOutcome.Failure ? MinigameTheme.Bad : MinigameTheme.SoftGray;
            string heading = r.Outcome == MinigameOutcome.Success ? "Task complete" : r.Outcome == MinigameOutcome.PartialSuccess ? "Partial success"
                : r.Outcome == MinigameOutcome.Failure ? "Task failed" : "Task cancelled";
            Badge(card.transform, r.Outcome, tone, 48, 44);
            Ui.Label(card.transform, r.Outcome.ToString().ToUpperInvariant(), 180, 48, 600, 22, 15, tone, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(card.transform, heading, 180, 72, 680, 46, 34, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(card.transform, r.Summary, 180, 122, 680, 60, 19, MinigameTheme.SoftGray);

            string[] names = { "SCORE", "MISTAKES", "TIME", "COMPLETE" };
            string[] values = { r.Score.ToString(), r.MistakeCount.ToString(), $"{Mathf.FloorToInt(r.CompletionTimeSeconds / 60)}:{Mathf.FloorToInt(r.CompletionTimeSeconds % 60):00}", Mathf.RoundToInt(r.CompletionPercent * 100) + "%" };
            for (int i = 0; i < 4; i++)
            {
                var s = Ui.Panel(card.transform, "Stat" + i, 48 + i * 204, 200, 192, 92, MinigameTheme.Panel);
                Ui.Label(s.transform, names[i], 16, 12, 170, 20, 14, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
                Ui.Label(s.transform, values[i], 16, 36, 170, 44, 32, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            }

            var note = Ui.Panel(card.transform, "WhyItMatters", 48, 312, W - 96, 120, MinigameTheme.Panel);
            Ui.Label(note.transform, "WHY IT MATTERS", 18, 14, 400, 20, 14, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(note.transform, r.EducationalNotes.FirstOrDefault() ?? "", 18, 38, W - 132, 76, 18, MinigameTheme.WarmWhite);

            string effects = r.SuggestedEffects.Count == 0 ? "No mission effects." :
                "Suggested mission effects:  " + string.Join("   ·   ", r.SuggestedEffects.Select(Describe));
            if (r.MissionId == "pole-7" && r.EventId == "pole7.base.essential")
                effects = r.Outcome == MinigameOutcome.Success ? "Continue to restore Pole-7's essential line." :
                    "Your earlier repairs are saved. Continue, then use the panel to retry.";
            Ui.Label(card.transform, effects, 48, 448, W - 96, 60, 16, MinigameTheme.SoftGray);

            var cont = Ui.Button(card.transform, "Continue", W - 48 - 240, H - 48 - 64, 240, 64, () => onContinue?.Invoke(), Ui.ButtonStyle.Primary, 22);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cont.gameObject);

            MinigameAudio.Play(r.Outcome == MinigameOutcome.Success ? MinigameCue.Success : r.Outcome == MinigameOutcome.PartialSuccess ? MinigameCue.Partial : MinigameCue.Failure);
            v.StartCoroutine(v.Pop(card.rectTransform));
            return v;
        }

        public static string Describe(MinigameEffect e)
        {
            string name = e.EffectType switch {
                MinigameEffectTypes.DataQualityModifier => "Data quality", MinigameEffectTypes.ScienceReturnModifier => "Science return",
                MinigameEffectTypes.PowerAvailabilityModifier => "Power availability", MinigameEffectTypes.ReliabilityModifier => "Reliability",
                MinigameEffectTypes.RadiationDamageModifier => "Radiation damage", MinigameEffectTypes.MissionTimePenalty => "Mission time", _ => e.EffectType };
            return e.EffectType == MinigameEffectTypes.MissionTimePenalty ? $"{name} +{e.Value:0.#} step" + (Mathf.Abs(e.Value - 1) < .01f ? "" : "s")
                : $"{name} {(e.Value >= 0 ? "+" : "−")}{Mathf.Abs(e.Value) * 100:0}%";
        }

        // Shape-coded badge: check (success), half disc (partial), cross (failure), dash (cancelled).
        private static void Badge(Transform parent, MinigameOutcome outcome, Color tone, float x, float y)
        {
            var ring = Ui.Panel(parent, "Badge", x, y, 108, 108, tone, MinigameTheme.Circle);
            var inner = Ui.Panel(ring.transform, "Inner", 8, 8, 92, 92, MinigameTheme.Card, MinigameTheme.Circle);
            RectTransform Bar(float bx, float by, float w, float h, float angle)
            {
                var b = Ui.Panel(inner.transform, "Mark", bx, by, w, h, tone, MinigameTheme.RoundedSmall).rectTransform;
                b.pivot = new Vector2(.5f, .5f); b.anchoredPosition = new Vector2(bx + w / 2, -(by + h / 2)); b.localRotation = Quaternion.Euler(0, 0, angle);
                return b;
            }
            switch (outcome)
            {
                case MinigameOutcome.Success: Bar(18, 50, 30, 12, -45); Bar(32, 40, 50, 12, 45); break;
                case MinigameOutcome.PartialSuccess:
                    var half = Ui.Panel(inner.transform, "Half", 14, 14, 64, 64, tone, MinigameTheme.Circle);
                    half.type = Image.Type.Filled; half.fillMethod = Image.FillMethod.Horizontal; half.fillAmount = .5f; break;
                case MinigameOutcome.Failure: Bar(18, 40, 56, 12, 45); Bar(18, 40, 56, 12, -45); break;
                default: Bar(22, 40, 48, 12, 0); break;
            }
        }

        private IEnumerator Pop(RectTransform card)
        {
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .16f) { float s = .94f + .06f * t; card.localScale = new Vector3(s, s, 1); yield return null; }
            card.localScale = Vector3.one;
        }
    }
}
