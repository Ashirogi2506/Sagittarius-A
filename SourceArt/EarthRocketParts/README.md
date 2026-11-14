# Earth rocket part sources

This folder contains the 30 Blender source models supplied in `FarSignal-Earth-Rocket-Assembly-Parts.zip` on 14 November 2026. `Parts-Index.csv` is the authoritative mapping from catalog ID to source object and filename.

These files intentionally live outside `Assets/`: Unity imports `.blend` through an installed Blender executable, and Blender is not currently installed on this workstation. Keeping the sources here prevents broken Unity imports.

## Import target

Export one FBX per row in `Parts-Index.csv`, with transforms applied and the mapped source object at the local origin. Create visual-only prefabs at:

`Assets/SagittariusA/Resources/EarthParts/<ID>.prefab`

Use the lowercase catalog ID (`l1`, `n2`, `panel`, and so on). The runtime already loads that path and falls back to blockout geometry when no prefab exists. Prefabs must use upright +Y, a neutral local transform, compatible materials, and no scripts, cameras, or lights.
