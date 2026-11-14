# Earth launch checklist - proposed integration

`EarthLaunchSimulator.Validate` remains a resource/design preview only; it does not check locks, the completed power test, or arrival time. Do not launch the campaign from that preview. For the pre-launch checklist use `EarthLaunchChecklist.Validate(scenario, design, cargoSnapshot, powerSnapshot, landingPlan)` and show every returned issue. Only a report with `CanLaunch == true` has passed these checks. The campaign controller still owns saving and the actual launch transition.

```csharp
var power = new EarthPowerTest(design.powerSystemId, selectedPower.maxPowerKw, laserLoaded);
power.SetCabin(true);
power.SetRadio(true);
if (laserLoaded) power.TryLaser(); // optional: an overload trips only the laser
var plan = new LandingPlan { padId = design.landingPadId, hasArrivalMinute = true, arrivalMinute = 3 };
var report = new EarthLaunchChecklist().Validate(scenario, design, packing.Snapshot(), power.Snapshot(), plan);
```

The example variables and surrounding game adapters must be supplied by the team. When the player changes power system, create a new `EarthPowerTest` so the test starts again. Changing bay requires the game to create a fresh packing state and repack. Cargo placements in the design and packing snapshot must match; locks are checked for all loaded items. The team-owned UI must persist the necessary test, lock and landing state via the agreed save system. This code does not perform a launch, spend fuel, or save state.

The campaign brief specifies a recoverable warning for an unaffordable lunar descent, but the older `CheckLandingFuel` method currently signals a restart. These conflict with earlier user instructions. Do not wire that method into the final lunar descent until the team confirms the intended behavior and the authoritative save/transaction API. The checklist itself does not deduct fuel.

Draft gameplay values are 2 kW cabin, 1 kW radio, 3 kW laser, a 0-10 minute planning range and a warning at 100 kg or less payload margin. Pad daylight windows and fuel prices come from the mission data. These are game values, not official engineering specifications. The code and Unity Edit Mode tests are not yet verified in an actual Unity project.
