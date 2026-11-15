# Moon scene handoff for the 3D / interaction teammate

Work from the `feature/gameplay-ui` branch, then create your own `feature/moon-scene` branch. Open the Unity project with editor 6000.6.9f1. Coordinate changes to shared scenes and scripts before editing them. Keep your Moon scene and prefabs separate from Earth UI files. Existing source art includes `Moon_Level.blend` and `FarSignal-Moon-70.blend` in the repository root; inspect them and agree with the art owner which version to use. Export/import an optimized scene with sensible scale, colliders and lighting. Do not assume the raw Blender file is already a playable Unity scene.

## First playable milestone

- Create a Moon arrival / Pole-7 scene with a ground surface, player spawn, player camera, lighting, a visible cart and reachable interaction targets. Provide keyboard movement and look plus clear interaction feedback.
- Create targets for the cart power switch, wheel cover, three distinct dust patches, panel and repair kit. The player must be able to reach and identify them.
- Place `MoonSceneConnector` on a scene object. Integrate using `MoonInteractionIntegration.md`; the campaign loader will supply the existing `MoonStageController` using `Bind`. The loader is still a separate task and there is no automatic Earth-to-Moon scene transition yet.
- Read `IsAvailable` before showing prompts or accepting interaction. Use the existing constants in `MoonInteractionIds`; do not invent new strings for existing actions.
- Use `Interact` for clicks/taps, and `HoldPatch(id, Time.deltaTime)` every frame for active patch holds. Call `EndHold` when released, looking away or switching patch.
- Drive the cart visuals from `Context.cart` after context changes: power, cover, three cleaned patches, retest and cargo loaded/locked. A reported action may be rejected, so do not grant progress or play an irreversible success animation before the updated state confirms it.
- Show `connector.Notice` and the current objective to the player. The sequence and exact IDs are in `MoonInteractionIntegration.md`. Descent is a separate confirmation action; show the landing plan and fuel outcome supplied by gameplay.
- Call out scene setup, spawn points, control mappings, prefabs and dependencies in your pull request. Include a short test recording or steps.

## Acceptance checks together

After the loader exists, finish Earth flight checks and confirm the Moon scene receives the same commander, ship, cargo and landing plan. Verify the cart requires start, switch off, open cover, three one-second holds, close and retest; then pick up and load the panel and kit one at a time. Verify out-of-order actions do not progress. Leave and resume during repair or while carrying cargo; restored visuals must match saved state. Verify descent fuel is spent once and insufficient fuel reports the restart state. Exercise this in Play mode using the real scene and gameplay controller.

## Boundaries and open decisions

Gameplay owns rules, fuel, progression, save data and UI state; the scene owns art, colliders, animations, audio and input presentation. Campaign integration must implement scene loading, a durable campaign save and the pre-launch checkpoint restore. The exact manual-versus-laser opening interaction remains undecided: do not invent mission completion rules for it. The current Moon controller covers the cart slice, not the complete Moon chapter.
