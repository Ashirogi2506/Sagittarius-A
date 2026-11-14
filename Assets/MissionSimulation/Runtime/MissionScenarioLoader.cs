using System;
using UnityEngine;

namespace MissionSimulation
{
    public static class MissionScenarioLoader
    {
        // Pass a JSON TextAsset or load Resources/earth_observation with Resources.Load<TextAsset>.
        public static MissionScenario FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Scenario JSON is empty.", "json");
            var scenario = JsonUtility.FromJson<MissionScenario>(json);
            if (scenario == null) throw new ArgumentException("Scenario JSON could not be parsed.", "json");
            return scenario;
        }
    }
}
