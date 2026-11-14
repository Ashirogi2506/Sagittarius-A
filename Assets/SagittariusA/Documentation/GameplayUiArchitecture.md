# Gameplay architecture — Earth increment

Unity 6.6 / 6000.6.9f1. Source: the Earth section and embedded catalog in FarSignal.Campaign-Team-Prompt.md (14 November 2026). Catalog IDs and values are draft game data.

## Implemented flow

Commander profile -> route -> Khulan briefing -> 3D ship configuration -> launch checklist -> launch authorization -> Earth log -> teammate transit -> Moon arrival handoff.

Continue Expedition resumes the saved 3D configuration, Earth log after launch, or Moon handoff after transit. Launch authorization records a validated manifest; physical launch, orbit and flight simulation are not implemented here. Moon arrival is an integration screen, not a playable lunar scene.

Configuration has six hardware categories (four choices each), six cargo items, live totals, and Balanced / Cargo-focused / Long-range / High-power ready models. Red checks block launch; yellow checks remain visible. Cargo, power and landing checks require a registered minigame runner. Missing modules display MODULE PENDING and cannot grant completion.

EarthAssemblyView is the sole configuration screen. Clicking a part changes the saved ship, 3D model and totals immediately; no separate fitting action is required. INFO buttons open optional catalog descriptions. See EarthAssembly.md for controls, ready models and art integration.

## Ownership

- EarthAssemblyView edits the configuration; EarthGarageView renders the checklist and reports. Neither calculates ship limits.
- EarthGameplaySession coordinates draft state, check invalidation, minigame callbacks and launch gating.
- IEarthSimulationAdapter is the replacement point for the simulation engineer. DraftEarthSimulationAdapter reproduces this brief's rules for local development. It is not flight physics.
- IEarthMinigameRunner is the teammate boundary; see EarthMinigameIntegration.md.
- GetMoonHandoff() returns the saved loadout, landing plan, resources, commander and transit choice. MoonStageRequested fires after successful transit. A scene loader must also query GetMoonHandoff() on resume; events are not replayed.
- The simulation/integration engineer owns the eventual campaign save service, resource spending and cross-chapter transactions. This increment spends no landing fuel or battery.
- Existing generic MissionDefinition / IMissionSimulationService types are earlier scaffolding. Earth currently uses the typed catalog and adapter above; do not implement both for the same screen.

## Draft persistence

Local prototype state uses PlayerPrefs key FarSignal.EarthGarageDraft.v1, separate from the existing profile keys. Save after changes, accepted checks, launch and transit. Load rejects incompatible version, unknown/duplicate IDs and missing structural fields. It is not a production campaign save. IEarthDraftStore is injectable at session initialization for the team's persistence service and isolated tests.

Changing cargo or bay clears cargo placements and locks. Changing power or laser cargo resets the power test. All changes reassess the current landing plan and hardware limits. Switching to a different ready model resets its checks; reselecting the current model preserves them.

A minigame receives a snapshot and a unique run ID plus build revision. Only matching callbacks are accepted. Cancel, stale and duplicate callbacks cannot advance a stage. Only the relevant check fields are copied back. In-progress activity checkpoints are not persisted yet; quitting during an activity retries that activity from the last accepted check.

## Units and formulas

| Quantity | Unit / draft rule |
|---|---|
| Cost | USD: bus + six selected hardware parts + cargo |
| Mass | kg: bus + flight engine + bay + power system + cargo |
| Payload capacity | kg: launcher base + compatible nozzle gain - cover loss |
| Mass margin | capacity - mass; negative blocks, 0–100 kg warns |
| Power | kW: cabin 2 + radio 1; optional laser adds 3 |
| Battery energy | kWh; minimum rated storage 4 |
| Fuel | Fictional units; launch minimum 10, pad-specific descent requirement |
| Landing time | Draft minute 0–10; light window includes opening, excludes closing |

Launcher/nozzle/cover mass is represented by their capacity effects. Do not add it again. kW is power; kWh is stored energy. No real mission prediction is implied.

## Validation

Tests/EarthGarage is a package-free .NET console runner linking the production rule model. Run:
`dotnet run --project Tests/EarthGarage/EarthGarage.Tests.csproj`

It checks the exact catalog totals, red and yellow gates, cargo locks/overlap/adjacency, invalid IDs, mount and cover compatibility, optional laser testing, landing-window boundaries and pad fuel warnings. Unity Play Mode checks cover selecting parts, preset repair, review gating and saved reload.

Far Signal > Run Earth Session Checks exercises the real Unity session with an editor-only fake runner and in-memory save: cancellation, stale/duplicate callbacks, invalidation, launch once, transit once and reload. It never registers on the player's session or changes the player's save.

Full team integration still needs the actual minigame scenes, Moon scene resume and authoritative save transaction tests. Do not mark those integrated until the teammate modules are present.
