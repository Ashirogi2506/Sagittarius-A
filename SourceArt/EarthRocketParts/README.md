# Earth rocket part sources

This folder contains the 30 Blender source models supplied in `FarSignal-Earth-Rocket-Assembly-Parts.zip` on 14 November 2026. `Parts-Index.csv` is the authoritative mapping from catalog ID to source object and filename.

These files intentionally live outside `Assets/`. Keeping authoring sources separate prevents Unity from invoking Blender during every asset refresh and makes the committed FBX output deterministic.

## Import target

The reproducible Blender exporter is `Tools/Blender/export_earth_parts.py`. It recenters every model, preserves relative variant sizes, scales hardware for the exploded ship preview, and keeps cargo readable. It writes one FBX per row to:

`Assets/SagittariusA/Resources/EarthParts/<ID>.fbx`

Use the lowercase catalog ID (`l1`, `n2`, `panel`, and so on). The runtime loads that path directly and falls back to blockout geometry when an asset is missing. Exported assets are upright +Y, centered at the attachment point, and contain meshes/materials only.
