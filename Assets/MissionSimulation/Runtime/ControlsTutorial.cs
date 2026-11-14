using System;
using System.Collections.Generic;

namespace MissionSimulation.Tutorial
{
    // Labels and actions are supplied by the game's input owner; this does not bind keys.
    [Serializable] public class ControlLabel { public string actionId, displayLabel; }
    [Serializable] public class TutorialStep { public string id, title, instruction, actionId; }
    [Serializable] public class TutorialDefinition { public string id; public TutorialStep[] steps; }
    [Serializable] public class TutorialView
    {
        public string stepId, title, instruction, actionId, displayLabel;
        public int stepNumber, stepCount;
        public bool bindingMissing;
    }
    public enum TutorialState { NotStarted, Running, Completed, Skipped }

    // The gameplay controller signals successfully completed actions, not raw key presses.
    public sealed class ControlsTutorial
    {
        readonly TutorialDefinition definition;
        readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);
        int index;
        public TutorialState State { get; private set; }
        public ControlsTutorial(TutorialDefinition definition, IEnumerable<ControlLabel> bindings)
        {
            if (definition == null || definition.steps == null || definition.steps.Length == 0 || string.IsNullOrWhiteSpace(definition.id)) throw new ArgumentException("Tutorial definition is incomplete.", "definition");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in definition.steps)
                if (step == null || string.IsNullOrWhiteSpace(step.id) || string.IsNullOrWhiteSpace(step.title) || string.IsNullOrWhiteSpace(step.instruction) || string.IsNullOrWhiteSpace(step.actionId) || !ids.Add(step.id)) throw new ArgumentException("Tutorial steps need unique IDs and valid text and actions.", "definition");
            this.definition = definition;
            SetBindings(bindings);
            State = TutorialState.NotStarted;
        }
        public void SetBindings(IEnumerable<ControlLabel> bindings)
        {
            // Validate first so an invalid replacement cannot erase the current labels.
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            if (bindings != null) foreach (var b in bindings)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.actionId) || string.IsNullOrWhiteSpace(b.displayLabel) || next.ContainsKey(b.actionId)) throw new ArgumentException("Binding labels must have unique action IDs and nonempty labels.", "bindings");
                next.Add(b.actionId, b.displayLabel);
            }
            labels.Clear(); foreach (var pair in next) labels.Add(pair.Key, pair.Value);
        }
        public void Start() { index = 0; State = TutorialState.Running; }
        public TutorialView Current()
        {
            if (State != TutorialState.Running) return null;
            var step = definition.steps[index]; string label;
            bool found = labels.TryGetValue(step.actionId, out label);
            if (!found) label = "Control not configured";
            return new TutorialView { stepId = step.id, title = step.title, instruction = step.instruction.Replace("{control}", label), actionId = step.actionId, displayLabel = label, bindingMissing = !found, stepNumber = index + 1, stepCount = definition.steps.Length };
        }
        public bool ReportAction(string actionId)
        {
            if (State != TutorialState.Running || !string.Equals(definition.steps[index].actionId, actionId, StringComparison.Ordinal)) return false;
            index++;
            if (index == definition.steps.Length) State = TutorialState.Completed;
            return true;
        }
        public void Skip() { if (State == TutorialState.Running) State = TutorialState.Skipped; }
    }
}
