# Mission simulation module

Unity C# module for the Earth-observation vertical slice. Copy `Assets/MissionSimulation` into the team's Unity project's `Assets` folder. This folder is not a complete Unity project. The simulation has no scene, UI, or model dependencies; the JSON loader uses Unity's `JsonUtility`. Install/enable the Unity Test Framework and run the Edit Mode tests in Test Runner after importing.

## UI integration

```csharp
using MissionSimulation;
using UnityEngine;

TextAsset asset = Resources.Load<TextAsset>("earth_observation");
MissionScenario scenario = MissionScenarioLoader.FromJson(asset.text);
IMissionSimulator simulator = new MissionSimulator();
var design = new PlayerDesign {
    orbitId = "leo", launcherId = "large",
    componentIds = new[] { "bus", "solar", "radio", "fuel", "shield", "camera" }
};
ValidationResult preview = simulator.Validate(scenario, design);
// Display preview.resources, preview.issues and preview.CanLaunch.
SimulationResult result = simulator.Run(scenario, design, seed: 42);
// Display result.outcome, validation issues, events, explanations,
// dataReturnedMB, scienceDelivered and score.entries / score.Total.
```

Call `Validate` when a selection changes and `Run` on launch. `Run` revalidates; blocked designs return a blocked outcome. The same configuration and seed produce the same result in the same runtime. Do not rely on identical `System.Random` sequences across different .NET implementations or runtime versions. Component IDs must be unique in a design; missing or duplicate IDs are blocking errors. A launcher exceeding its capacity, an incompatible launcher/orbit or budget overrun also blocks launch. Other issues are warnings, with consequences explained in the result.

## Model and assumptions

Scenario content is JSON. Use the Earth JSON as the schema for future destination configurations; Moon, Mars and a Jupiter moon mission still need design and verification. The Jupiter moon has not yet been selected. Values are illustrative gameplay assumptions, not real vehicle specifications. Coordinate values and objectives with the science designer. Cost is in arbitrary budget units; mass in kg, power in W, data rates in MB/day, mission duration in days; fuel, science, protection and cost use abstract gameplay units. Component reliability is an overall probability for the scenario duration, not a daily rate. Mission duration scales generated and transmitted data. Events independently occur according to their configured probabilities and apply multipliers. Required system tags, scoring weights and shortage penalties are configured per scenario in `requiredSystemTags` and `rules`.

The simplified model calculates returned data as the lesser of produced data and downlink capacity over the duration, scaled by reliability and downlink events. Science comes only from instruments tagged for the objective and suitable for the destination, then is scaled by orbit, resource availability, supporting systems, events and reliability. Missing an objective instrument or using an unsuitable orbit prevents objective science; shortages, hazards and events can reduce performance. Objective completion uses the lesser of science and data progress. The Earth score is out of 100 with a reason per category. These are teaching approximations, not a real mission planning tool.

NASA background for the communications and orbit concepts (not a source for this game's numbers or formulas): [NASA Space Communications and Navigation](https://www.nasa.gov/directorates/somd/space-communications-navigation-program/nasa-space-communications-and-navigation-supporting-exploration/) and [NASA Tracking and Data Relay Satellites](https://www.nasa.gov/missions/tdrs/tracking-and-data-relay-satellite-tdrs-generations-of-spacecraft/).

## Integration status

The module is on the `feature/mission-simulation` branch in a draft pull request. A complete Unity project and UI were not yet available. Scene integration, tests in Unity, builds, and additional mission configurations remain to be completed in the team project. The C# simulation core compiled locally, but the local operating system blocked execution of the standalone test program; this is not a passed test run.
