# Pre-launch checkpoint handoff

`PreLaunchCheckpointService.Create` validates the full Earth launch checklist and prepares a deep copy of the run ID, mission ID, commander identity, ship selections, packed cargo/locks, power test, landing plan, fuel and battery at launch. A ready design with warnings may produce a checkpoint. The save owner must persist the entire result before allowing launch; the returned object is not automatically saved.

`Restore(runId, scenario, saved)` verifies the run, mission, schema version, checklist, recorded starting fuel and battery, and a deterministic fingerprint of scenario values used by the Earth checks. The fingerprint is stable when scenario arrays are reordered, and excludes labels and dialogue. If validation fails, the UI should surface the error and request a compatible save/scenario, not create a new run silently. The checkpoint is distinct from a landing transaction: insufficient fuel reports `RestartDestination.PreLaunchCheckpoint`, and the campaign owner decides when and how to restore it. This module never loads a scene or changes shared game state.

```csharp
var prepared = checkpointService.Create(runId, commander, scenario, design, cargo, power, landingPlan);
if (prepared.success) SavePreLaunchThroughCampaignOwner(prepared.checkpoint);
// Later, after a failed landing:
var restored = checkpointService.Restore(runId, scenario, savedPreLaunchCheckpoint);
if (restored.success) RestoreThroughCampaignOwner(restored.checkpoint);
```

Save/restore calls are illustrative integration placeholders. Actual file format, save ownership, atomicity, concurrency and committed campaign transitions are not implemented here. No unverified assumption is made about extra checkpoints, current currency outside this mission, or other campaign state. Checkpoint schema version 2 adds a mission-data fingerprint; version 1 saves are rejected, not silently upgraded. The campaign team must decide how to migrate existing saves and define how authored rule changes beyond these data fields affect saves. Unity tests have not yet been run in the team's actual project.
