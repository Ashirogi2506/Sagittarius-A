using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Minigames.EarthImageCalibration
{
    // Earth · Satellite Image Calibration. Rules: ImageCalibrationRules.
    // The live preview is layered UI images: two blur levels cross-faded for focus, a white/black veil for exposure,
    // and a band-specific image set for the spectral band. No shaders or extra packages.
    [Minigame(IdValue)]
    public sealed class EarthImageCalibrationMinigame : MinigameBase
    {
        public const string IdValue = "earth-image-calibration";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "earth", MissionLabel = "Earth observation", Title = "Satellite Image Calibration",
            Instruction = "Match the target image by adjusting focus, exposure, and spectral band.",
            EducationalSubtitle = "Useful science data needs a sharp image, the right brightness and the right wavelength.",
            DefaultTimeLimitSeconds = 60f, EducationalNote = ImageCalibrationRules.EducationalNote };
        public override string Id => IdValue;

        private ImageCalibrationRules rules;
        private Image liveA, liveB, veil;
        private Slider focus, exposure;
        private Button[] bandButtons;
        private Text[] indicatorState; private Image[] indicatorChip;
        private Button confirm;
        private static readonly string[] BandNames = { "Visible", "Infrared", "Multispectral" };

        protected override float Elapsed => rules.Elapsed;
        protected override void OnInitialize() { rules = new ImageCalibrationRules(Context, TimeLimit); }

        protected override void BuildContent(RectTransform c)
        {
            // Target image.
            var target = Ui.Panel(c, "TargetCard", 0, 0, 600, 420, MinigameTheme.Card);
            Ui.Label(target.transform, "TARGET IMAGE", 22, 18, 400, 22, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(target.transform, "Reference frame from the science team", 22, 40, 560, 22, 15, MinigameTheme.SoftGray);
            var t = LiveImage(target.transform, "Target", 20, 72, 560, 330);
            t.sprite = Frame(rules.TargetBand, 0);
            // Live preview.
            var live = Ui.Panel(c, "LiveCard", 630, 0, 890, 420, MinigameTheme.Card);
            Ui.Label(live.transform, "LIVE SENSOR", 22, 18, 400, 22, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(live.transform, "Adjust until it looks like the target", 22, 40, 560, 22, 15, MinigameTheme.SoftGray);
            liveA = LiveImage(live.transform, "LiveA", 20, 72, 560, 330);
            liveB = LiveImage(live.transform, "LiveB", 20, 72, 560, 330);
            veil = LiveImage(live.transform, "ExposureVeil", 20, 72, 560, 330); veil.sprite = null;
            // Accuracy indicators: label + state word + chip colour.
            string[] names = { "FOCUS", "EXPOSURE", "SPECTRAL BAND" };
            indicatorState = new Text[3]; indicatorChip = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Ui.Panel(live.transform, "Indicator" + i, 600, 72 + i * 112, 270, 100, MinigameTheme.Panel);
                Ui.Label(row.transform, names[i], 16, 14, 240, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
                indicatorState[i] = Ui.Chip(row.transform, "ADJUST", 16, 48, 140, 34, MinigameTheme.Warn, out indicatorChip[i]);
            }

            // Controls.
            var controls = Ui.Panel(c, "Controls", 0, 440, 1520, 170, MinigameTheme.Card);
            Ui.Label(controls.transform, "FOCUS", 24, 20, 200, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            focus = Ui.Slider(controls.transform, "Focus", 24, 44, 520, 40, v => { rules.SetFocus(v); Refresh(); });
            Ui.Label(controls.transform, "EXPOSURE", 24, 96, 200, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            exposure = Ui.Slider(controls.transform, "Exposure", 24, 120, 520, 40, v => { rules.SetExposure(v); Refresh(); });
            Ui.Label(controls.transform, "SPECTRAL BAND", 600, 20, 300, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            bandButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var band = (SpectralBand)i;
                bandButtons[i] = Ui.Button(controls.transform, BandNames[i], 600 + i * 196, 50, 184, 72, () => { rules.SetBand(band); Feedback($"{BandNames[(int)band]} band selected.", FeedbackKind.Info); Refresh(); });
            }
            confirm = Ui.Button(controls.transform, "Confirm calibration", 1222, 40, 274, 92, Confirm, Ui.ButtonStyle.Primary, 21);

            focus.SetValueWithoutNotify(rules.Focus); exposure.SetValueWithoutNotify(rules.Exposure);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
            Feedback("Compare the live sensor with the target. Use the sliders and band buttons, then confirm.", FeedbackKind.Info);
        }

        private Image LiveImage(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Ui.Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.raycastTarget = false; img.preserveAspect = false;
            return img;
        }

        private static Sprite Frame(SpectralBand band, int level)
        {
            var tex = Resources.Load<Texture2D>($"Minigames/Earth/eo_{band.ToString().ToLowerInvariant()}_{level}");
            if (tex == null) { Debug.LogWarning("[Minigames] Missing Earth observation image " + band + level); return null; }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
        }
        private readonly Sprite[,] frames = new Sprite[3, 4];
        private Sprite Cached(SpectralBand b, int l) => frames[(int)b, l] != null ? frames[(int)b, l] : frames[(int)b, l] = Frame(b, l);

        private void Confirm()
        {
            if (!IsRunning) return;
            string message = rules.Confirm();
            if (rules.Finished) { Feedback(message, FeedbackKind.Good); Finish(); }
            else Feedback(message + $"  (mistakes: {rules.Mistakes})", FeedbackKind.Bad);
            Refresh();
        }

        protected override bool OnTick(float dt) => rules.Tick(dt);

        protected override void OnRestart()
        {
            rules.Reset();
            focus.SetValueWithoutNotify(rules.Focus); exposure.SetValueWithoutNotify(rules.Exposure);
            Refresh();
        }

        protected override void Refresh()
        {
            if (liveA == null) return;
            // Focus: blur level 0..3 from the distance to the target (inside tolerance looks sharp).
            float level = Mathf.Clamp((rules.FocusError - rules.Tolerance * .6f) / .3f * 3f, 0, 3);
            int lo = Mathf.FloorToInt(level), hi = Mathf.Min(3, lo + 1);
            liveA.sprite = Cached(rules.Band, lo); liveB.sprite = Cached(rules.Band, hi);
            liveB.color = new Color(1, 1, 1, level - lo);
            // Exposure: white veil when too bright, dark veil when too dark.
            float e = rules.ExposureError;
            veil.color = e > 0 ? new Color(1, .98f, .94f, Mathf.Min(.85f, e * 1.9f)) : new Color(0, .01f, .03f, Mathf.Min(.88f, -e * 2f));

            SetIndicator(0, rules.FocusCorrect, rules.FocusCorrect ? "SHARP" : "BLURRED");
            SetIndicator(1, rules.ExposureCorrect, rules.ExposureCorrect ? "BALANCED" : "ADJUST");
            SetIndicator(2, rules.BandCorrect, rules.BandCorrect ? "MATCHED" : "MISMATCH");
            for (int i = 0; i < 3; i++)
            {
                bool selected = (int)rules.Band == i;
                ((Image)bandButtons[i].targetGraphic).color = selected ? MinigameTheme.Orange : MinigameTheme.CardRaised;
                Ui.SetButtonLabel(bandButtons[i], selected ? "● " + BandNames[i] : BandNames[i]);
                bandButtons[i].GetComponentInChildren<Text>().color = selected ? new Color(.1f, .07f, .05f) : MinigameTheme.WarmWhite;
            }
            Shell.SetProgress(rules.Progress, $"Calibration {rules.CorrectCount} / 3");
            confirm.interactable = IsRunning;
        }

        private void SetIndicator(int i, bool ok, string word) =>
            Ui.SetChip(indicatorState[i], indicatorChip[i], (ok ? "OK  " : "") + word, ok ? MinigameTheme.Good : MinigameTheme.Warn);

        protected override MinigameResult BuildResult() => rules.BuildResult(Context);
    }
}
