# Mission simulation integration

This branch contains a reusable Earth-observation prototype and a separate draft campaign rules layer. Values in the JSON and campaign rules are fictional gameplay assumptions, not NASA specifications. The branch is a module rather than a complete Unity project; integrate it into the team's existing project, preserving its assembly definitions and resolving any duplicate files with the gameplay owner.

## Earth-observation prototype

Use the existing `MissionSimulation` namespace. Load `Resources/earth_observation.json` via Unity's `Resources.Load<TextAsset>("earth_observation")`, then call `MissionScenarioLoader.FromJson(asset.text)`. Construct a `PlayerDesign` with orbit, launcher and component IDs. Call `MissionSimulator.Validate(scenario, design)` for `resources`, `issues` and `CanLaunch`; call `Run(scenario, design, seed)` to obtain the outcome, score entries, event descriptions and explanations. The same input and seed are deterministic within the same runtime. Display each warning/blocking issue and each score entry's points, maximum and reason.

This older prototype is distinct from the newer campaign's crewed Earth launch catalog. Its rates and fuel are abstract units. It does not yet implement every requested field (notably storage, separate orbit compatibility per instrument, launcher reliability and suggested fixes); do not present it as the completed final simulator.

## Campaign Earth launch

Load `Resources/ FarSignal.Earth.proposed.json` without the space: `Resources.Load<TextAsset>("FarSignal.Earth.proposed")`; deserialize it with `JsonUtility.FromJson<EarthScenario>(asset.text)`. Fill an `EarthDesign`, then use `EarthLaunchSimulator.Validate` for a live report. Use `CargoPackingMinigame` to pack and lock items, `EarthPowerTest` for the cabin/radio and optional laser, and `EarthLaunchChecklist.Validate` with a `LandingPlan` for the final launch checks. Read `LaunchReport.issues` (code, explanation, suggestedFix and level) and its totals and margins. The starter configuration costs $93M and carries 9,570 kg against 10,100 kg. All numbers are draft game values.

`LunarDescent.Preview` checks fuel and daylight. `Confirm` returns a new state containing the operation ID and the fuel deduction; persist that entire state atomically before advancing. Repeating the same operation on the persisted state does not deduct again. The existing gameplay sends the player back to the pre-launch checkpoint on insufficient fuel. This matches the existing simulator and gameplay contract, but conflicts with the newer campaign brief's pad-selection retry; coordinate the change with the gameplay owner before changing that behavior. The caller owns durable storage, recovery, concurrency control and state transitions.

## Mars and Ganymede campaign draft

`CampaignState` is serializable with `JsonUtility`. `CampaignProgress.OrbiterMass` uses the draft 300 kg Mars cradle, and `SelectModules` invalidates the bench test and clamps when a module changes. `CanLoad` and `CanTravel` expose the currently implemented gates. `UseSpareForAntenna` is idempotent on a restored state; manual recovery does not spend a spare. `SaveReading` records the two site IDs, their fictional dose rates, timestamp and image ID. `AcceptComparison`, `ReturnToOrbit`, `Deploy`, `SendWindow` and `Acknowledge` cover the basic finale gates and packet retries. The light antenna sends 10 seconds per window; the aligned directional antenna sends 14. Data packet IDs are `health`, `sample`, and `full-graph`.

Persist the **whole** state and operation outcome atomically after each successful action, before acknowledging completion in UI. These methods alone do not provide file I/O, transactional persistence, protection from simultaneous callers, scene binding, timestamp generation, validated bench test interactions or durable state migration. The UI/gameplay integration must validate and set the bench, clamp, antenna alignment and plan flags from real gameplay events; public flags are not an authoritative anti-tamper boundary. The complete campaign, every requested mission transition, test saves, final report integration and full end-to-end Unity playthrough remain unfinished.

## Testing

The tests under `Assets/MissionSimulation/Tests` target Unity Edit Mode. Standalone .NET runs with a Unity JSON/resources shim are useful for checking the pure rules, but do not prove Unity import, compilation, persistence integration or playability. Run the Unity Test Runner in the team's project once this branch is integrated. Never overwrite the existing project or scenes to do so.
