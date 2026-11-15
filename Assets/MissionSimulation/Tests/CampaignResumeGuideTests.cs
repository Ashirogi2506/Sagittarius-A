using System.Collections.Generic;
using MissionSimulation.FarSignal;
using NUnit.Framework;
using UnityEngine;

namespace MissionSimulation.Tests
{
    public class CampaignResumeGuideTests
    {
        private static CampaignState AtMars()
        {
            return new CampaignState {
                lunarDescent = new LunarDescentState { committed = true }, routeChecked = true,
                suppliesDelivered = true, pole7RelayRestored = true, routeUploaded = true,
                marsApproachReceived = true, returnedToShip = true, antennaRecovered = true,
                marsTransitPackets = new List<string> { "arrival-update" }
            };
        }

        [Test] public void ResumeGuidesEarthAndMarsWithoutChangingState()
        {
            var s = new CampaignState();
            Assert.AreEqual(CampaignNextAction.RestoreEarthCheckpoint, CampaignResumeGuide.GetNextAction(s).action);
            s.lunarDescent = new LunarDescentState();
            Assert.AreEqual(CampaignNextAction.ConfirmLunarDescent, CampaignResumeGuide.GetNextAction(s).action);
            s.lunarDescent.committed = true;
            Assert.AreEqual(CampaignNextAction.CheckLunarRoute, CampaignResumeGuide.GetNextAction(s).action);

            s = AtMars();
            s.antenna = "directional"; s.shield = "reinforced";
            Assert.AreEqual(CampaignNextAction.ChooseOrbiterModules, CampaignResumeGuide.GetNextAction(s).action);
            s.antenna = "light"; s.shield = "basic";
            Assert.AreEqual(CampaignNextAction.ConnectBenchCables, CampaignResumeGuide.GetNextAction(s).action);
            s.powerCableConnected = s.dataCableConnected = true;
            Assert.AreEqual(CampaignNextAction.CompleteBenchTest, CampaignResumeGuide.GetNextAction(s).action);
            s.benchPassed = true;
            Assert.AreEqual(CampaignNextAction.SeatOrbiter, CampaignResumeGuide.GetNextAction(s).action);
            s.seated = true; s.clamp1 = true;
            Assert.AreEqual(CampaignNextAction.SecureClamps, CampaignResumeGuide.GetNextAction(s).action);
            s.clamp2 = true;
            Assert.AreEqual(CampaignNextAction.ReceiveDeploymentPlan, CampaignResumeGuide.GetNextAction(s).action);
            s.planReceived = true;
            string before = JsonUtility.ToJson(s);
            Assert.AreEqual(CampaignNextAction.LandOnGanymede, CampaignResumeGuide.GetNextAction(s).action);
            Assert.AreEqual(before, JsonUtility.ToJson(s));
        }

        [Test] public void ReversedSiteOrderAndPartialPacketsResumeFromSavedRecords()
        {
            var s = AtMars();
            s.powerCableConnected = s.dataCableConnected = s.benchPassed = s.seated = s.clamp1 = s.clamp2 = s.planReceived = true;
            s.ganymedeLanded = true;
            s.surveyRecords.Add(new SurveyRecord { siteId = "grooved-rise", imageId = "b", missionTimestampSeconds = 5 });
            // Flags alone are not a substitute for the saved site record.
            s.craterPlainSaved = true;
            var reloaded = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(s));
            var advice = CampaignResumeGuide.GetNextAction(reloaded);
            Assert.AreEqual(CampaignNextAction.RecordSurfaceSites, advice.action);
            CollectionAssert.AreEqual(new[] { "crater-plain" }, advice.remainingIds);
            reloaded.surveyRecords.Add(new SurveyRecord { siteId = "crater-plain", imageId = "a", missionTimestampSeconds = 8 });
            Assert.AreEqual(CampaignNextAction.CompareReadings, CampaignResumeGuide.GetNextAction(reloaded).action);
            reloaded.comparisonAccepted = reloaded.returnedToOrbit = reloaded.deployed = true;
            reloaded.antenna = "directional";
            Assert.AreEqual(CampaignNextAction.AlignAntenna, CampaignResumeGuide.GetNextAction(reloaded).action);
            reloaded.antennaAligned = true;
            reloaded.receivedPackets.Add("health");
            reloaded = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(reloaded));
            advice = CampaignResumeGuide.GetNextAction(reloaded);
            Assert.AreEqual(CampaignNextAction.SendRequiredPackets, advice.action);
            CollectionAssert.AreEqual(new[] { "sample" }, advice.remainingIds);
            reloaded.receivedPackets.Add("sample");
            Assert.AreEqual(CampaignNextAction.ReceiveEarthAcknowledgement, CampaignResumeGuide.GetNextAction(reloaded).action);
            reloaded.earthAcknowledged = true;
            Assert.AreEqual(CampaignNextAction.CampaignComplete, CampaignResumeGuide.GetNextAction(reloaded).action);
        }
    }
}
