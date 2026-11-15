# FARSIGNAL minigame integration

This document covers the short 2D mission tasks that open during mission events:

| ID | Mission | Task | Default time |
|---|---|---|---|
| `earth-image-calibration` | Earth observation | Satellite Image Calibration | 60 s |
| `moon-power-routing` | Moon exploration | Lunar Power Grid Routing | 100 s |
| `mars-power-management` | Mars mission | Dust Storm Power Management | 45 s storm |
| `jupiter-shield-allocation` | Jupiter orbiter | Radiation Shield Allocation | 50 s planning |

The project uses Unity 6000.6.9f1, URP, uGUI 2.6 with legacy `Text`, and the Input System only (`activeInputHandler: 1`).

TextMeshPro Essential Resources are not imported in this project. The team's UI uses legacy `Text`, so the minigames do too. No packages were added or updated.

## Architecture

```
Game flow / mission event
   └─ MinigameGameFlowAdapter.Run(context, onCompleted)        Assembly-CSharp (Integration/)
         └─ MinigameManager.StartMinigame(context, callback)    SagittariusA.Minigames assembly
               ├─ MinigameRegistry      ID -> type + MinigameDefinition ([Minigame("id")] attribute)
               ├─ MinigameSession       plain C# lifecycle: one active task, exactly one result, always restore
               │     └─ MinigameInputLock (IMinigameEnvironment): timeScale, 3D input, PlayerInput maps, cursor, UI selection
               ├─ MinigameShellView     overlay canvas, blocker, title, instruction, progress, timer, feedback, result panel
               └─ <Mission>Minigame : MinigameBase   view only; rules live in <Mission>Rules (plain C#)
   └─ IMinigameSimulationAdapter.ApplySuggestedEffects(result)   simulation owner implements
```

- **Rules and UI are separate.** Every task has a plain C# rules class: `ImageCalibrationRules`, `PowerGridRules`, `DustStormRules` and `ShieldAllocationRules`. The rules classes have no Unity dependency and own all scoring, outcomes and suggested effects. The MonoBehaviour views only draw state and forward input.
- **The manager knows no rules.** It resolves the ID, creates the shell, adds the minigame component, forwards `Tick(Time.unscaledDeltaTime)`, shows the result panel and returns the result.
- **No simulation formulas are duplicated.** The Mars storm uses its own display values, not the mission power model. Minigames only *request* effects.

## Folders

```
Assets/SagittariusA/Minigames/
  Runtime/                         SagittariusA.Minigames.asmdef (refs Unity.InputSystem, UnityEngine.UI)
    Core/                          contracts, registry, session, manager, input lock, audio hooks, MinigameBase, IMinigameSimulationAdapter
    UI/                            MinigameShellView, instruction/progress/result views, Ui helpers, MinigameTheme
    EarthImageCalibration/         ImageCalibrationRules + EarthImageCalibrationMinigame
    MoonPowerRouting/              PowerGridRules (+ 3 layouts) + MoonPowerRoutingMinigame
    MarsPowerManagement/           DustStormRules + MarsPowerManagementMinigame
    JupiterShieldAllocation/       ShieldAllocationRules + JupiterShieldAllocationMinigame
    Resources/Minigames/Earth/     eo_<band>_<blur 0-3>.png  (stylized observation images)
    Resources/Minigames/Audio/     hover, press, valid, invalid, progress, success, partial, failure .wav
  Integration/                     Assembly-CSharp: MinigameGameFlowAdapter, PendingEffectsSimulationAdapter, MinigameTestLauncher
  Scenes/Minigame_TestLauncher.unity
  Tests/EditMode/                  NUnit rule and framework tests (asmdef, Editor only)
  Tests/PlayMode/                  lifecycle tests with the real manager
```

**Prefabs.** Like the existing Earth screens, the shell and the minigame contents are built in code at runtime, so there are no prefabs to keep in sync and no binary prefab editing. To move to prefabs later, wrap `MinigameShellView.Create` and each `BuildContent` in prefab instantiation. The public API does not change.

## Starting a minigame

From a mission event or `GameFlowController` (Assembly-CSharp):

```csharp
using SagittariusA.Gameplay.Minigames;   // MinigameGameFlowAdapter
using SagittariusA.Minigames;            // MinigameContext, MinigameResult

var context = new MinigameContext
{
    MinigameId = "earth-image-calibration",
    MissionId = "earth",
    EventId = "camera-calibration",
    DifficultyLevel = 1,
    RandomSeed = 12345,
    TimeLimitSeconds = 60f,      // -1 = use the definition default, 0 = untimed
    AllowCancel = false
};
context.DisplayValues["orbit"] = "Low Earth orbit";   // optional, display only

MinigameGameFlowAdapter.Run(context, result =>
{
    Debug.Log($"Minigame finished: {result.Outcome}, score {result.Score}");
    // Effects have already been passed to MinigameGameFlowAdapter.Simulation.
    ResumeMissionEvent(result);
});
```

