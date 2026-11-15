# Lunar descent: provisional simulation contract

`LunarDescent.Preview` checks a chosen pad, time and the current remaining fuel without changing any values. Pad cost and light windows come from the Earth scenario JSON; the allowed selection range of 0-10 minutes is a draft rule in the campaign brief. Insufficient fuel returns `RestartDestination.PreLaunchCheckpoint` as the user explicitly requested. This differs from the campaign brief's retry-another-pad instruction and must be communicated to the campaign owner. A shadowed plan returns a warning; the caller may choose a new plan before confirmation.

`Confirm(scenario, state, padId, arrivalMinute, operationId)` returns a new `LunarDescentState` with the fuel deducted when validation succeeds. The input object is unchanged. Retrying the same operation against the returned, saved state reports `AlreadyCommitted` without another deduction. A different operation after confirmation reports `OperationConflict`. A failure has no new committed state; the campaign controller owns the pre-launch respawn.

This class does **not** write a save, execute a landing, change scenes, or make concurrent writes safe. The authoritative campaign save owner must atomically persist the returned state and unique operation ID together, reject stale/concurrent versions, and use the saved state on reload. Reusing an older pre-confirmation state cannot detect a previous confirmation. Until the authoritative contract is agreed and integrated, this is a provisional local calculation only.

```csharp
var preview = descent.Preview(scenario, savedState, padId, arrivalMinute);
// Display preview.message and allow changing the plan before confirmation.
var result = descent.Confirm(scenario, savedState, padId, arrivalMinute, operationId);
if (result.status == DescentStatus.Committed) SaveAtomicallyThroughCampaignOwner(result.nextState);
// Only the campaign controller can advance after the save succeeds.
```

The save call and variables above are integration placeholders, not implemented APIs. Tests cover a 12-unit Ridge failure, a 10-unit Beacon deduction, duplicate requests, invalid inputs and daylight boundaries; Unity tests have not yet been executed in the actual team project.
