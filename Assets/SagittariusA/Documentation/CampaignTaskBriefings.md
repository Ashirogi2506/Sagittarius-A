# Campaign task explanations and shared minigames

Task explanations are adapted from the user's `FarSignal.Campaign-Team-Prompt.md` (Shared rules, character dialogue, Earth chapter and Moon chapter). English player-facing text, Unicode player names, optional beacon work, essential power before relay, and the working Mars assembly-bay invitation follow that version. Control descriptions reflect the actual playable implementation.

`CampaignTaskBriefings` supplies context, purpose, steps and controls for all 30 Moon interactions and the three Earth activity contracts. `TaskBriefingView` pauses world time while reading, restores cursor/selection/time, and calls its acceptance callback once. Back/Escape grants no progress. Moon automatically explains only four milestones: arrival, wheel repair, the service route and Pole-7 repair. Small actions start immediately; H explains the focused or next task without executing it. Leaving a portable battery still asks for confirmation because it removes that battery from the Mars inventory. Acknowledged explanations are remembered for the current surface visit; failed minigames can be retried immediately.

Earth UI calls `BeginExplainedActivity`; the registered runner starts only after Begin Task. The existing synchronous `BeginActivity` API remains available for runner integration/tests. Cargo is pre-launch; power and landing planning are still in flight.

## Imported ZIP scope

Source: user-supplied `FarSignal-Unity-Project-Share.zip`, folder `Assets/SagittariusA/Minigames` only. Its older game code, art and project settings were not imported. The supplied framework, four modules, UI/audio resources, standalone test scene and NUnit tests live in that folder. Integration changes repair duplicate-start shell cleanup and restoration after a partial pause/input capture failure.

| Supplied module | Current use |
| --- | --- |
| `moon-power-routing` | Live Pole-7 essential-power task after installing P-4. |
| `earth-image-calibration` | Standalone practice; does not replace cargo/power/landing runners. |
| `mars-power-management` | Standalone dust-storm practice; does not replace the newer Mars orbiter-assembly chapter. |
| `jupiter-shield-allocation` | Standalone practice; does not change the orbiter selected on Mars. |

To try all four, open `Assets/SagittariusA/Minigames/Scenes/Minigame_TestLauncher.unity`, enter Play mode, choose a module and select Begin Task. Each uses one explanation screen before its timer starts. This scene writes no campaign saves.

## Live Moon contract

`MoonPlayableExperience` starts the manager with `MinigameId=moon-power-routing`, `MissionId=pole-7`, `EventId=pole7.base.essential`, difficulty 1, seed 0 and no timer. This profile requires essential services and local radio. Research and extra lights are optional. SOL and BAT refer to the base's supply, not portable cargo batteries.

Only a matching Success result, acknowledged with Continue while the same Moon run is still active, advances `RestoreEssential`. Cancelling, failure, reading help or closing the surface cannot grant this milestone. The campaign profile returns no suggested resource modifiers. Earlier repair progress survives retries. World movement stops throughout reading, playing and the result panel.

## Verification entry points

- `Far Signal > Run Campaign Task Briefing Checks`: coverage of every Moon interaction and Unicode name preservation.
- `Run Moon Mouse Interaction Flow`: queued real mouse/keyboard events through all required Moon actions, Back/H/Begin behavior, grid cancellation/failure/retry/success, cart rides and final results. Player positioning and travel time are accelerated for the test.
- `Run Moon Repair Panel Checks`: the existing 13 repair panel variants, wrong inputs and cancellation.
- `Run Moon Ridge Flow Checks`: route geometry, turning, cargo, state transitions and second spawn using the other landing pad.
- Unity Test Runner: supplied EditMode rule/framework tests and PlayMode manager lifecycle tests, with added campaign-profile and failure-restoration coverage.

`CampaignTaskBriefingChecks.StartAutomation` is an explicit editor automation entry for the description layout, repair panels, full mouse flow and Ridge route. It uses an isolated in-memory Moon save and ends Play mode (batch execution exits the editor with a pass/fail code); it never starts automatically in normal editing.

## Latest verification (2026-11-15)

The gameplay/editor assemblies and imported PlayMode test assembly compile against Unity 6.6 references with zero warnings and zero errors. All 58 supplied and extended pure-C# rule/framework cases pass. Coverage checks against the compiled gameplay assembly pass for 30 Moon descriptions, all three Earth activities and the Unicode commander name.

The earlier full mouse-driven Moon flow, all 13 repair panels, description layout/visual review and Ridge route/respawn checks passed in Unity. The explanation frequency has since been reduced to four milestones; its updated checks verify that smaller interactions open directly. The four imported framework PlayMode tests were started, but their result was not confirmed before desktop control was stopped.
