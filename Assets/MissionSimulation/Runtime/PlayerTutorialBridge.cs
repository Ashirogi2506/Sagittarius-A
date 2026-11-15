using System;
using UnityEngine;
using UnityEngine.Events;
using MissionSimulation.Tutorial;

namespace MissionSimulation.Player
{
    [Serializable] public sealed class TutorialTextEvent : UnityEvent<string> { }

    // Add to an agreed startup object; UI is supplied by the gameplay/UI owner.
    public sealed class PlayerTutorialBridge : MonoBehaviour
    {
        public PlayerController player;
        public TextAsset tutorialJson;
        public TutorialTextEvent instructionChanged;
        public UnityEvent tutorialFinished;
        ControlsTutorial tutorial;
        void Start()
        {
            if (player == null || tutorialJson == null) { Debug.LogWarning("Tutorial requires a player and tutorial JSON.", this); enabled = false; return; }
            try
            {
                tutorial = new ControlsTutorial(JsonUtility.FromJson<TutorialDefinition>(tutorialJson.text), player.GetTutorialLabels());
                player.ActionCompleted += OnAction; tutorial.Start(); Publish();
            }
            catch (ArgumentException e) { Debug.LogWarning("Invalid tutorial: " + e.Message, this); enabled = false; }
        }
        void OnDestroy() { if (player != null) player.ActionCompleted -= OnAction; }
        void OnAction(string actionId)
        {
            if (tutorial == null || !tutorial.ReportAction(actionId)) return;
            if (tutorial.State == TutorialState.Completed) { if (tutorialFinished != null) tutorialFinished.Invoke(); }
            else Publish();
        }
        void Publish()
        {
            var view = tutorial.Current();
            if (view != null && instructionChanged != null) instructionChanged.Invoke(view.instruction);
        }
        // Call after changing bindings in the Inspector or in an options screen.
        public void RefreshBindingLabels()
        {
            if (tutorial == null || player == null) return;
            tutorial.SetBindings(player.GetTutorialLabels()); Publish();
        }
    }
}
