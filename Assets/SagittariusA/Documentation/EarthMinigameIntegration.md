# Earth minigame integration handoff

Implement the minigames on your branch using Unity 6.6. The gameplay/UI engineer owns EarthGarageView, EarthGameplaySession and navigation; your runner owns its interaction panel, input, local retry and cleanup. Use FarSignal.Campaign-Team-Prompt.md as the story/design source. Do not invent new part IDs or duplicate the ship formulas.

## Hookup

Implement IEarthMinigameRunner from Assets/SagittariusA/Runtime/Flow/EarthGameplaySession.cs on a MonoBehaviour. Register in Start after the session is initialized:

```csharp
session = FindFirstObjectByType<EarthGameplaySession>();
session.RegisterMinigames(this);
session.MoonStageRequested += HandleMoonArrival;
var resumed = session.GetMoonHandoff();
if (resumed != null) HandleMoonArrival(resumed);
```

Keep the session alive while running the activity. Use an overlay or additive scene; don't unload its owner. Load the Moon scene only after handing its snapshot to the campaign coordinator. On teardown unsubscribe and call session.UnregisterMinigames(this). Keep UI callbacks on Unity's main thread.

Implement:
- Supports(activity): true only for modules actually shipped.
- Begin(context, finished): open that activity with the provided snapshot.
- Pause / Resume: suspend or resume your input/timers.
- Cancel: close the activity and release input. The session invalidates the run before calling this.

Close your overlay and restore focus before invoking finished. Copy context.runId, context.buildRevision and context.activity into every result. completed=false means cancel, never failure of the whole mission. Local mistakes should offer a retry inside the activity.

## Activities and result data

There are three activities, not four. Cargo is pre-launch. Power and landing run after launch on the in-flight screen; together they are the Moon-transit gameplay. The earlier campaign brief placed them before launch and described cabin transit separately, but this current gameplay decision supersedes that ordering.

1. Cargo: show the selected cargo and bay. Return checks.placements containing every loaded item exactly once. Each CargoPlacement has itemId, zero-based firstSlot, locked=true. Slots are contiguous; a six-slot bay is two rows of three. The panel needs two adjacent slots in one row. Prevent overlaps and out-of-range placement. Lock every item, including optional cargo.
2. Power: enabled only after launch. Keep cabin (2 kW) and radio (1 kW) on; test the laser (+3 kW) when carried. Return powerTestCompleted, cabinOn, radioOn, laserTested. laserTested means the player attempted the laser test, including overload/disconnection; it does not mean the laser is usable. The adapter reports inadequate output as a yellow hand-tools tradeoff.
3. Landing: enabled only after launch. Return padId and arrivalMinute in checks. Use session.Catalog.landingMap for IDs, windows and fuel. Shadow or insufficient pad fuel warns during planning; Moon must validate descent and own fuel deductions.

For checks 1–3, unrelated result fields are ignored. Returned check data is validated before it is saved. The runner must display incomplete/rejected results as retryable. Do not write PlayerPrefs, change progression, call TryLaunch or load the next stage yourself.

Context contains commander ID/name, mission ID, part IDs, cargo, previous accepted checks, draft fuel, battery and supply. Resolve display names and specs from the session catalog. Preserve Unicode names and render plain text.

## Current boundaries

The adapter, local PlayerPrefs draft, and run/revision protocol are implemented; this is a first integration contract, not a completed campaign save system. In-progress minigame state is not persisted: cancelling/reloading retries from the last accepted check. Coordinate a checkpoint payload with the simulation engineer before promising mid-activity resume.

No registered runner means buttons stay disabled with MODULE PENDING. Do not ship a fake runner that grants all checks. A test-only runner may be used for integration tests and must be excluded from production.

MoonStageRequested fires automatically when both in-flight activities pass. It receives EarthMoonHandoff: next mission ID, build, checks, fuel, battery, supply, launch cost and mass margin, and commander. Query GetMoonHandoff() on reload too. Resources are pre-descent amounts; the campaign simulation owns later spending and one-time transactions.

## Acceptance handoff

Deliver your prefabs/additive scenes, runner script, dependencies and setup instructions. Demonstrate:
- valid starter passes cargo and launches, then completes power and landing in flight;
- loose/overlapping cargo cannot launch; unfinished power or landing cannot reach the Moon;
- bay/cargo change invalidates locks; power/laser change invalidates power test;
- changing pad/time can recover from a shadow warning;
- cancel returns to the same ship without granting success;
- old run IDs and duplicate callbacks do not advance twice;
- finish both in-flight checks -> Moon directly with the same commander/loadout/landing plan;
- quit during activity -> safe retry; quit after accepted result -> accepted state preserved.

Use keyboard/controller-selectable controls and a button alternative to precise dragging. Don't make optional tasks gate the main route.
