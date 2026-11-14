using UnityEngine;

namespace SagittariusA.Gameplay.Data
{
    [CreateAssetMenu(menuName = "Far Signal/Mission/Mission Definition", fileName = "Mission_")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(3, 8)] [SerializeField] private string briefing;
        [TextArea(2, 5)] [SerializeField] private string objective;
        [SerializeField] private MissionResourceLimits limits = new MissionResourceLimits();
        [SerializeField] private MissionComponentDefinition[] availableComponents;
        [SerializeField] private OrbitOptionDefinition[] availableOrbits;
        [SerializeField] private LauncherOptionDefinition[] availableLaunchers;

        public string Id => id;
        public string DisplayName => displayName;
        public string Briefing => briefing;
        public string Objective => objective;
        public MissionResourceLimits Limits => limits;
        public MissionComponentDefinition[] AvailableComponents => availableComponents;
        public OrbitOptionDefinition[] AvailableOrbits => availableOrbits;
        public LauncherOptionDefinition[] AvailableLaunchers => availableLaunchers;
    }
}
