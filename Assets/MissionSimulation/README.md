# Mission simulation module

Unity C# module for the Earth-observation vertical slice and draft campaign rules. This handoff branch contains the module at its existing `Assets/MissionSimulation` path within the team's Unity project. Do not copy it into another location or replace it wholesale; preserve its `.meta` files and scene references. The simulation has no scene, UI, or model dependencies; the JSON loader uses Unity's `JsonUtility`.

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

The separate Earth-observation prototype uses JSON; future generic simulator scenarios need design and verification. The campaign rules cover the Moon, Mars and Ganymede through a different API. Values are illustrative gameplay assumptions, not real vehicle specifications. Coordinate values and objectives with the science designer. Cost is in arbitrary budget units; mass in kg, power in W, data rates in MB/day, mission duration in days; fuel, science, protection and cost use abstract gameplay units. Component reliability is an overall probability for the scenario duration, not a daily rate. Mission duration scales generated and transmitted data. Events independently occur according to their configured probabilities and apply multipliers. Required system tags, scoring weights and shortage penalties are configured per scenario in `requiredSystemTags` and `rules`.

The simplified model calculates returned data as the lesser of produced data and downlink capacity over the duration, scaled by reliability and downlink events. Science comes only from instruments tagged for the objective and suitable for the destination, then is scaled by orbit, resource availability, supporting systems, events and reliability. Missing an objective instrument or using an unsuitable orbit prevents objective science; shortages, hazards and events can reduce performance. Objective completion uses the lesser of science and data progress. The Earth score is out of 100 with a reason per category. These are teaching approximations, not a real mission planning tool.

NASA background for the communications and orbit concepts (not a source for this game's numbers or formulas): [NASA Space Communications and Navigation](https://www.nasa.gov/directorates/somd/space-communications-navigation-program/nasa-space-communications-and-navigation-supporting-exploration/) and [NASA Tracking and Data Relay Satellites](https://www.nasa.gov/missions/tdrs/tracking-and-data-relay-satellite-tdrs-generations-of-spacecraft/).

## Integration status

The campaign Earth launch rules and the original Earth-observation simulator are separate models with different inputs and units; neither should be copied or called twice for the same calculation. The API and remaining limitations are documented in `Docs/mission-simulation-api.md`. The persistent campaign store exists but is not yet wired to the team's gameplay save. Scene integration and a complete end-to-end campaign playthrough remain unfinished.
