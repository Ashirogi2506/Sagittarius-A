using System;
using MissionSimulation.Player;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Minigames;
using UnityEngine;

namespace SagittariusA.Gameplay.Minigames
{
    // Bridge between the existing game (Assembly-CSharp) and the minigame framework (SagittariusA.Minigames assembly).
    // - Registers the project's 3D input behaviours so they are disabled while a minigame is open.
    // - Offers one call for mission events: MinigameGameFlowAdapter.Run(context, onCompleted).
    // - Forwards every result to the simulation adapter (placeholder until the simulator API exists).
    public static class MinigameGameFlowAdapter
    {
        public static IMinigameSimulationAdapter Simulation { get; set; } = new PendingEffectsSimulationAdapter();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterInputBlockers()
        {
            // These read Keyboard/Mouse directly every frame, so they are switched off while a minigame is open.
            MinigameInputLock.RegisterBlockerType<MoonFirstPersonController>();
            MinigameInputLock.RegisterBlockerType<PlayerController>();
        }

        // Start a minigame from a mission event. The simulation adapter receives the result before the caller does.
        public static void Run(MinigameContext context, Action<MinigameResult> onCompleted = null)
        {
            MinigameManager.Instance.StartMinigame(context, result =>
            {
                try { Simulation?.ApplySuggestedEffects(result); } catch (Exception e) { Debug.LogException(e); }
                onCompleted?.Invoke(result);
            });
        }

        // Convenience for the four mission tasks with their documented IDs.
        public static void Run(string minigameId, string missionId, string eventId, int difficulty, int seed, Action<MinigameResult> onCompleted = null) =>
            Run(new MinigameContext { MinigameId = minigameId, MissionId = missionId, EventId = eventId, DifficultyLevel = difficulty, RandomSeed = seed, AllowCancel = true }, onCompleted);
    }
}
