using System.Linq;
using NUnit.Framework;
using SagittariusA.Minigames.EarthImageCalibration;
using SagittariusA.Minigames.JupiterShieldAllocation;
using SagittariusA.Minigames.MarsPowerManagement;
using SagittariusA.Minigames.MoonPowerRouting;

namespace SagittariusA.Minigames.Tests
{
    public class EarthImageCalibrationTests
    {
        private static ImageCalibrationRules New(int seed = 12345, int level = 1) =>
            new ImageCalibrationRules(new MinigameContext { MinigameId = "earth-image-calibration", RandomSeed = seed, DifficultyLevel = level }, 60f);
        private static void Solve(ImageCalibrationRules r) { r.SetFocus(r.TargetFocus); r.SetExposure(r.TargetExposure); r.SetBand(r.TargetBand); }

        [Test] public void StartsOffTargetOnEveryControl()
        {
            for (int seed = 0; seed < 50; seed++) { var r = New(seed); Assert.AreEqual(0, r.CorrectCount, "seed " + seed); }
        }
        [Test] public void ValuesInsideToleranceCountAsCorrect()   // 8
        {
            var r = New();
            r.SetFocus(r.TargetFocus + r.Tolerance * 0.95f); r.SetExposure(r.TargetExposure - r.Tolerance * 0.95f);
            Assert.IsTrue(r.FocusCorrect); Assert.IsTrue(r.ExposureCorrect);
            r.SetFocus(r.TargetFocus + r.Tolerance + 0.02f); Assert.IsFalse(r.FocusCorrect);
            Assert.AreEqual(0.08f, New(level: 1).Tolerance, 1e-6);
        }
        [Test] public void OutsideToleranceGivesSpecificFeedback()   // 9
        {
            var r = New(); Solve(r);
            r.SetExposure(r.TargetExposure + 0.2f);
            Assert.AreEqual("Image is overexposed.", r.Confirm()); Assert.AreEqual(1, r.Mistakes);
            r.SetExposure(r.TargetExposure - 0.2f); StringAssert.Contains("underexposed", r.Confirm());
            r.SetExposure(r.TargetExposure); r.SetBand((SpectralBand)(((int)r.TargetBand + 1) % 3));
            Assert.AreEqual("Incorrect spectral band.", r.Confirm());
            r.SetBand(r.TargetBand); r.SetFocus(r.TargetFocus > .5f ? 0 : 1); StringAssert.Contains("out of focus", r.Confirm());
            Assert.AreEqual(4, r.Mistakes); Assert.IsFalse(r.Finished);
        }
        [Test] public void CorrectCalibrationReturnsSuccess()   // 10
        {
            var r = New(); r.Tick(12f); Solve(r); r.Confirm();
            var res = r.BuildResult(new MinigameContext { MinigameId = "earth-image-calibration" });
            Assert.AreEqual(MinigameOutcome.Success, res.Outcome); Assert.AreEqual(96, res.Score);   // 100 - 20*12/60
            Assert.AreEqual(1f, res.CompletionPercent); Assert.IsTrue(res.SuggestedEffects.Any(e => e.EffectType == MinigameEffectTypes.ScienceReturnModifier && e.Value > 0));
            Assert.AreEqual(ImageCalibrationRules.EducationalNote, res.EducationalNotes[0]);
        }
        [Test] public void ThreeMistakesThenCorrectIsPartial()
        {
            var r = New(); r.Confirm(); r.Confirm(); r.Confirm(); Solve(r); r.Confirm();
            Assert.AreEqual(MinigameOutcome.PartialSuccess, r.Outcome);
        }
        [Test] public void TimeoutOutcomeDependsOnCorrectSettings()   // 11
        {
            var a = New(); a.SetFocus(a.TargetFocus); a.SetBand(a.TargetBand);
            Assert.IsTrue(a.Tick(61f)); Assert.AreEqual(MinigameOutcome.PartialSuccess, a.Outcome);
            var b = New(); b.SetFocus(b.TargetFocus);
            Assert.IsTrue(b.Tick(61f)); Assert.AreEqual(MinigameOutcome.Failure, b.Outcome);
            var res = b.BuildResult(new MinigameContext()); Assert.IsTrue(res.SuggestedEffects.Any(e => e.EffectType == MinigameEffectTypes.DataQualityModifier && e.Value < -0.25f));
        }
        [Test] public void SameSeedSameTargets()   // 6
        {
            var a = New(77, 2); var b = New(77, 2); var c = New(78, 2);
            Assert.AreEqual(a.TargetFocus, b.TargetFocus); Assert.AreEqual(a.TargetExposure, b.TargetExposure); Assert.AreEqual(a.TargetBand, b.TargetBand);
            Assert.AreEqual(a.Focus, b.Focus); Assert.IsFalse(a.TargetFocus == c.TargetFocus && a.TargetExposure == c.TargetExposure);
        }
        [Test] public void UntimedRunNeverExpires() { var r = new ImageCalibrationRules(new MinigameContext(), 0f); Assert.IsFalse(r.Tick(600f)); Assert.IsFalse(r.Finished); }
    }

