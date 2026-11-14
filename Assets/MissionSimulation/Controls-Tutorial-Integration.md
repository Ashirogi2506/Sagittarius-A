# Far Signal opening controls tutorial

This module defines a proposed sequence and tracks successful gameplay actions. It does not implement player movement, key bindings, visuals, opening a scene, persistence, or actual interactions. Integrate it with the gameplay/UI owner before presenting it to players. The order and actions are editable in `Resources/FarSignal.ControlsTutorial.json`; the team must confirm which interactions actually exist and can occur at the beginning of the game.

Use the game's authoritative input system for both input handling and the displayed control labels. No physical key is hard-coded. If a binding is absent, `Current().bindingMissing` is true and the visible label says `Control not configured`; don't treat it as a playable tutorial until all actions have real bindings.

```csharp
using UnityEngine;
using MissionSimulation.Tutorial;

var json = Resources.Load<TextAsset>("FarSignal.ControlsTutorial");
var data = JsonUtility.FromJson<TutorialDefinition>(json.text);
// Build these labels from the actual, current input bindings.
ControlLabel[] labelsFromInputSystem = GetLabelsFromInputSystem();
var tutorial = new ControlsTutorial(data, labelsFromInputSystem);
tutorial.Start(); // Call when a new game begins, at the agreed introduction point.
TutorialView view = tutorial.Current(); // UI shows title, instruction, step number and count.
// After the gameplay system confirms the current action actually happened:
tutorial.ReportAction("move");
view = tutorial.Current();
// When input bindings are changed, rebuild the entire label list and refresh the display:
tutorial.SetBindings(GetLabelsFromInputSystem());
// Check tutorial.State for Running, Completed or Skipped.
```

`GetLabelsFromInputSystem()` is a placeholder for the team's input adapter, not an included method. The gameplay owner calls `ReportAction` with the exact `actionId` only after the corresponding action succeeds. Merely pressing a key does not establish that the item was picked up, used, or opened. Failed actions and actions out of sequence do not advance the tutorial. A game may expose `Skip()` if the team wants that behavior. Calling `Start()` begins from the first step; save/resume and when the story starts are owned by the game controller.

The steps `move`, `look`, `interact`, `pickup`, `inventory`, `use`, and `break` are draft integration identifiers, not key assignments or proof that these game mechanics exist. The team can change IDs, labels, wording and sequence in coordination with the input and gameplay owners. The `break` step also describes manual opening only where the game supports it; the precise manual repair interaction remains undecided.
