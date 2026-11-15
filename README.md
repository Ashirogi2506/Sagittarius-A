# FAR SIGNAL

A Sagittarius-A team mission-design game: prepare a ship on Earth, restore the Moon's Pole-7 base, assemble and test an orbiter on Mars, and build the Ganymede stations that send the first signal home.

## Play on Windows

Download the Windows ZIP from [Releases](https://github.com/Ashirogi2506/Sagittarius-A/releases), extract the entire folder, then open `FarSignal.exe`. Keep `FarSignal_Data`, `UnityPlayer.dll`, and the other supplied folders beside the executable. Unity and Visual Studio are not required to play.

Choose **Commander Profile** to set the character and call sign. **Continue Expedition** opens the current campaign. **New Expedition** asks before replacing campaign progress; it preserves the profile, settings, and achievements.

| Action | Control |
| --- | --- |
| Walk / look | WASD / mouse |
| Jump | Space; gravity changes by world |
| Use / interact | Left mouse button or E |
| Pick up / place | Right mouse to pick up, left mouse to place |
| Release mouse | Escape |
| Return from a chapter | Tab / the on-screen menu |
| Moon task explanation | H |
| Settings | F1 |
| Earth assembly | Click a category or part; drag to rotate; scroll to zoom |
| Flight / module view | Assembly view button |

Follow each minigame's controls. Cargo packing is required before the first launch; power and landing planning happen in flight. The Moon cart follows the selected service route and allows mouse look during the ride. All three Ganymede stations are required before departure.

The release includes five skippable flight films, the team's commander portraits and ship models, an original ambient score, interface/movement sounds, saved audio/display/look settings, and eight persistent achievements. Collecting all eight displays Completionist.

## Open the Unity project

Use **Unity 6.6 / 6000.6.9f1** with Windows build support. Open this repository root in Unity Hub. Runtime assets are already exported; Blender is only needed to edit/re-export source art. Open `Assets/Scenes/Release.unity`, or use **Far Signal > Open Main Game Scene**.

Use **Far Signal > Build Windows Release** to export a Windows x64 Mono build to the sibling `outputs/FarSignal-Windows` folder. This menu compiles and exports only; it does not run tests. Keep `.meta` files with their assets and avoid committing generated `Library`, `Temp`, `Logs`, or build folders.

## Release status

**1.0.0-rc1 is a release candidate.** The source compiles and the Windows export is packaged for distribution. The final presentation changes have not undergone a complete runtime playthrough; runtime testing was stopped at the project owner's request. In particular, final ship placement, the Mars loader route, video playback, and UI layout need player review before declaring a fully tested stable release.

Mission numbers and the orbital-transfer narrative are game design values, not a flight-planning tool. Imported simulation/dialogue integration assets are retained for the team; the live campaign uses the gameplay stage controllers and task briefings.

Project tracking: [Tenger worshippers (Far signal)](https://github.com/users/Ashirogi2506/projects/1).