    public class MoonPowerRoutingTests
    {
        private static PowerGridRules New(int level = 1, int seed = 2) => new PowerGridRules(new MinigameContext { RandomSeed = seed, DifficultyLevel = level }, 75f);

        [Test] public void BlockedNodesRejectConnections()   // 12
        {
            var r = New(); var b = r.Layout.Blocked[0];
            var res = r.Connect((b.x - 1, b.y), (b.x, b.y));
            Assert.IsFalse(res.Accepted); StringAssert.Contains("Damaged terrain", res.Message); Assert.AreEqual(1, r.Mistakes);
            Assert.IsFalse(r.Connect((0, 0), (2, 0)).Accepted);   // not adjacent
            Assert.AreEqual(0, r.CablesUsed);
        }
        [Test] public void CableLimitIsEnforced()   // 13
        {
            var r = New(); int laid = 0;
            for (int y = 0; y < r.Layout.Height && laid < r.CableLimit + 3; y++)
                for (int x = 0; x + 1 < r.Layout.Width && laid < r.CableLimit + 3; x++)
                {
                    if (r.Node(x, y).Kind == GridNodeKind.Blocked || r.Node(x + 1, y).Kind == GridNodeKind.Blocked) continue;
                    r.Connect((x, y), (x + 1, y)); laid++;
                }
            Assert.AreEqual(r.CableLimit, r.CablesUsed);
            var res = r.Connect((0, 0), (0, 1)); Assert.IsFalse(res.Accepted); StringAssert.Contains("Cable limit", res.Message);
            r.ResetRoute(); Assert.AreEqual(0, r.CablesUsed);
        }
        [Test] public void AllCriticalPoweredReturnsSuccess([Values(1, 2, 3)] int level, [Values(0, 1)] int seed)   // 14
        {
            var r = New(level, seed);
            foreach (var (a, b) in r.Layout.ReferenceSolution) Assert.IsTrue(r.Connect(a, b).Accepted, $"L{level} seed{seed} {a}-{b}");
            Assert.LessOrEqual(r.CablesUsed, r.CableLimit);
            Assert.AreEqual(MinigameOutcome.Success, r.Energize());
            var res = r.BuildResult(new MinigameContext()); Assert.AreEqual(MinigameOutcome.Success, res.Outcome);
            Assert.AreEqual(PowerGridRules.EducationalNote, res.EducationalNotes[0]);
        }
        [Test] public void MissingScienceIsPartialAndMissingThermalIsFailure()
        {
            var r = New();
            foreach (var (a, b) in r.Layout.ReferenceSolution.Take(10)) r.Connect(a, b);   // solar -> comms and thermal only
            Assert.AreEqual(MinigameOutcome.PartialSuccess, r.Evaluate());
            var f = New(); foreach (var (a, b) in f.Layout.ReferenceSolution.Take(5)) f.Connect(a, b);
            Assert.AreEqual(MinigameOutcome.Failure, f.Evaluate());
        }
        [Test] public void CapacityServesCriticalBeforeLighting()
        {
            var r = New(1, 0);   // battery capacity 2: science + lighting would fit; solar capacity 2 serves comms+thermal only
            foreach (var (a, b) in r.Layout.ReferenceSolution) r.Connect(a, b);
            var solar = r.Layout.Special.First(n => n.Id == "solar");
            Assert.AreEqual(2, solar.Capacity);
            var powered = r.Powered(); Assert.IsTrue(powered.Contains("comms") && powered.Contains("thermal") && powered.Contains("science"));
            Assert.IsFalse(powered.Contains("lights"));
        }
        [Test] public void SameSeedSameLayout()
        {
            var a = New(2, 5); var b = New(2, 5); var c = New(2, 4);
            CollectionAssert.AreEqual(a.Layout.Blocked, b.Layout.Blocked); CollectionAssert.AreNotEqual(a.Layout.Blocked, c.Layout.Blocked);
        }
    }

