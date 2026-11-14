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
// At the actual landing decision, pass the current fuel and pad, which can change in flight:
LandingFuelResult landing = new EarthLaunchSimulator().CheckLandingFuel(mission, "ridge", 12f);
// If landing.restartRequired, the game controller shows failure and restarts the game.
```

Cargo slots are zero-based. Each item occupies a contiguous run starting at `startSlot`, so the panel occupies two adjacent slots. Total payload includes the ship bus, engine, cargo bay, power system and cargo. Effective capacity is launcher payload plus nozzle gain minus cover loss. Fuel and nozzle/fairing effects are the team's abstract gameplay values, not certified specifications. The optional laser is disabled if its load exceeds the selected power system while the essential bus load is still met. A planned landing pad with insufficient fuel is a warning, since the player can change the plan later.

`Evaluate` reports blocked, ready, or ready with warnings, and returns an independent copy of selected items for later use. It is a readiness result: it does not assert the ship has launched or reached orbit. The caller owns persistence and any later flight outcome.

Missing mission or design data produces a blocking issue. The validator rejects missing lists, null entries, duplicate or blank IDs, invalid numeric values and cost overflow; malformed data should be corrected by the scenario owner. Selectors and cargo IDs are exact and case-sensitive. A report that is blocked may contain partial resource totals and should not be treated as a launchable design.

The user confirmed that insufficient fuel for the landing pad causes mission failure and a restart, and that a manual opening option is needed when the laser cannot be used. `CheckLandingFuel` signals failure to the game controller without deciding the timing or resetting state itself. Fuel sufficiency alone does not guarantee a successful landing. For the manual route, UI/gameplay must offer the opening interaction; `laserPowered` reports whether the laser is usable during the launch configuration, but the gameplay owner must determine its state later in the mission.

The source draft does not define cargo lock state, cabin/radio checks, exact landing timing, flight fuel consumption, flight events, carryover persistence, scores, or the manual opening interaction. Those interactions need agreement with their respective owners; this report is a launch design preview, not a full mission result. No dialogue or visual assets are changed. Values and narrative remain under the scenario owner's review.
