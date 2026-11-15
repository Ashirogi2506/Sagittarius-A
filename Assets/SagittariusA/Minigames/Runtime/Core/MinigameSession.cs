using System;

namespace SagittariusA.Minigames
{
    // Everything outside the minigame that must be saved and restored: pause, 3D input, cursor, UI selection.
    public interface IMinigameEnvironment
    {
        void Capture(MinigameDefinition definition);   // save state, pause world, lock 3D input
        void Restore();                                // must be safe to call after errors and scene changes
    }

    public interface IMinigameLog { void Error(string message); void Warning(string message); }

    // Plain C# lifecycle shared by MinigameManager and the tests:
    // one active minigame, exactly one result, environment always restored.
    public sealed class MinigameSession
    {
        private readonly IMinigameEnvironment environment;
        private readonly IMinigameLog log;
        private IMinigame game;
        private MinigameContext context;
        private Action<MinigameResult> callback;
        private bool captured;

        public bool IsActive { get; private set; }
        public IMinigame Current => game;
        public MinigameContext Context => context;
        public MinigameResult PendingResult { get; private set; }   // reported by the game, waiting for the result panel
        public event Action<MinigameResult> GameReportedResult;     // manager shows the result panel

        public MinigameSession(IMinigameEnvironment environment, IMinigameLog log = null)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.log = log;
        }

        // Returns false and immediately reports a Cancelled/Failure result if the minigame cannot start.
        public bool Start(MinigameContext requested, MinigameRegistry registry, Func<MinigameRegistry.Entry, IMinigame> create, Action<MinigameResult> onCompleted)
        {
            if (IsActive)
            {
                log?.Warning($"Minigame '{requested?.MinigameId}' requested while '{context?.MinigameId}' is active. Request rejected.");
                Deliver(onCompleted, MinigameResult.For(requested, MinigameOutcome.Cancelled, "Another minigame is already running.").Normalize());
                return false;
            }
            if (requested == null || registry == null || !registry.TryResolve(requested.MinigameId, out var entry))
            {
                log?.Error($"Unknown minigame ID '{requested?.MinigameId}'.");
                Deliver(onCompleted, MinigameResult.For(requested, MinigameOutcome.Cancelled, "Unknown minigame.").Normalize());
                return false;
            }
            IsActive = true; context = requested; callback = onCompleted; PendingResult = null;
            try
            {
                environment.Capture(entry.Definition); captured = true;
                game = create(entry) ?? throw new InvalidOperationException($"Minigame '{entry.Id}' could not be created.");
                game.Completed += OnGameCompleted;
                game.Initialize(requested);
                game.Begin();
                return true;
            }
            catch (Exception e)
            {
                log?.Error($"Minigame '{requested.MinigameId}' failed to start: {e}");
                Finish(MinigameResult.For(requested, MinigameOutcome.Cancelled, "The task could not start."));
                return false;
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || PendingResult != null || game == null || !game.IsRunning) return;
            try { game.Tick(unscaledDeltaTime); }
            catch (Exception e) { log?.Error($"Minigame '{context.MinigameId}' tick error: {e}"); Finish(MinigameResult.For(context, MinigameOutcome.Failure, "The task stopped because of an error.")); }
        }

        // Player cancel, scene change, or game flow abort. Delivers Cancelled immediately.
        public void Cancel(string reason = "Task cancelled.")
        {
            if (!IsActive) return;
            var pending = PendingResult;
            try { if (pending == null && game != null && game.IsRunning) game.Cancel(); }
            catch (Exception e) { log?.Error("Minigame cancel error: " + e); }
            // A game that reported during Cancel() wins only if it was already finished; otherwise Cancelled.
            Finish(pending ?? MinigameResult.For(context, MinigameOutcome.Cancelled, reason));
        }

        // Called after the result panel is dismissed (or directly when there is no panel).
        public void ConfirmResult() { if (IsActive && PendingResult != null) Finish(PendingResult); }

        private void OnGameCompleted(MinigameResult result)
        {
            if (!IsActive || PendingResult != null) return;   // exactly one result per run
            PendingResult = (result ?? MinigameResult.For(context, MinigameOutcome.Failure, "No result.")).Normalize();
            PendingResult.MinigameId = context.MinigameId; PendingResult.MissionId = context.MissionId; PendingResult.EventId = context.EventId;
            if (GameReportedResult != null) GameReportedResult(PendingResult); else ConfirmResult();
        }

        private void Finish(MinigameResult result)
        {
            if (!IsActive) return;
            IsActive = false;
            if (game != null) game.Completed -= OnGameCompleted;
            var cb = callback; callback = null; game = null; PendingResult = null;
            if (captured) { captured = false; try { environment.Restore(); } catch (Exception e) { log?.Error("Restoring game state failed: " + e); } }
            Deliver(cb, result.Normalize());
        }

        private void Deliver(Action<MinigameResult> cb, MinigameResult result)
        {
            try { cb?.Invoke(result); } catch (Exception e) { log?.Error("Minigame completion callback threw: " + e); }
        }
    }
}
