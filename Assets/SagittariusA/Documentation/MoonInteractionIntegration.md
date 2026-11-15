# Moon 3D interaction integration

`MoonStageController` owns Moon progress, descent fuel spending, cart rules and the local draft save. The 3D scene reports physical interactions; it must not modify campaign state or unlock objectives directly.

## Scene bridge

Implement `IMoonInteractionDriver` on one `MonoBehaviour` in the Moon scene. Find the persistent `MoonStageController` and call `RegisterDriver(this)` after the scene is ready. Call `UnregisterDriver(this)` before teardown.

`Initialize` receives a `MoonStageContext` and callback. Bind only the IDs in `availableInteractionIds`. After each accepted callback, `UpdateContext` supplies a fresh snapshot and the next allowed IDs. Treat all objects as unavailable until their exact ID appears.

Report an interaction with the supplied run ID:

```csharp
report(new MoonInteractionResult {
    runId = context.runId,
    interactionId = MoonInteractionIds.SwitchCartOff,
    completed = true
});
```

For a dust patch, report the actively held frame time as `elapsedSeconds`. Call `MoonStageController.EndPatchHold()` when the key is released or the player looks away. Do not send total session time.

## Implemented sequence

1. `moon.descent.confirm` commits the saved Earth landing plan once and deducts its fuel once.
2. `pole7.cart.start` discovers the wheel jam.
3. `pole7.cart.switch_off` makes the cover safe to open.
4. `pole7.cart.cover_open` exposes three dust targets.
5. Hold `pole7.cart.patch_1`, `_2` and `_3` for one second each.
6. `pole7.cart.cover_close` and `pole7.cart.retest` verify the repair.
7. `pole7.cart.load_panel` and `pole7.cart.load_kit` complete this Moon slice.

The controller rejects stale run IDs, duplicate callbacks and out-of-order actions. An insufficient-fuel descent changes the phase to `RestartAtPreLaunch` and raises `RestartAtPreLaunchRequested`; the campaign coordinator must perform the actual checkpoint restoration.

## 3D teammate ownership

The scene owner supplies models, colliders, highlights, animation, sound and interaction timing. `PlayerWorldTarget` can be used for raycast targets, but its Inspector event should call the bridge rather than changing mission state. The gameplay owner supplies objective UI, save handling, validation and chapter progression.

The exact manual-versus-laser opening sequence is not implemented because its physical interaction is still undecided. `MoonStageContext.laserCarried` and `laserUsable` are available when that sequence is specified.
