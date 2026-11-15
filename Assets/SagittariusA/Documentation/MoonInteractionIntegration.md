# Moon 3D interaction integration

`MoonStageController` owns Moon progress, descent fuel spending, cart rules and the local draft save. The 3D scene reports physical interactions; it must not modify campaign state or unlock objectives directly.

## Scene bridge

Add `MoonSceneConnector` to a Moon scene object. The campaign scene loader must keep the existing `MoonStageController` alive and call `connector.Bind(controller)` after the Moon scene loads. Unbinding is automatic on destruction. The loader and scene transition are not implemented yet; do not create a second controller in the Moon scene.

Read `connector.Context` and use `connector.IsAvailable(id)` to enable targets. The Inspector event `onContextChanged` fires when the context is initialized or updated. Treat objects as unavailable until their exact ID appears. Refresh visual state from the context after loading a save.

Report an interaction with the supplied run ID:

```csharp
connector.Interact(MoonInteractionIds.SwitchCartOff);
```

The playable surface calls `MoonPlayableExperience.RequestInteraction(id)` to open a repair panel or start a cart trip. Its success callback reports to the connector; travel reports only after physical arrival. Do not wire a repair marker directly to `connector.Interact`, since that bypasses the panel. The controller remains the authority on ordering and save state.

The low-level dust contract is still `connector.HoldPatch(id, elapsedSeconds)`. A completed brush panel submits `CartWheelMinigame.HoldSecondsPerPatch` once as its validated success; cancellation submits nothing. An alternate 3D hold implementation can call it once per frame while held, with `EndHold()` on release. Inspect updated `Context.cart` to drive visuals; a callback alone is not proof of acceptance.

## Implemented sequence

1. `moon.descent.confirm` commits the saved Earth landing plan once and deducts its fuel once.
2. `pole7.cart.start` discovers the wheel jam.
3. `pole7.cart.switch_off` makes the cover safe to open.
4. `pole7.cart.cover_open` exposes three dust targets.
5. Complete the brush panel for `pole7.cart.patch_1`, `_2` and `_3`.
6. `pole7.cart.cover_close` and `pole7.cart.retest` verify the repair.
7. `pole7.cargo.pickup_panel` or `pole7.cargo.pickup_kit` picks up one item at a time. Report the corresponding `pole7.cart.load_panel` or `pole7.cart.load_kit` only after pickup. `MoonStageContext.carriedCargoId` contains `panel`, `kit` or no item and is included in the saved draft.
8. Drive to the bridge, inspect it, survey the firm route, drive to Pole-7 and deliver the cargo. The steep route and beacon inspection are optional.
9. Isolate the circuit, repair it with the kit or an eligible powered laser, install the panel, restore essential power and relay, then upload the route.
10. Drive back to the landing area and return to the ship. Completion exposes a Mars staging summary and `GetMarsHandoff()`; the Mars scene is not yet implemented.

The controller rejects stale run IDs, duplicate callbacks and out-of-order actions. An insufficient-fuel descent changes the phase to `RestartAtPreLaunch` and raises `RestartAtPreLaunchRequested`; the campaign coordinator must perform the actual checkpoint restoration.

## 3D teammate ownership

The scene owner supplies models, colliders, highlights, animation, sound and interaction timing. `PlayerWorldTarget` can be used for raycast targets, but its Inspector event should call the bridge rather than changing mission state. The gameplay owner supplies objective UI, save handling, validation and chapter progression.

The kit opens a cable-matching panel; an eligible laser opens a seam-tracing panel. Their 3D animations remain for the scene team. `MoonStageContext.laserCarried` and `laserUsable` expose the eligibility to the scene. All other repair steps also open task panels via `MoonRepairPanel.Supports(id)`.
