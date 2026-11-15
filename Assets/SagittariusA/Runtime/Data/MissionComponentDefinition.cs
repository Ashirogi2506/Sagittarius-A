using UnityEngine;

namespace SagittariusA.Gameplay.Data
{
    public enum MissionComponentCategory
    {
        Instrument,
        Power,
        Communications,
        ThermalControl,
        Structure,
        Propulsion,
        Protection
    }

    [CreateAssetMenu(menuName = "Far Signal/Mission/Component", fileName = "Component_")]
    public sealed class MissionComponentDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private MissionComponentCategory category;
        [SerializeField] private Sprite icon;
        [SerializeField] private MissionResourceTotals resourceImpact = new MissionResourceTotals();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public MissionComponentCategory Category => category;
        public Sprite Icon => icon;
        public MissionResourceTotals ResourceImpact => resourceImpact;
    }
}
