# Earth assembly prototype

Open the garage and choose **Open 3D Assembly**. Pick a category and part, then click the matching orange attachment point. **Fit to Matching Point** provides the same action for keyboard/controller navigation. Drag to rotate and scroll to zoom, or use the rotate/zoom buttons.

The default saved ship is already fitted. This increment supports replacing its six hardware groups and adding/removing cargo; it is not an empty-frame construction sequence. Cargo appears beside the bay in this exploded assembly view. Packing and locking it remains the cargo minigame's responsibility.

## Live state

Both the list garage and 3D assembly use EarthGameplaySession. Fitting updates that state, saves it and publishes Changed; active totals refresh before the next rendered frame. Browsing candidates, opening descriptions and rotating the model never change the build or its totals. Repeatedly fitting the same cargo is idempotent. Remove is a separate action.

The totals reflect fitted parts, not preview candidates. A socket enforces the selected category. The existing simulation adapter still evaluates nozzle mount, cover clearance, mass, budget and other constraints; incompatible configurations show warnings and block launch. The temporary model's dimensions are illustrative and do not perform physical collision/clearance validation.

## Description files

Each hardware and cargo entry supports an optional plain-text **description** field in Resources/FarSignalEarth.json. INFO opens the same description in both views. Missing copy explicitly shows “Description coming soon.” Supply the approved prose keyed by the existing IDs (l1–l4, n1–n4, f1–f4, e1–e4, c1–c4, p1–p4, panel, kit, laser, battery, spares, fuel). The UI scrolls long descriptions and does not parse markup.

## Replace prototype geometry

The current shapes are an interactive blockout, not final spacecraft art. Hardware prefabs can be supplied under Resources/EarthParts/<partId>.prefab. EarthAssemblyView loads them by catalog ID. Use a local attachment origin, upright +Y and neutral local transform. Keep scripts, cameras and lights out of these visual prefabs. Materials should match the project's rendering pipeline.

Current attachment positions and click targets are schematic and defined in EarthAssemblyView. Tune them to the delivered models; custom-model bounds and attachment markers still need an art integration pass. The dedicated preview uses layer 31, an isolated studio at x=10000, a RenderTexture and a camera. Do not use layer 31 for unrelated gameplay geometry. The camera, shapes, render texture and temporary materials are released when the view closes.

For a player build, include the chosen preview shader/material in build assets (Standard for the built-in pipeline, Universal Render Pipeline/Lit for URP). The preview chooses based on the active pipeline, including quality overrides. Final prefabs should carry explicit material references. This increment is verified in Editor Play Mode; standalone build validation remains part of the art integration pass.