    public class MarsPowerManagementTests
    {
        private static DustStormRules New(int level = 1, int seed = 3) => new DustStormRules(new MinigameContext { RandomSeed = seed, DifficultyLevel = level });
        private static void Run(DustStormRules r, float seconds) { for (float t = 0; t < seconds && !r.Finished; t += 0.1f) r.Tick(0.1f); }

        [Test] public void BatteryDrainsWhenDemandExceedsSupply()   // 15
        {
            var r = New(); Run(r, 9f);   // all systems on (13 kW) as the first dip starts
            float before = r.BatteryPercent; Assert.Greater(r.DemandKw, r.AvailableKw);
            Run(r, 2f); Assert.Less(r.BatteryPercent, before);
        }
        [Test] public void EssentialDowntimeIsTracked()   // 16
        {
            var r = New(); r.SetSystem("comms", false); Run(r, 1.5f);
            Assert.AreEqual(1.5f, r.Find("comms").OffStreak, 0.11f); Assert.AreEqual(1, r.Mistakes); Assert.IsFalse(r.Finished);
            r.SetSystem("comms", true); Assert.AreEqual(0f, r.Find("comms").OffStreak); Assert.AreEqual(1.5f, r.Find("comms").TotalOff, 0.11f);
            r.SetSystem("thermal", false); Run(r, r.EssentialTolerance + 0.5f);
            Assert.IsTrue(r.Finished); Assert.AreEqual(MinigameOutcome.Failure, r.Outcome); StringAssert.Contains("Thermal control", r.FailureReason);
        }
        [Test] public void BatteryReachingZeroReturnsFailure()   // 17
        {
            var r = New(3); Run(r, 45f);   // never shed load
            Assert.IsTrue(r.Finished); Assert.AreEqual(MinigameOutcome.Failure, r.Outcome); Assert.AreEqual(0f, r.BatteryPercent, 0.001f);
            Assert.AreEqual(MinigameOutcome.Failure, r.BuildResult(new MinigameContext()).Outcome);
        }
        [Test] public void SensibleLoadSheddingSucceeds([Values(1, 2, 3)] int level, [Values(1, 2)] int seed)
        {
            var r = New(level, seed);
            r.SetSystem("lights", false); r.SetSystem("heater", false);
            while (!r.Finished)
            {
                // Shed optional loads, keep science while the battery has margin.
                r.SetSystem("science", r.BatteryPercent > 40f);
                r.Tick(0.1f);
            }
            Assert.AreEqual(MinigameOutcome.Success, r.Outcome, $"battery {r.BatteryPercent} science {r.ScienceRatio}");
        }
        [Test] public void DoingNothingFailsAndScienceAlwaysOnIsPartialOnHardStorms()
        {
            var idle = New(1, 1); Run(idle, 45f); Assert.AreEqual(MinigameOutcome.Failure, idle.Outcome);
            var greedy = New(3, 1); greedy.SetSystem("lights", false); greedy.SetSystem("heater", false); Run(greedy, 45f);
            Assert.AreEqual(MinigameOutcome.PartialSuccess, greedy.Outcome);
        }
        [Test] public void SameSeedSameStorm()
        {
            var a = New(2, 9); var b = New(2, 9); var c = New(2, 10);
            for (float t = 0; t <= 45; t += 1.5f) Assert.AreEqual(a.SolarAt(t), b.SolarAt(t));
            Assert.IsTrue(Enumerable.Range(0, 30).Any(i => a.SolarAt(i * 1.5f) != c.SolarAt(i * 1.5f)));
        }
    }

