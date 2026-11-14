# Far Signal: Earth launch validation (proposed)

The scenario JSON is the team's unmodified design draft. This module is separate from the earlier Earth-observation prototype and uses its own namespace. It does not claim to implement the later chapters or a complete playable mission.

```csharp
using MissionSimulation.FarSignal;
using UnityEngine;

var text = Resources.Load<TextAsset>("FarSignal.Earth.proposed");
var mission = JsonUtility.FromJson<EarthScenario>(text.text);
var design = new EarthDesign {
    launcherId = "l2", nozzleId = "n1", fairingId = "f2", engineId = "e3",
    cargoModuleId = "c2", powerSystemId = "p1", landingPadId = "main",
    cargo = new[] { new CargoPlacement { cargoId = "panel", startSlot = 0 },
                    new CargoPlacement { cargoId = "kit", startSlot = 2 } }
};
LaunchReport report = new EarthLaunchSimulator().Validate(mission, design);
// Show report.issues, report.CanLaunch, costs, margins, fuel and laserPowered.
LaunchResult result = new EarthLaunchSimulator().Evaluate(mission, design);
// Show result.summary and result.report. Only if launch succeeds in the game,
// save result.designSnapshot and result.nextMissionId for the next chapter.
```

Cargo slots are zero-based. Each item occupies a contiguous run starting at `startSlot`, so the panel occupies two adjacent slots. Total payload includes the ship bus, engine, cargo bay, power system and cargo. Effective capacity is launcher payload plus nozzle gain minus cover loss. Fuel and nozzle/fairing effects are the team's abstract gameplay values, not certified specifications. The optional laser is disabled if its load exceeds the selected power system while the essential bus load is still met. A planned landing pad with insufficient fuel is a warning, since the player can change the plan later.

`Evaluate` reports blocked, ready, or ready with warnings, and returns an independent copy of selected items for later use. It is a readiness result: it does not assert the ship has launched or reached orbit. The caller owns persistence and any later flight outcome.

Missing mission or design data produces a blocking issue. The validator rejects missing lists, null entries, duplicate or blank IDs, invalid numeric values and cost overflow; malformed data should be corrected by the scenario owner. Selectors and cargo IDs are exact and case-sensitive. A report that is blocked may contain partial resource totals and should not be treated as a launchable design.

The source draft does not define cargo lock state, cabin/radio checks, exact landing timing, flight duration, flight events, carryover persistence, scores, or a rule for whether all fuel may be spent on landing. Those interactions need agreement with their respective owners; this report is a launch design preview, not a full mission result. No dialogue or visual assets are changed. Values and narrative remain under the scenario owner's review.
