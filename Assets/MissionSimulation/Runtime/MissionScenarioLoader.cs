using System;
using UnityEngine;

namespace MissionSimulation
{
    public static class MissionScenarioLoader
    {
        public static MissionScenario LoadMission(string missionId)
        {
            if (missionId != "earth-observation") throw new ArgumentException("Unknown mission ID.", nameof(missionId));
            var asset = Resources.Load<TextAsset>("earth_observation");
            if (asset == null) throw new InvalidOperationException("Earth mission data is missing from Resources.");
            var scenario = FromJson(asset.text);
            if (scenario.id != missionId) throw new InvalidOperationException("Mission ID does not match the requested data.");
            return scenario;
        }
        // Pass a JSON TextAsset or load Resources/earth_observation with Resources.Load<TextAsset>.
        public static MissionScenario FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Scenario JSON is empty.", "json");
            var scenario = JsonUtility.FromJson<MissionScenario>(json);
            if (scenario == null) throw new ArgumentException("Scenario JSON could not be parsed.", "json");
            return scenario;
        }
    }

    // Thin public facade for UI integrations. The engine still owns all calculations.
    public sealed class MissionSimulationService
    {
        private readonly MissionSimulator simulator = new MissionSimulator();
        public MissionScenario LoadMission(string missionId) => MissionScenarioLoader.LoadMission(missionId);
        public ResourceBudget CalculateResources(MissionScenario mission, PlayerDesign design) => simulator.Validate(mission, design).resources;
        public System.Collections.Generic.List<ValidationIssue> ValidateDesign(MissionScenario mission, PlayerDesign design) => simulator.Validate(mission, design).issues;
        public SimulationResult RunSimulation(MissionScenario mission, PlayerDesign design, int seed) => simulator.Run(mission, design, seed);
    }
}