You can also call the manager directly. In that case, apply the effects yourself:

```csharp
MinigameManager.Instance.StartMinigame(context, result =>
{
    Debug.Log($"Minigame finished: {result.Outcome}, score {result.Score}");
    simulationAdapter.ApplySuggestedEffects(result);
});
```

Event-based alternative: `MinigameManager.Instance.Started += ctx => ...;` and `MinigameManager.Instance.Finished += result => ...;`.

**Guarantees:**

- The callback fires **exactly once** per `StartMinigame` call.
- It fires after the overlay has closed and pause and input have been restored.
- It fires even for a rejected start: unknown ID, another minigame already active, an initialization exception, or a scene change. Those cases return `Cancelled` (or `Failure` for a runtime error) and log a developer message.

To test, open `Assets/SagittariusA/Minigames/Scenes/Minigame_TestLauncher.unity` and press Play. The launcher lets you:

- pick any task
- change difficulty 1–3 and the seed
- turn the time limit and cancel permission on or off
- see the returned result and its suggested effects

## MinigameResult fields

| Field | Meaning |
|---|---|
| `MinigameId`, `MissionId`, `EventId` | Copied from the context. |
| `Outcome` | `Success`, `PartialSuccess`, `Failure`, or `Cancelled` (player closed, scene changed, start rejected). |
| `Score` | 0–100, always clamped. The base is 100, −8 per mistake, and up to −20 for time used. Partial results are capped at 70 and failures at 35–40. |
| `CompletionTimeSeconds` | Unscaled seconds spent in the task. |
| `MistakeCount` | Wrong confirmations or rejected actions (Mars: essential systems switched off). |
| `CompletionPercent` | 0–1 share of the task goal reached (settings correct, critical systems powered, storm survived, zones protected). |
| `Summary` | One player-facing sentence. |
| `EducationalNotes` | `[0]` is the task's educational line. Extra notes may follow, for example capacity warnings. |
| `SuggestedEffects` | Requested consequences: `EffectType`, `Value`, `Explanation`. |

## Suggested effects

| Task | Success | Partial | Failure |
|---|---|---|---|
| Earth | ScienceReturn +0.10, DataQuality +0.10 | DataQuality −0.10 | DataQuality −0.30, ScienceReturn −0.20 |
| Moon | PowerAvailability +0.10 | ScienceReturn −0.15 | Reliability −0.20, MissionTimePenalty +1 |
| Mars | Reliability +0.05 | ScienceReturn −0.15 | Reliability −0.30, MissionTimePenalty +2 |
| Jupiter | RadiationDamage −0.25, Reliability +0.05 | ScienceReturn −0.15 | RadiationDamage +0.35, Reliability −0.25 |

Effect type names are constants in `MinigameEffectTypes`.

**Simulation integration point.** Implement `SagittariusA.Minigames.IMinigameSimulationAdapter` in the simulation code and assign it once at startup:

```csharp
MinigameGameFlowAdapter.Simulation = new MySimulationEffectAdapter(missionSimulationService);
```

Until then, `PendingEffectsSimulationAdapter` logs every result and stores the effects per mission. The simulator can collect them later with `TakePendingEffects(missionId)`. No engineering values are changed.

## Pause and input locking

`MinigameInputLock` captures state on start and restores it on every exit path. Success, partial, failure, cancel, an exception during start, a scene change and the manager being destroyed are all exit paths.

- **Time.** `Time.timeScale` is saved and set to 0 (`MinigameDefinition.PauseWorld`). All minigame logic uses `Time.unscaledDeltaTime`.
- **3D input behaviours.** Registered behaviours are disabled: `MoonFirstPersonController` and `MissionSimulation.Player.PlayerController`. `MinigameGameFlowAdapter` finds both by name, so the folder also compiles in a project without them. You can add more with `MinigameInputLock.RegisterBlockerType<T>()`.
- **PlayerInput.** Every `PlayerInput` action map except `UI` is disabled.
- **Cursor.** The cursor is unlocked and shown, then restored afterwards.
- **UI selection.** The previously selected UI element is restored.
- **EventSystem.** If none exists, a temporary one is created with `InputSystemUIInputModule` and destroyed on close.
- **Click blocking.** The shell is its own overlay canvas (sorting order 1000) with a full-screen raycast blocker, so clicks never reach the 3D world or the UI behind it.

## Controls and accessibility

