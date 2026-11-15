# Campaign subtitle handoff

The dialogue work is in a separate worktree from the existing uncommitted gameplay changes. Review and integrate the changed files at their existing paths; do not create another copy of `Assets/SagittariusA` or replace unrelated project assets. The branch also contains the previously needed `MoonFirstPersonController.cs.meta` GUID correction.

`CampaignDialogue.json` contains the Earth opening and Earth event dialogue from the campaign brief, plus the longer milestone exchanges for Moon, Mars and Ganymede. `CampaignDialogueDirector` displays one line at a time at the bottom center, substitutes the saved commander name as plain text, uses a reading-time estimate, and permits skipping the current sequence with Enter or the Skip button. The timer pauses while an Earth minigame is active and while the Moon player is moving, jumping or holding the interaction key. Completed/skipped sequences are recorded in the existing Earth or Moon draft save; older saves without the new list load with an empty list. Interrupted sequences replay when their trigger is encountered again.

Connected to existing events:

| Trigger | Existing event |
| --- | --- |
| `earth_opening` | Earth briefing screen opens |
| `garage_first_open` | Configuration screen opens |
| `required_cargo_secured` | Successful cargo activity returns |
| `orbit_reached` | Earth Results screen opens |
| `moon_arrival` | Successful descent changes phase to Pole-7 cart repair |
| `moon_wheel_passed` | Wheel test changes from not passed to passed |

The dialogue is not a mission progression authority: completing or skipping it only records its own played flag. The launcher and minigame scenes do not start it. Existing Moon gameplay returns to the pre-launch checkpoint on insufficient fuel and this change does not alter that leader-owned decision.

`laser_overload` and `landing_map_first_open` are present in the data but not wired: the current activity runner lacks a safe pre-interaction event that can wait for a subtitle without interfering with the activity. The `moon_relay_uploaded`, `mars_bench_passed`, `ganymede_both_readings`, `orbiter_released`, and `earth_acknowledgement` entries are also not wired because the corresponding playable chapter events do not yet exist here. The brief has additional short contextual lines beyond these stored sequences. The gameplay owner should connect them when the relevant events and campaign-wide save contract exist; do not trigger them merely because a scene opens.

Compile was checked with the installed Unity version's actual compiler response file and references against the current gameplay code. This does not replace opening this worktree in Unity, checking subtitle position at different resolutions, exercising Skip with mouse/keyboard/controller, checking Unicode names, testing save/reload and running a full campaign playthrough.
