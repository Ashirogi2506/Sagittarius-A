using System;
using System.Collections.Generic;

namespace SagittariusA.Minigames.EarthImageCalibration
{
    public enum SpectralBand { Visible, Infrared, Multispectral }

    // Rules for "Satellite Image Calibration". No Unity dependency.
    public sealed class ImageCalibrationRules
    {
        public float TargetFocus { get; }
        public float TargetExposure { get; }
        public SpectralBand TargetBand { get; }
        public float Tolerance { get; }
        public float TimeLimit { get; }

        public float Focus { get; private set; }
        public float Exposure { get; private set; }
        public SpectralBand Band { get; private set; }
        public int Mistakes { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public MinigameOutcome? Outcome { get; private set; }
        private readonly float startFocus, startExposure; private readonly SpectralBand startBand;

        public ImageCalibrationRules(MinigameContext context, float timeLimitSeconds)
        {
            var rng = MinigameRandom.Create(context, "earth-image-calibration");
            int level = MinigameDifficulty.Clamp(context?.DifficultyLevel ?? 1);
            Tolerance = level == 1 ? 0.08f : level == 2 ? 0.07f : 0.06f;
            TimeLimit = timeLimitSeconds;
            TargetFocus = Round(0.2f + (float)rng.NextDouble() * 0.6f);
            TargetExposure = Round(0.25f + (float)rng.NextDouble() * 0.5f);
            TargetBand = (SpectralBand)rng.Next(3);
            // Start clearly off target on every control so the player has to work all three.
            startFocus = Round(TargetFocus > 0.5f ? TargetFocus - 0.3f - (float)rng.NextDouble() * 0.15f : TargetFocus + 0.3f + (float)rng.NextDouble() * 0.15f);
            startExposure = Round(TargetExposure > 0.5f ? TargetExposure - 0.28f - (float)rng.NextDouble() * 0.12f : TargetExposure + 0.28f + (float)rng.NextDouble() * 0.12f);
            startBand = (SpectralBand)(((int)TargetBand + 1 + rng.Next(2)) % 3);
            Reset();
        }
        private static float Round(float v) => (float)Math.Round(Math.Max(0, Math.Min(1, v)), 3);

        // Restart puts the controls back; mistakes and the clock keep running so restart is never a free retry.
        public void Reset() { Focus = startFocus; Exposure = startExposure; Band = startBand; }

        public void SetFocus(float value) { if (!Finished) Focus = Clamp01(value); }
        public void SetExposure(float value) { if (!Finished) Exposure = Clamp01(value); }
        public void SetBand(SpectralBand band) { if (!Finished) Band = band; }
        private static float Clamp01(float v) => float.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;

        public bool FocusCorrect => Math.Abs(Focus - TargetFocus) <= Tolerance + 1e-4f;
        public bool ExposureCorrect => Math.Abs(Exposure - TargetExposure) <= Tolerance + 1e-4f;
        public bool BandCorrect => Band == TargetBand;
        public int CorrectCount => (FocusCorrect ? 1 : 0) + (ExposureCorrect ? 1 : 0) + (BandCorrect ? 1 : 0);
        public float Progress => CorrectCount / 3f;

        // 0 = sharp. Used by the view to pick blur level; never shows the target number.
        public float FocusError => Math.Abs(Focus - TargetFocus);
        public float ExposureError => Exposure - TargetExposure;

        // Specific feedback for every wrong setting, in a fixed order.
        public List<string> Problems()
        {
            var list = new List<string>();
            if (!FocusCorrect) list.Add("Image is out of focus.");
            if (!ExposureCorrect) list.Add(Exposure > TargetExposure ? "Image is overexposed." : "Image is underexposed.");
            if (!BandCorrect) list.Add("Incorrect spectral band.");
            return list;
        }

        // Returns the feedback line. A wrong confirmation costs one mistake.
        public string Confirm()
        {
            if (Finished) return "Calibration already finished.";
            var problems = Problems();
            if (problems.Count == 0)
            {
                Finished = true;
                Outcome = Mistakes <= 2 ? MinigameOutcome.Success : MinigameOutcome.PartialSuccess;
                return "Calibration confirmed. The image matches the target.";
            }
            Mistakes++;
            return string.Join(" ", problems);
        }

        // Advances the clock; returns true when the time limit has just expired.
        public bool Tick(float dt)
        {
            if (Finished || dt <= 0) return false;
            Elapsed += dt;
            if (TimeLimit > 0 && Elapsed >= TimeLimit)
            {
                Elapsed = TimeLimit; Finished = true;
                Outcome = CorrectCount >= 2 ? MinigameOutcome.PartialSuccess : MinigameOutcome.Failure;
                return true;
            }
            return false;
        }

        public int Score => MinigameScoring.Standard(Mistakes, Elapsed, TimeLimit);

        public const string EducationalNote = "Incorrect focus, exposure, or wavelength selection can reduce the scientific value of satellite observations.";

        public MinigameResult BuildResult(MinigameContext context)
        {
            var outcome = Outcome ?? MinigameOutcome.Failure;
            var r = MinigameResult.For(context, outcome, outcome switch {
                MinigameOutcome.Success => "Sensor calibrated. Images are sharp, correctly exposed and in the right band.",
                MinigameOutcome.PartialSuccess when CorrectCount == 3 => $"Calibrated after {Mistakes} wrong confirmations. Some observation time was lost.",
                MinigameOutcome.PartialSuccess => $"Time ran out with {CorrectCount} of 3 settings correct. Data quality is reduced.",
                _ => $"Time ran out with {CorrectCount} of 3 settings correct. Most images are unusable.",
            });
            r.Score = outcome == MinigameOutcome.Failure ? Math.Min(Score, 40) : Score;
            r.MistakeCount = Mistakes; r.CompletionTimeSeconds = Elapsed; r.CompletionPercent = Progress;
            r.EducationalNotes.Add(EducationalNote);
            switch (outcome)
            {
                case MinigameOutcome.Success:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ScienceReturnModifier, 0.10f, "Well-calibrated images return more science."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.DataQualityModifier, 0.10f, "Sharp, correctly exposed data."));
                    break;
                case MinigameOutcome.PartialSuccess:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.DataQualityModifier, -0.10f, "Some images were taken before calibration was right."));
                    break;
                default:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.DataQualityModifier, -0.30f, "Images are blurred, badly exposed or in the wrong band."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ScienceReturnModifier, -0.20f, "Less usable observation data."));
                    break;
            }
            return r;
        }
    }
}
