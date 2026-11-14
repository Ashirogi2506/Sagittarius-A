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

Scenario content is JSON. Use the Earth JSON as the schema for future destination configurations; Moon, Mars and Jupiter content still needs design and verification. Values are illustrative gameplay assumptions, not real vehicle specifications. Coordinate values and objectives with the science designer. Cost is in arbitrary budget units; mass in kg, power in W, data rates in MB/day, mission duration in days; fuel, science, protection and cost use abstract gameplay units. Component reliability is an overall probability for the scenario duration, not a daily rate. Mission duration scales generated and transmitted data. Events independently occur according to their configured probabilities and apply multipliers.

The simplified model calculates returned data as the lesser of produced data and downlink capacity over the duration, scaled by reliability. Science is scaled by orbit, resource availability, supporting systems, events and reliability. Missing an objective instrument, an unsuitable destination or orbit prevents objective science; shortages, hazards and events can reduce performance. Objective completion uses the lesser of science and data progress. Score is out of 100 with a reason per category. These are teaching approximations, not a real mission planning tool.

NASA background for the communications and orbit concepts (not a source for this game's numbers or formulas): [NASA Space Communications and Navigation](https://www.nasa.gov/directorates/somd/space-communications-navigation-program/nasa-space-communications-and-navigation-supporting-exploration/) and [NASA Tracking and Data Relay Satellites](https://www.nasa.gov/missions/tdrs/tracking-and-data-relay-satellite-tdrs-generations-of-spacecraft/).

## Integration status

The team repository, Unity project, UI and Unity editor were not present when this module was created. The branch, pull requests, scene integration, tests in Unity, builds, and additional mission configurations therefore remain to be completed in the real team project. Do not replace the team's project or initialize a separate repository inside it without coordinating integration.
