# Mars playable slice

This slice implements the current `First Test Signal` brief. Mars is a planned orbiter assembly stop in a functioning hangar; there is no jammed-bay rescue.

## Standalone editor entry

Use **Far Signal > Mars > Play Standalone Mars Test**. The command opens a new unsaved scene after Unity offers to save current scene changes, creates the runtime prototype, and enters Play mode. Its `MemoryMarsDraftStore` is isolated in memory: it does not read or overwrite the real campaign save. The test commander is `Нара`, the Pole-7 archive is available, and one optional `spares` box is carried so both antenna recovery paths can be exercised.

Transit controls are A/D or Left/Right. Hold the indicator in the target band for one second. **Check Alignment** records a miss; target assistance appears after two misses. The spares path requires a second confirmation click and removes one box once. Packet transmissions visibly run for their 3- or 7-second game duration. Completed packets remain complete; an unfinished first packet stays queued for the next 8-second contact window.

On Mars: WASD moves, mouse looks, Space jumps with 3.71 m/s² Mars gravity, left click or E uses a highlighted station, and Escape releases the cursor. The short route is landing pad, marked approach, operating airlock, assembly bay, bench, fixed loader lane, and ship cradle. The weather mast and packet-practice console are optional side stations.

## Main path

At the assembly bench, configure the sensor mode, antenna, and shield. Live mass uses the draft 300 kg Mars cradle limit, separate from Earth launch totals. Select each shelf part and place it on its labelled keyed mount; a mismatched mount is rejected. Installed sensor, antenna silhouette, shield thickness, POWER cable, and DATA cable update visibly.

At the bench, connect the separate POWER and DATA ports, then power on, sample, and transmit. Missing DATA deliberately produces a blank **Mars Bench Test** graph and a recovery hint. A successful graph labels its axes **Sample** and **Simulated particle count**. Changing hardware or either connector invalidates the previous test.

At the loader panel, move the fixed-path loader, seat the exact tested build, close the separate left and right clamps, and receive the deployment plan and first-contact schedule. **Continue to Ganymede**, then **Begin Ganymede Transit**, passes the exact `MarsGanymedeHandoff` into the playable finale. Standalone Mars testing continues into Ganymede with an isolated memory store; campaign runs use the persistent draft. See `GanymedePlayableChapter.md`.

## Authored environment

The release uses the supplied FarSignal-Mars-Assembly environment, exported by `Tools/Blender/export_mars_environment.py`, with authored terrain, hangar, workstations, shelves and route anchors. Gameplay adds keyed mounts, connectors, loader state and interaction targets. The crew ship uses the team's film ship asset. Preserve mission-rule ownership and the labelled mount, connector, loader, clamp, and station states when editing the art.

## State and validation

`MarsMission` owns validation and durable draft state. Presentation sends revisioned operations. Duplicate operation IDs are idempotent; stale revisions, commands from another run, and out-of-order completion are rejected. The save contains antenna recovery/spending, completed and queued packets, exact configuration, graph signature, loader/clamps/plan, optional observations, and the original Moon outcomes/inventory. The delivered P-4 panel and any battery left on the Moon are never regenerated.

Use **Far Signal > Mars > Run Mars Acceptance Checks** for the Unity editor checks. The batch entry is `SagittariusA.EditorTools.Mars.MarsAcceptanceChecks.RunBatch`.

The release source compiles with Unity 6.6. Earlier rule checks passed before the last presentation changes. The final route, ship placement, UI, and loader motion have not been re-tested in Play Mode: the project owner explicitly stopped runtime testing before export. Do not interpret a successful compile/export as a complete gameplay acceptance result.
