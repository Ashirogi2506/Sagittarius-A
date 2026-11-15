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
        [Test] public void TotalScoreStaysWithinBoundsEvenForBadWeights()
        {
            var score = new ScoreBreakdown();
            score.entries.Add(new ScoreEntry("test", 120, 120, "test"));
            Assert.AreEqual(100f, score.Total);
            score.entries[0].points = -10;
            Assert.AreEqual(0f, score.Total);
        }
        [Test] public void OverBudgetAndMissingPowerAreReportedWithFixes()
        {
            scenario.budget = 50;
            var design = Design();
            design.componentIds = new[] { "bus", "radio", "fuel", "shield", "camera" };
            var result = simulator.Validate(scenario, design);
            Assert.IsTrue(result.issues.Any(x => x.code == "budget" && x.severity == Severity.Blocking));
            Assert.IsTrue(result.issues.Any(x => x.code == "power"));
            Assert.IsTrue(result.issues.Any(x => x.code == "support"));
            Assert.IsTrue(result.issues.All(x => !string.IsNullOrEmpty(x.suggestedFix)));
        }
        [Test] public void IncompatibleInstrumentOrbitIsReported()
        {
            var design = Design(); design.orbitId = "high";
            var result = simulator.Run(scenario, design, 42);
            Assert.IsTrue(result.validation.issues.Any(x => x.code == "instrument_orbit"));
            Assert.AreEqual(0f, result.scienceDelivered);
        }
        [Test] public void StorageAndLauncherReliabilityAppearInResourceTotals()
        {
            var result = simulator.Validate(scenario, Design());
            Assert.AreEqual(1000f, result.resources.storageCapacityMB);
            Assert.Less(result.resources.reliability, 0.99f);
            var camera = scenario.components.First(x => x.id == "camera");
            camera.dataMBPerDay = 3000;
            Assert.IsTrue(simulator.Validate(scenario, Design()).issues.Any(x => x.code == "storage"));
        }
        [Test] public void UiFacadeLoadsCalculatesAndReturnsCompleteResult()
        {
            var service = new MissionSimulationService();
            var loaded = service.LoadMission("earth-observation"); loaded.events = new MissionEvent[0];
            var design = Design();
            Assert.AreEqual(245f, service.CalculateResources(loaded, design).massKg);
            Assert.IsFalse(service.ValidateDesign(loaded, design).Any(x => x.severity == Severity.Blocking));
            var result = service.RunSimulation(loaded, design, 42);
            Assert.IsNotEmpty(result.summary);
            CollectionAssert.Contains(result.completedObjectives, "observe");
            Assert.Throws<System.ArgumentException>(() => service.LoadMission("not-a-mission"));
        }
        [Test] public void InvalidNumericValuesAndDuplicateComponentIdsBlockSimulation()
        {
            scenario.components[0].massKg = float.NaN;
            Assert.IsFalse(simulator.Validate(scenario, Design()).CanLaunch);
            scenario.components[0].massKg = 100;
            scenario.components[1].id = scenario.components[0].id;
            Assert.IsFalse(simulator.Validate(scenario, Design()).CanLaunch);
        }
    }
}
