using System;
using System.Linq;
using SagittariusA.Minigames;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.UI;
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
        private static TaskBriefingView briefing;

        // Found by name so this folder also compiles in a project without the team's player scripts.
        private static readonly string[] InputBehaviourTypes = {
            "SagittariusA.Gameplay.Flow.MoonFirstPersonController",
            "MissionSimulation.Player.PlayerController",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterInputBlockers()
        {
            // These read Keyboard/Mouse directly every frame, so they are switched off while a minigame is open.
            foreach (var name in InputBehaviourTypes)
            {
                var type = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
                if (type == null || !typeof(Behaviour).IsAssignableFrom(type)) continue;
                MinigameInputLock.RegisterBlocker(() => UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Exclude).OfType<Behaviour>());
            }
        }

        // Start a minigame from a mission event. The simulation adapter receives the result before the caller does.
        public static void Run(MinigameContext context, Action<MinigameResult> onCompleted = null)
        {
            if (briefing != null)
            {
                onCompleted?.Invoke(MinigameResult.For(context,MinigameOutcome.Cancelled,"Another task explanation is open."));
                return;
            }
            var data=CampaignTaskBriefings.SharedPractice(context?.MinigameId);
            if(data==null){StartConfirmed(context,onCompleted);return;}
            briefing=TaskBriefingView.Open(null,data,accepted=>
            {
                briefing=null;
                if(accepted)StartConfirmed(context,onCompleted);
                else onCompleted?.Invoke(MinigameResult.For(context,MinigameOutcome.Cancelled,"Task not started."));
            });
        }

        private static void StartConfirmed(MinigameContext context, Action<MinigameResult> onCompleted)
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