    public class JupiterShieldAllocationTests
    {
        private static ShieldAllocationRules New(int level = 1, int seed = 4) => new ShieldAllocationRules(new MinigameContext { RandomSeed = seed, DifficultyLevel = level }, 60f);
        private static void ProtectAll(ShieldAllocationRules r) { foreach (var z in r.Zones) if (z.RequiredProtection > 0) Assert.IsTrue(r.Place(z.Id, ShieldAllocationRules.Cheapest(z.RequiredProtection)).Accepted, z.Id); }

        [Test] public void MassBudgetIsEnforced()   // 18
        {
            var r = New(3);
            foreach (var z in r.Zones) r.Place(z.Id, ShieldType.Heavy);
            Assert.LessOrEqual(r.MassUsed, r.MassBudget); Assert.Greater(r.Mistakes, 0);
            var full = r.Zones.First(z => z.Shield.HasValue); int before = r.MassUsed;
            Assert.IsTrue(r.Remove(full.Id)); Assert.AreEqual(before - ShieldAllocationRules.MassOf(ShieldType.Heavy), r.MassUsed);   // refund
            var res = r.Place("hull", ShieldType.Light); Assert.IsFalse(res.Accepted);
        }
        [Test] public void CriticalZoneProtectionIsEvaluated()   // 19
        {
            var r = New(); ProtectAll(r);
            var comms = r.Find("comms"); r.Place("comms", ShieldType.Light);
            Assert.AreEqual(comms.Risk == RiskLevel.Low, comms.Protected);
            if (!comms.Protected) Assert.AreEqual(MinigameOutcome.Failure, r.Evaluate());
            r.Remove("computer"); Assert.IsFalse(r.Find("computer").Protected); Assert.AreEqual(MinigameOutcome.Failure, r.Evaluate());
        }
        [Test] public void ValidProtectionReturnsSuccess([Values(1, 2, 3)] int level, [Values(1, 2, 3, 4)] int seed)   // 20
        {
            var r = New(level, seed); ProtectAll(r);
            Assert.LessOrEqual(r.MassUsed, r.MassBudget);
            Assert.AreEqual(MinigameOutcome.Success, r.Confirm());
            Assert.AreEqual(MinigameOutcome.Success, r.BuildResult(new MinigameContext()).Outcome);
        }
        [Test] public void NoncriticalGapIsPartial()
        {
            var r = New(1, 6); ProtectAll(r); r.Remove("power");
            Assert.AreEqual(r.Find("power").RequiredProtection > 0 ? MinigameOutcome.PartialSuccess : MinigameOutcome.Success, r.Evaluate());
        }
        [Test] public void SameSeedSameRadiationPattern()
        {
            var a = New(2, 21); var b = New(2, 21);
            CollectionAssert.AreEqual(a.Zones.Select(z => z.Risk).ToList(), b.Zones.Select(z => z.Risk).ToList()); Assert.AreEqual(a.MassBudget, b.MassBudget);
            Assert.AreEqual(RiskLevel.High, a.Zones.Where(z => z.Critical).Max(z => z.Risk));
        }
    }
}
