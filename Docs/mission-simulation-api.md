# Mission simulation integration

This branch contains a reusable Earth-observation prototype and a separate draft campaign rules layer. Values in the JSON and campaign rules are fictional gameplay assumptions, not NASA specifications. The branch is a module rather than a complete Unity project; integrate it into the team's existing project, preserving its assembly definitions and resolving any duplicate files with the gameplay owner.

## Earth-observation prototype

Use the existing `MissionSimulation` namespace. The public facade loads `Resources/earth_observation.json` and exposes the engine without requiring a scene:

```csharp
var service = new MissionSimulationService();
var mission = service.LoadMission("earth-observation");
var design = new PlayerDesign { orbitId = "leo", launcherId = "large",
    componentIds = new[] { "bus", "solar", "radio", "fuel", "shield", "camera" } };
ResourceBudget totals = service.CalculateResources(mission, design);
var issues = service.ValidateDesign(mission, design);
SimulationResult result = service.RunSimulation(mission, design, 42);
```

Call `ValidateDesign` when a selection changes. Display each issue's severity, message and suggested fix; show `summary`, objective IDs, score entries, events and explanations from the result. `Validate` and `Run` on `MissionSimulator` remain available for existing consumers. The same input and seed are deterministic within the same runtime.

This older prototype is distinct from the newer campaign's crewed Earth launch catalog. Its rates and fuel are abstract units. Component mass already includes its loaded propellant; adding the abstract fuel count again as kilograms would double-count it. The bus provides draft onboard storage; a daily surplus larger than that storage produces a warning. Data returned remains the simplified aggregate of production and downlink over the duration, rather than a detailed buffer simulation. Launcher and component reliability are multiplied; for legacy data, an omitted launcher reliability is treated as no modifier. Instrument orbit restrictions, objective orbit and destination are checked separately. Reliability values represent the configured whole mission duration, not independent daily probabilities. All such values are provisional game design.

## Campaign Earth launch

Load `Resources/ FarSignal.Earth.proposed.json` without the space: `Resources.Load<TextAsset>("FarSignal.Earth.proposed")`; deserialize it with `JsonUtility.FromJson<EarthScenario>(asset.text)`. Fill an `EarthDesign`, then use `EarthLaunchSimulator.Validate` for a live report. Use `CargoPackingMinigame` to pack and lock items, `EarthPowerTest` for the cabin/radio and optional laser, and `EarthLaunchChecklist.Validate` with a `LandingPlan` for the final launch checks. Read `LaunchReport.issues` (code, explanation, suggestedFix and level) and its totals and margins. The starter configuration costs $93M and carries 9,570 kg against 10,100 kg. All numbers are draft game values.

`LunarDescent.Preview` checks fuel and daylight. `Confirm` returns a new state containing the operation ID and the fuel deduction; persist that entire state atomically before advancing. Repeating the same operation on the persisted state does not deduct again. The existing gameplay sends the player back to the pre-launch checkpoint on insufficient fuel. This matches the existing simulator and gameplay contract, but conflicts with the newer campaign brief's pad-selection retry; coordinate the change with the gameplay owner before changing that behavior. The caller owns durable storage, recovery, concurrency control and state transitions.

## Mars and Ganymede campaign draft

`CampaignProgress.CreateFromCheckpoint` verifies and copies the existing Earth checkpoint, commander and cargo; `CommitLunarDescent` uses the existing descent operation and fuel record. Pole-7 route, supply delivery, relay, optional beacon battery, Mars approach and return-to-ship gates are represented by the corresponding `CampaignProgress` methods; delivery removes the panel but retains the kit. `CampaignProgress.OrbiterMass` uses the draft 300 kg Mars cradle. Module or connector changes invalidate the bench test and clamps; `TryBenchTest`, `SeatOrbiter`, `SetClamp`, `ReceiveDeploymentPlan`, `CanLoad`, and `CanTravel` expose these gates. A missing data connection cannot pass a bench test. `UseSpareForAntenna` is idempotent on a restored state; manual recovery does not spend a spare. Mars transit packet retries use `SendMarsTransitWindow` and `MarsArrivalConfirmed`. `LandOnGanymede` and `SaveReading` record the two site IDs, their fictional dose rates, timestamp and image ID. `AcceptComparison`, `ReturnToOrbit`, `Deploy`, `HoldAntennaAlignment`, `SendWindow` and `Acknowledge` cover the basic finale gates and packet retries. The light antenna sends 10 seconds per window; the aligned directional antenna sends 14. Data packet IDs are `health`, `sample`, and `full-graph`.

