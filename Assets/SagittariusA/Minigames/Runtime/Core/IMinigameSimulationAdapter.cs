namespace SagittariusA.Minigames
{
    // Integration point owned by the mission-simulation programmer.
    // Receives requested effects; decides how (and whether) they change mass, power, data, reliability and so on.
    // Minigames never modify simulation values directly.
    public interface IMinigameSimulationAdapter
    {
        void ApplySuggestedEffects(MinigameResult result);
    }
}
