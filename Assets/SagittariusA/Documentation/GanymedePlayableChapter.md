# Ganymede gameplay and construction

This chapter follows the Mars handoff in `FarSignal.Campaign-Team-Prompt.md`. User decisions on 2026-11-15 override its optional construction scope: **the radiation station, ice survey station and uplink relay must all be built before departure**. Terrain photography and the full science graph remain optional. Explanations stay in the major task panels; H opens the checklist on demand.

## Play

- Unity: **Far Signal > Ganymede > Play Standalone Ganymede Test**. This uses an in-memory save and the basic Mars orbiter. It does not alter campaign saves.
- GameplayUI main menu in the editor: **GANYMEDE TEST**.
- Campaign: Moon results → Begin Mars Transit → assemble/test/load the orbiter → Begin Ganymede Transit.
- Continue Expedition resumes Mars or Ganymede through the completed Moon handoff belonging to the current expedition. A completed Ganymede run reopens its flight log.

WASD moves, mouse looks, Space jumps, left click/E uses an object, right click collects/packs the dosimeter, H opens help, Escape releases the mouse/closes surface panels, and Tab returns to the menu. The surface uses a CharacterController and mesh colliders, with a safe lander spawn on entry/resume.

## Shared environment and independent equipment

The supplied `FarSignal-Ganymede-Start.blend` and `FarSignal-Ganymede-Completed.blend` contain **146 identical shared meshes**. The export contains those meshes once. No full-scene swapping or terrain duplication is used.

`Resources/Ganymede/GanymedeEnvironment.fbx` has these groups:

| Group | State |
|---|---|
| SharedEnvironment | Always present: terrain, horizon and fragments. Jupiter remains visible during cabin/orbit presentation. |
| ArrivalEquipment | Lander, equipment case, site markers, plates and route stakes visible on arrival. |
| Built_Radiation | Activated only by commissioning Site A. |
| Built_IceSurvey | Activated only by commissioning Site B. |
| Built_Uplink | Activated only by commissioning the relay at C. |
| Anchors | Authored spawn, return point, both survey sites and overlook. |

`GanymedeEnvironmentView.SetBuilt` restores these groups from saved mission flags without moving the player. Objects, renderers and colliders activate together. The Unity importer keeps mesh data readable for runtime collision cooking. FBX scale is normalized against the authored 180 m terrain width.

Original source files are preserved in `SourceArt/Ganymede/`. `Tools/Blender/export_ganymede_environment.py` exports evaluated geometry and bakes procedural base colors into the supplied terrain/horizon/Jupiter textures. It never saves changes into either source file. The installed Blender 4.5 reports that the files were written by a newer version; the evaluated export and FBX round trip were checked, but final Unity visual review is still required.

## Main path

1. Transit: cabin/radio stay on at 3 units out of 5. Turn off the 2-unit camera, then run the 2-unit orbiter checkout. Confirm the approach plan and land.
2. Pick up the equipment case beside the lander. At each station, fit the base, keyed instrument head and power connector into matching sockets. Commission to reveal its completed model. Wrong sockets and missing prerequisites give local retries; partial construction saves.
3. At A and B, place the portable dosimeter, level both axes within ±0.12, and hold a three-second sample. Walking away interrupts only the current sample. The placed, leveled instrument stays available. Pack it before moving to the other site.
4. Save both records and the supported comparison at the lander. Site IDs fix A at 24 and B at 16 simulated μSv/h, regardless of visit order. The game makes no safety claim from one reading per site. All three stations, both records, comparison and packed instrument are required for ascent.
5. Confirm the crew, reach the marked point, release both clamps once, and view the separation. Skip preserves the same saved release. The light antenna locks automatically; the directional antenna needs one second in its target band.
6. Send health (2 s) and small reading (4 s). Light contacts last 10 s; directional contacts last 14 s. Full graph is optional and takes 9/12 s for standard/frequent modes. Received packets persist across retries and reloads. Unfinished packets remain queued.
7. Time-skip to Earth's acknowledgement, then open the campaign flight log/credits. Surface dose records and orbiter particle counts remain separate.

## Ownership and integration

`GanymedeMission` owns rules and saves. `GanymedePlayableExperience` owns input, UI and presentation; it never spends Moon/Mars resources. It accepts the real `MarsGanymedeHandoff`, including commander, inventory, module selection, bench graph and practice packet order. `PlayerPrefsGanymedeStore` is a versioned local draft; injected memory stores isolate tests. The handoff signature prevents one expedition from resuming another expedition's draft.

`GanymedeWorldTarget` IDs are `case`, `lander`, `crater-plain`, `grooved-rise`, and `uplink-relay`. Authored markers bind to these IDs. The dosimeter is a movable instrument; the three commissioned stations remain installed.

## Validation

- Production Mars and Ganymede rule sources: **11 acceptance groups passed**, including reverse site order, wrong comparison, all-three construction gate, partial construction reload, sample interruption, one-time release, directional lock, packet retries and Unicode name.
- Production Earth packing rules: **4 groups passed**, including all four ready-made builds.
- Export check: **238 meshes**, one shared terrain, all construction groups, all five anchors, and a 180 m terrain width survived an FBX round trip.
- Play Mode test entry: **Far Signal > Ganymede > Run Full Ganymede and Cargo Play Checks**. It runs actual UI callbacks and timers, collision/target checks, construction toggles and text layout checks in isolated memory. Interactive execution remains pending until Unity is available.

The imported ZIP's Jupiter shielding exercise remains optional practice. It is not inserted as a new required campaign gate.
