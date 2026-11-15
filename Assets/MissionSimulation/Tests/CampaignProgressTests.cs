using NUnit.Framework;
using MissionSimulation.FarSignal;
using UnityEngine;

namespace MissionSimulation.Tests
{
    public class CampaignProgressTests
    {
        [Test] public void CradleMassAndModuleChangesInvalidateTest()
        {
            var s = new CampaignState { benchPassed = true, seated = true, clamp1 = true, clamp2 = true, planReceived = true };
            Assert.AreEqual(250, CampaignProgress.OrbiterMass("light", "basic"));
            Assert.IsTrue(CampaignProgress.CanTravel(s));
            Assert.IsTrue(CampaignProgress.SelectModules(s, "directional", "reinforced", "standard"));
            Assert.AreEqual(305, CampaignProgress.OrbiterMass(s.antenna, s.shield));
            Assert.IsFalse(CampaignProgress.CanLoad(s));
            Assert.IsFalse(s.benchPassed); Assert.IsFalse(s.clamp1);
        }
        [Test] public void SpareIsSpentOnlyOnce()
        {
            var s = new CampaignState { spares = 1 };
            Assert.IsTrue(CampaignProgress.UseSpareForAntenna(s));
            Assert.IsTrue(CampaignProgress.UseSpareForAntenna(s));
            Assert.AreEqual(0, s.spares);
        }
        [Test] public void ReloadedStateDoesNotSpendSpareTwice()
        {
            var s = new CampaignState { commanderName = "Нара", spares = 1 };
            Assert.IsTrue(CampaignProgress.UseSpareForAntenna(s));
            var restored = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(s));
            Assert.IsTrue(CampaignProgress.UseSpareForAntenna(restored));
            Assert.AreEqual(0, restored.spares); Assert.AreEqual("Нара", restored.commanderName);
        }
        [Test] public void ComparisonRequiresRecordsNotJustFlags()
        {
            var s = new CampaignState { craterPlainSaved = true, groovedRiseSaved = true };
            Assert.IsFalse(CampaignProgress.AcceptComparison(s, "site-b-lower-when-measured"));
        }
        [Test] public void BasicRouteRequiresBothMeasurementsAndPackets()
        {
            var s = new CampaignState { benchPassed = true, seated = true, clamp1 = true, clamp2 = true, planReceived = true };
            Assert.IsFalse(CampaignProgress.AcceptComparison(s, "site-b-lower-when-measured"));
            Assert.IsTrue(CampaignProgress.SaveReading(s, "grooved-rise", 5, "site-b"));
            Assert.IsTrue(CampaignProgress.SaveReading(s, "crater-plain", 8, "site-a"));
            Assert.AreEqual(16, s.surveyRecords[0].simulatedDoseRateUsvPerHour);
            Assert.AreEqual(24, s.surveyRecords[1].simulatedDoseRateUsvPerHour);
            Assert.IsFalse(CampaignProgress.AcceptComparison(s, "site-b-always-safe"));
            Assert.IsTrue(CampaignProgress.AcceptComparison(s, "site-b-lower-when-measured"));
            Assert.IsTrue(CampaignProgress.ReturnToOrbit(s));
            Assert.IsFalse(CampaignProgress.Deploy(s, true, false));
            Assert.IsTrue(CampaignProgress.Deploy(s, true, true));
            Assert.IsTrue(CampaignProgress.Deploy(s, true, true));
            CollectionAssert.AreEqual(new[] { "full-graph" }, CampaignProgress.SendWindow(s, new[] { "full-graph", "health", "sample" }));
            Assert.IsFalse(CampaignProgress.Acknowledge(s));
            CollectionAssert.AreEqual(new[] { "health", "sample" }, CampaignProgress.SendWindow(s, new[] { "health", "sample", "full-graph" }));
            Assert.IsTrue(CampaignProgress.Acknowledge(s));
            Assert.AreEqual(0, CampaignProgress.SendWindow(s, new[] { "health", "sample" }).Count);
        }
    }
}
