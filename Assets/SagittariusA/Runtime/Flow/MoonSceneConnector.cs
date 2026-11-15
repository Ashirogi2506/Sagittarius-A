using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace SagittariusA.Gameplay.Flow
{
    // Scene-owned adapter. The scene loader passes in the existing campaign controller.
    public sealed class MoonSceneConnector : MonoBehaviour, IMoonInteractionDriver
    {
        [Serializable] public sealed class ContextEvent : UnityEvent { }
        [SerializeField] private ContextEvent onContextChanged = new ContextEvent();
        private MoonStageController controller;
        private MoonStageContext context;
        private Action<MoonInteractionResult> report;
        private bool paused;

        public MoonStageContext Context { get { return context; } }
        public string Notice { get { return controller != null ? controller.Notice : "Moon controller not connected."; } }

        public void Bind(MoonStageController value)
        {
            if (controller == value) return;
            if (controller != null) controller.UnregisterDriver(this);
            controller = value;
            if (controller != null) controller.RegisterDriver(this);
        }

        public void Initialize(MoonStageContext value, Action<MoonInteractionResult> callback)
        { context = value; report = callback; paused = false; onContextChanged.Invoke(); }
        public void UpdateContext(MoonStageContext value)
        { context = value; onContextChanged.Invoke(); }
        public void Pause() { paused = true; EndHold(); }
        public void Resume() { paused = false; }
        public void Shutdown() { EndHold(); context = null; report = null; }

        public bool IsAvailable(string interactionId)
        {
            return !paused && context != null && context.availableInteractionIds != null &&
                context.availableInteractionIds.Contains(interactionId);
        }

        // Use for a completed tap/click interaction. Patch IDs require HoldPatch instead.
        public void Interact(string interactionId)
        {
            if (IsPatch(interactionId) || !IsAvailable(interactionId) || report == null) return;
            report(new MoonInteractionResult { runId = context.runId, interactionId = interactionId, completed = true });
        }

        // Called once per frame while holding on one patch; elapsedSeconds is frame time.
        public void HoldPatch(string interactionId, float elapsedSeconds)
        {
            if (!IsPatch(interactionId) || !IsAvailable(interactionId) || report == null ||
                float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0) return;
            report(new MoonInteractionResult { runId = context.runId, interactionId = interactionId,
                elapsedSeconds = elapsedSeconds, completed = true });
        }

        public void EndHold() { if (controller != null) controller.EndPatchHold(); }
        private static bool IsPatch(string id)
        { return id == MoonInteractionIds.CleanPatch1 || id == MoonInteractionIds.CleanPatch2 || id == MoonInteractionIds.CleanPatch3; }
        private void OnDestroy() { if (controller != null) controller.UnregisterDriver(this); }
    }
}
