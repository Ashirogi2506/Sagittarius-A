# Pre-launch checkpoint handoff

`PreLaunchCheckpointService.Create` validates the full Earth launch checklist and prepares a deep copy of the run ID, mission ID, commander identity, ship selections, packed cargo/locks, power test, landing plan, fuel and battery at launch. A ready design with warnings may produce a checkpoint. The save owner must persist the entire result before allowing launch; the returned object is not automatically saved.

`Restore(runId, scenario, saved)` verifies the run, mission, schema version, checklist, and the recorded starting fuel and battery against current scenario data, then returns a separate copy for the campaign controller. If validation fails, the UI should surface the error and request a compatible save/scenario, not create a new run silently. The checkpoint is distinct from a landing transaction: insufficient fuel reports `RestartDestination.PreLaunchCheckpoint`, and the campaign owner decides when and how to restore it. This module never loads a scene or changes shared game state.

```csharp
var prepared = checkpointService.Create(runId, commander, scenario, design, cargo, power, landingPlan);
if (prepared.success) SavePreLaunchThroughCampaignOwner(prepared.checkpoint);
// Later, after a failed landing:
var restored = checkpointService.Restore(runId, scenario, savedPreLaunchCheckpoint);
if (restored.success) RestoreThroughCampaignOwner(restored.checkpoint);
```

Save/restore calls are illustrative integration placeholders. Actual file format, save ownership, atomicity, concurrency and committed campaign transitions are not implemented here. No unverified assumption is made about extra checkpoints, current currency outside this mission, or other campaign state. If the scenario changes after a save, the service intentionally refuses an inconsistent fuel/battery restore instead of silently inventing migration behavior. The campaign team must define versioning/migration and validate other changed scenario values before release. Unity tests have not yet been run in the team's actual project.
