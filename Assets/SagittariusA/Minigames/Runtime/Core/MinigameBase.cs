using System;
using SagittariusA.Minigames.UI;
using UnityEngine;

namespace SagittariusA.Minigames
{
    // Base for the UI minigames. Handles the IMinigame lifecycle, time limit, shell wiring and the single Completed event.
    // Subclasses add [Minigame("id")], a public static Definition property, and implement the abstract members.
    public abstract class MinigameBase : MonoBehaviour, IMinigame
    {
        public abstract string Id { get; }
        public bool IsRunning { get; private set; }
        public event Action<MinigameResult> Completed;

        protected MinigameContext Context { get; private set; }
        protected MinigameDefinition ActiveDefinition { get; private set; }
        protected MinigameShellView Shell { get; private set; }
        protected float TimeLimit { get; private set; }
        private bool reported;

        // Called by MinigameManager before Initialize.
        public void Attach(MinigameShellView shell, MinigameDefinition definition)
        {
            Shell = shell; ActiveDefinition = definition;
            shell.RestartRequested += () => { if (IsRunning) { OnRestart(); Feedback("Task restarted. The clock keeps running.", FeedbackKind.Info); } };
        }

        public void Initialize(MinigameContext context)
        {
            if (Shell == null) throw new InvalidOperationException($"{GetType().Name} needs a MinigameShellView (start it through MinigameManager).");
            Context = context;
            TimeLimit = MinigameSettings.ResolveTimeLimit(context, ActiveDefinition);
            OnInitialize();
        }

        public void Begin()
        {
            BuildContent(Shell.Content);
            IsRunning = true;
            UpdateTimer();
            Refresh();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return;
            if (OnTick(unscaledDeltaTime)) { Finish(); return; }
            UpdateTimer();
        }

        public void Cancel() { IsRunning = false; OnCancelled(); }

        // Builds the result from the rules and reports it once.
        protected void Finish()
        {
            if (reported) return;
            reported = true; IsRunning = false;
            Refresh();
            var result = BuildResult();
            Completed?.Invoke(result);
        }

        private void UpdateTimer() { if (TimeLimit > 0) Shell.SetTimer(TimeLimit - Elapsed, TimeLimit); }

        protected void Feedback(string message, FeedbackKind kind)
        {
            Shell.ShowFeedback(message, kind);
            if (kind == FeedbackKind.Good) MinigameAudio.Play(MinigameCue.Valid);
            else if (kind == FeedbackKind.Bad) MinigameAudio.Play(MinigameCue.Invalid);
        }

        protected abstract float Elapsed { get; }
        protected abstract void OnInitialize();
        protected abstract void BuildContent(RectTransform content);
        // Advance rules by unscaled time. Return true when the task has ended (time-out or natural end).
        protected abstract bool OnTick(float dt);
        protected abstract void Refresh();
        protected abstract MinigameResult BuildResult();
        protected virtual void OnRestart() { }
        protected virtual void OnCancelled() { }
    }
}