- **Mouse:** drag sliders, tiles and cables, and click buttons.
- **Keyboard and controller:** Tab or arrows to navigate, Enter or Space to activate. Escape cancels only when `AllowCancel` is true.
- **State is never shown by colour alone.** Every state has a word chip, and some also use a pattern:
  - OK/CHECK feedback chips
  - SHARP / BLURRED indicators
  - POWERED / NO POWER labels
  - striped (unpowered) vs. solid (energized) cables
  - RISK HIGH with bar count and EXPOSED / PROTECTED for shields
  - shape-coded result badges
- **No flashing.** Animations are short slides and fades.
- **Time limits.** `MinigameSettings.DisableTimeLimits = true` runs every task untimed. A context `TimeLimitSeconds = 0` does the same for one run. Mars still plays its 45 s storm, because the storm is the mechanic, not a time limit.
- **Restart.** Restart resets the board but keeps the clock and the mistake count, so it is not a free retry. Mars has no restart because its storm is a timeline (`AllowRestart => false`).

## Determinism

Each task draws targets, layouts and storm curves from `MinigameRandom.Create(context, salt)`. This is a `System.Random` seeded from `RandomSeed`, `DifficultyLevel` and a stable salt. `UnityEngine.Random` is never touched.

The same seed and difficulty always produce the same setup. Results depend only on the player's visible actions.

| Difficulty | Earth tolerance | Moon layout | Mars | Jupiter mass slack |
|---|---|---|---|---|
| 1 | 0.08 | 7×5 "Crater rim" | essential tolerance 3 s | +2 |
| 2 | 0.07 | 8×6 "Boulder field" | deeper dips, 2.5 s | +1 |
| 3 | 0.06 | 9×6 "Shadowed ridge" | deepest dips, 2 s | +0 |

On the Moon, an odd seed mirrors the layout. Every layout has a reference solution that the tests replay.

## Replacing placeholder visuals and audio

| Asset | Where | Replace with |
|---|---|---|
| Panel, card and button shapes, circle, hatch pattern | `MinigameTheme.Rounded/RoundedSmall/Circle/Hatch` (generated at runtime) | Assign imported sprites in `MinigameTheme`. Keep nine-slice borders. |
| Colours | `MinigameTheme` | Update the constants. |
| Earth observation images | `Runtime/Resources/Minigames/Earth/eo_<visible/infrared/multispectral>_<0..3>.png` | Keep the names. Level 0 is sharp; levels 1–3 are increasingly blurred. Any 16:10 size works. |
| Spacecraft diagram (Jupiter) and site map markers (Moon) | Built from shapes in the view classes | Swap `Ui.Panel` calls for `Image` sprites. |
| Sounds | `Runtime/Resources/Minigames/Audio/<cue>.wav` | Same file names: `hover`, `press`, `valid`, `invalid`, `progress`, `success`, `partial`, `failure`. |

## Adding another minigame (no manager changes)

1. Write a plain C# rules class: state, actions, `Tick(dt)`, `BuildResult(context)`. Add EditMode tests for it.
2. Create `MyTaskMinigame : MinigameBase` with:
   - `[Minigame("my-task-id")]`
   - `public static MinigameDefinition Definition => new MinigameDefinition { ... }`
   - optionally `public static bool AllowRestart => false;`
3. Implement:
   - `OnInitialize`: create the rules from `Context` and `TimeLimit`.
   - `BuildContent(content)`: the content area is 1520×610 px, positioned from its top-left.
   - `OnTick`: return true when the task ends.
   - `Refresh`, `BuildResult`.
   - Optionally `OnRestart` and `OnCancelled`.
4. Call `Finish()` when the player completes the task. Use `Feedback(text, FeedbackKind)` for feedback and `Shell.SetProgress(value, label)` for progress.

The registry discovers the type automatically. Plain `IMinigame` classes without UI work too.

## Tests

EditMode tests are in `Tests/EditMode`:

- `MinigameFrameworkTests`, which cover the required checks 1–7, plus result panel and time limits
- `MinigameRulesTests`, which cover checks 8–20, plus determinism and a balance check for each task

PlayMode tests are in `Tests/PlayMode/MinigameLifecyclePlayModeTests`. They cover the real manager: pause, input and restore on cancel and on success, the one-active rule, unknown IDs, and that all four IDs are registered.

Run them from Window > General > Test Runner.

## Known limitations

- These were written and verified outside the Unity editor. The rules and framework tests ran under .NET (56 passing), and every Unity script was compile-checked against API stubs. The first in-editor run should confirm layout at 1920×1080, slider feel, and the PlayMode tests.
- The Earth live preview fakes blur by cross-fading four pre-blurred images. Focus is therefore stepped, not continuous.
- Moon layouts are three fixed designs; the seed only mirrors them.
- The test launcher scene is not added to Build Settings, to avoid changing project settings. Open it directly in the editor.
- `PendingEffectsSimulationAdapter` only stores effects. The simulation owner must implement `IMinigameSimulationAdapter` for effects to change mission values.