Use `CampaignSaveStore.ForSlot(slotId)` (or its constructor with an absolute path) for a new save, call `Create(initialState)` once, and `Load()` to resume. `Apply(operationId, actionKey, mutation)` reloads under an exclusive lock, writes the complete state and operation record to a flushed temporary file, and atomically replaces the save before returning `Applied`. Use a stable operation ID for each logical action and an action key that includes all relevant parameters; retries with the same ID and key return `AlreadyApplied`, while ID reuse with another key returns `OperationConflict`. Rejected operations do not change disk. Corrupt or unsupported saves throw instead of being replaced; handle IO errors and lock contention in the caller. File system and platform guarantees still apply; no cloud sync or cross-device conflict resolution is provided.

```csharp
var store = CampaignSaveStore.ForSlot("slot-1");
// On a new run only: store.Create(CampaignProgress.CreateFromCheckpoint(scenario, checkpoint));
var result = store.Apply("run-1-descent", "descent:main:3",
    state => CampaignProgress.CommitLunarDescent(state, scenario, "main", 3, "run-1-descent"));
// Advance only on Applied or AlreadyApplied; retain IDs across retries and reloads.
```

The existing gameplay branch has its own draft save/controller and is not wired to this store yet. UI/gameplay must call these methods from actual events and pass confirmed connectors, locks, target alignment and timestamps. Public serialized flags are not an authoritative anti-tamper boundary. Scene binding, migrating old saves, remaining chapter transitions, the final report integration and a full campaign playthrough remain unfinished.

### Local gateway draft for gameplay integration

`CampaignDraftGateway` wraps the store in typed operations for the Pole-7, Mars and Ganymede milestones. It is an integration draft, not a scene component: it does not subscribe to gameplay events, advance scenes, display dialogue, or automatically create or migrate a player's save. The gameplay owner must decide where it connects to the existing controller and save format.

```csharp
var campaign = CampaignDraftGateway.ForSlot("slot-1");
// New run only, after the Earth checkpoint has been verified:
campaign.CreateFromEarthCheckpoint(scenario, checkpoint);
// For an existing slot use campaign.Load(); do not call Create again.
var result = campaign.ConfirmDescent("run-1-descent", scenario, "main", 3f);
// Advance only if result.status is Applied or AlreadyApplied.
```

Keep one unique operation ID per logical action and reuse the same ID and inputs when retrying after an interruption. Use a new ID for a new action; reusing an ID with different action parameters returns `OperationConflict`. For descent, the operation key contains the pad and arrival time, not the scenario object: keep the same verified scenario for a run. A `Rejected` action was not saved and must not advance the UI; handle IO and lock errors before acknowledging an action. Use separate IDs for successive transmission windows and inspect the returned snapshot for the actual received packets.

The gameplay controller must call `SetBenchConnections` whenever either cable actually changes, even if a subsequent bench test fails. Connector or module changes invalidate the prior test; call `CompleteBenchTest` after the physical test succeeds. Similarly, pass actual clamp states, release-point and crew readiness, reading times and images. `AlignDirectionalAntenna` expects the controller to verify a continuous in-target hold of at least one second before calling it; the gateway does not measure that hold. The gateway keeps the current landing-fuel behavior unchanged while the team lead decides the conflicting requirement.

For resume guidance, call `CampaignResumeGuide.GetNextAction(campaign.Load().state)`. Its `action` is an advisory `CampaignNextAction`, and `remainingIds` identifies missing required site or packet IDs where applicable. Both survey sites may be visited in either order; optional packets are not required for completion. The guide does not modify the save, validate its consistency, replace the actual interaction gates, choose a scene or display UI. It starts from the saved Earth checkpoint/descent handoff, not the Earth build menus. Handle invalid or incompatible saves separately.

`Docs/IntegrationExamples` includes reviewed-but-not-wired examples for the gameplay branch's completed Earth snapshot. Read their README before integrating: team-owned DTOs and the existing Moon controller impose sequencing, missing-input, and transaction-ownership constraints.

Four synthetic save envelopes are available under `Assets/MissionSimulation/Tests/Fixtures`: `basic` starts with the basic build, `optional` represents a used spare and directional antenna, `blocked` exceeds the cradle by 5 kg, and `interrupted` has delivered only the health packet. These isolate rule tests and do not include a real Earth checkpoint or replace the gameplay team's save format. Copy a fixture to a disposable slot, never over a real player save.

## Testing

The tests under `Assets/MissionSimulation/Tests` target Unity Edit Mode. Standalone .NET runs with a Unity JSON/resources shim are useful for checking the pure rules, but do not prove Unity import, compilation, persistence integration or playability. Run the Unity Test Runner in the team's project once this branch is integrated. Never overwrite the existing project or scenes to do so.
