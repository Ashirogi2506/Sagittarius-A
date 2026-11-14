using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MissionSimulation.Tests
{
    public class MissionSimulatorTests
    {
        MissionScenario scenario;
        MissionSimulator simulator;
        [SetUp] public void Setup()
        {
            scenario = MissionScenarioLoader.FromJson(Resources.Load<TextAsset>("earth_observation").text);
            scenario.events = new MissionEvent[0];
            simulator = new MissionSimulator();
        }
        PlayerDesign Design(string radio = "radio", string launcher = "large")
        { return new PlayerDesign { orbitId = "leo", launcherId = launcher, componentIds = new[] { "bus", "solar", radio, "fuel", "shield", "camera" } }; }
        [Test] public void ValidDesignSucceedsAndTotalsAreCorrect()
        {
            var result = simulator.Run(scenario, Design(), 42);
            Assert.AreEqual(MissionOutcome.Success, result.outcome);
            Assert.AreEqual(245f, result.validation.resources.massKg);
            Assert.AreEqual(138f, result.validation.resources.cost);
            Assert.AreEqual(6, result.score.entries.Count);
        }
        [Test] public void LowDownlinkProducesPartialSuccess()
        {
            var result = simulator.Run(scenario, Design("small-radio"), 42);
            Assert.AreEqual(MissionOutcome.PartialSuccess, result.outcome);
            Assert.IsTrue(result.validation.issues.Any(x => x.code == "data"));
        }
        [Test] public void ExcessMassBlocksLaunch()
        { Assert.AreEqual(MissionOutcome.Blocked, simulator.Run(scenario, Design("radio", "small"), 42).outcome); }
        [Test] public void NoInstrumentFails()
        {
            var d = Design(); d.componentIds = new[] { "bus", "solar", "radio", "fuel", "shield" };
            Assert.AreEqual(MissionOutcome.Failure, simulator.Run(scenario, d, 42).outcome);
        }
        [Test] public void SameSeedRepeatsEventsAndScore()
        {
            scenario.events = new[] { new MissionEvent { description = "Test event", probability = 0.5f, scienceMultiplier = 0.8f, dataMultiplier = 0.8f, reliabilityMultiplier = 0.8f } };
            var a = simulator.Run(scenario, Design(), 11); var b = simulator.Run(scenario, Design(), 11);
            Assert.AreEqual(a.score.Total, b.score.Total); CollectionAssert.AreEqual(a.events, b.events);
        }
        [Test] public void DuplicateComponentsAreBlocked()
        {
            var d = Design(); d.componentIds = new[] { "camera", "camera" };
            Assert.IsFalse(simulator.Validate(scenario, d).CanLaunch);
        }
        [Test] public void GroundOutageReducesReturnWhenDownlinkIsSaturated()
        {
            var d = Design("small-radio");
            var baseline = simulator.Run(scenario, d, 42);
            scenario.events = new[] { new MissionEvent { description = "Outage", probability = 1f, scienceMultiplier = 1f, dataMultiplier = 0.5f, reliabilityMultiplier = 1f } };
            var affected = simulator.Run(scenario, d, 42);
            Assert.AreEqual(baseline.dataReturnedMB * 0.5f, affected.dataReturnedMB, 0.01f);
        }
        [Test] public void InvalidOutcomeThresholdsBlockLaunch()
        {
            scenario.partialThreshold = 0;
            Assert.IsFalse(simulator.Validate(scenario, Design()).CanLaunch);
        }
        [Test] public void UnsuitableExtraInstrumentDoesNotCancelSuitableCamera()
        {
            var extra = scenario.components.First(x => x.id == "thermal");
            extra.destinations = new[] { "Mars" };
            var d = Design(); d.componentIds = new[] { "bus", "solar", "radio", "fuel", "shield", "camera", "thermal" };
            var result = simulator.Run(scenario, d, 42);
            Assert.IsTrue(result.validation.issues.Any(x => x.code == "destination"));
            Assert.Greater(result.scienceDelivered, 0f);
            Assert.AreEqual(80f, result.validation.resources.science);
        }
        [Test] public void ScoreWeightsComeFromScenario()
        {
            scenario.rules.objectivePoints = 12f;
            var result = simulator.Run(scenario, Design(), 42);
            Assert.AreEqual(12f, result.score.entries.First(x => x.category == "Objective completion").maximum);
        }
    }
}
