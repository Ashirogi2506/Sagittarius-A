using System.Collections.Generic;
using SagittariusA.Minigames;
using UnityEngine;

namespace SagittariusA.Gameplay.Minigames
{
    // Safe placeholder until the mission simulator exposes an effect API.
    // It logs each result and keeps the requested effects per mission so the simulator can read them later
    // (PendingEffects / TakePendingEffects). It never changes engineering values.
    public sealed class PendingEffectsSimulationAdapter : IMinigameSimulationAdapter
    {
        private readonly Dictionary<string, List<MinigameEffect>> pending = new Dictionary<string, List<MinigameEffect>>();

        public void ApplySuggestedEffects(MinigameResult result)
        {
            if (result == null) return;
            Debug.Log($"[Minigames] {result.MinigameId} ({result.MissionId}/{result.EventId}): {result.Outcome}, score {result.Score}, " +
                      $"{result.SuggestedEffects.Count} suggested effect(s).");
            if (result.Outcome == MinigameOutcome.Cancelled || result.SuggestedEffects.Count == 0) return;
            string mission = result.MissionId ?? "unknown";
            if (!pending.TryGetValue(mission, out var list)) pending[mission] = list = new List<MinigameEffect>();
            list.AddRange(result.SuggestedEffects);
        }

        public IReadOnlyList<MinigameEffect> PendingEffects(string missionId) =>
            pending.TryGetValue(missionId ?? "", out var list) ? list : (IReadOnlyList<MinigameEffect>)new MinigameEffect[0];

        // The simulator calls this when it applies the effects, so they are applied once.
        public List<MinigameEffect> TakePendingEffects(string missionId)
        {
            if (!pending.TryGetValue(missionId ?? "", out var list)) return new List<MinigameEffect>();
            pending.Remove(missionId);
            return list;
        }
    }
}
