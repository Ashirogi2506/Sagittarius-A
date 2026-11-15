# Moon 3D interaction integration

`MoonStageController` owns Moon progress, descent fuel spending, cart rules and the local draft save. The 3D scene reports physical interactions; it must not modify campaign state or unlock objectives directly.

## Scene bridge

Add `MoonSceneConnector` to a Moon scene object. The campaign scene loader must keep the existing `MoonStageController` alive and call `connector.Bind(controller)` after the Moon scene loads. Unbinding is automatic on destruction. The loader and scene transition are not implemented yet; do not create a second controller in the Moon scene.

Read `connector.Context` and use `connector.IsAvailable(id)` to enable targets. The Inspector event `onContextChanged` fires when the context is initialized or updated. Treat objects as unavailable until their exact ID appears. Refresh visual state from the context after loading a save.

Report an interaction with the supplied run ID:

```csharp
connector.Interact(MoonInteractionIds.SwitchCartOff);
```

For a dust patch, call `connector.HoldPatch(id, Time.deltaTime)` once per frame while held, and `connector.EndHold()` on release, target loss or switching patches. Do not send total session time. Inspect updated `Context.cart` to drive visual effects; a callback is a request and does not itself prove an action was accepted.

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
