# Earth ship configuration

GameScreen.Configuration opens EarthAssemblyView directly. The separate list garage and manual Fit step have been removed. Briefing, Continue Expedition, report back buttons and repair guidance all return to this same screen.

Choose a ready-made ship, then optionally customize it. Clicking hardware swaps it immediately. Clicking cargo adds/removes it. The 3D model, selected indicators, limits and saved build update before the next rendered frame. INFO only opens a description. Clicking the 3D model selects its category; it never changes the loadout.

## Ready-made builds

These are assembled hardware configurations, not completed mission checks. All include the required panel and kit and fit the current catalog's budget, mass and compatibility limits.

| Model | Purpose | Cost | Mass / capacity | Capacity / reserves |
|---|---|---|---|---|
| Balanced | Lower-cost starting ship | $93M | 9,570 / 10,100 kg | 3 cargo spaces, 15 fuel, 6 kWh |
| Cargo-focused | More cargo space | $106M | 9,920 / 10,950 kg | 6 cargo spaces, 15 fuel, 6 kWh |
| Long-range | More fuel and battery | $107M | 10,220 / 11,250 kg | 3 cargo spaces, 30 fuel, 12 kWh |
| High-power | Laser-equipped ship | $114M | 10,270 / 11,130 kg | 4 cargo spaces, 15 fuel, 8 kW output |

EarthReadyBuild defines stable IDs and returns independent loadout copies. EarthGameplaySession.LoadReadyBuild saves the selection and clears previous checks when the loadout changes. Selecting the already-fitted model leaves its checks intact. Customizing a model changes its label to Custom Build. No save migration is necessary: model identity is inferred from the saved parts and cargo.

Review Launch requires valid hardware and locked cargo. Power testing and landing planning happen after launch on the in-flight screen. The old overweight demo remains available only as a developer test fixture; it is not offered as a ready-made ship.

## Controls and state

Drag to rotate, scroll to zoom, or use the rotate/zoom buttons. Presets and part buttons support standard UI navigation. No dragging parts or selecting attachment points is required.

The model reads the same EarthGameplaySession used by validation and reports. Busy/launched states lock edits. The preview studio and camera deactivate when leaving configuration and resume on return. Descriptions are modal and do not mutate selections.

## Descriptions and final artwork

Each hardware/cargo entry supports an optional plain-text description in Resources/FarSignalEarth.json. INFO uses that text; missing content says Description coming soon. Copy is still awaiting the user's files.

The supplied 30 Blender models are preserved in SourceArt/EarthRocketParts with their Parts-Index.csv mapping. `Tools/Blender/export_earth_parts.py` creates centered, upright, triangulated FBX assets in Resources/EarthParts/<partId>.fbx; all 24 hardware choices and six cargo props are imported. The runtime loads them directly and keeps generated shapes only as a missing-asset fallback. Click targets are fitted to the imported renderer bounds, independent of artistic colliders.

The preview uses layer 31, an isolated studio at x=10000, a RenderTexture and a camera. Reserve layer 31 for this view. Generated objects, texture and materials are released with the screen. A standalone build needs the preview shader/material included in build assets and its own visual test; current verification is Editor Play Mode.
