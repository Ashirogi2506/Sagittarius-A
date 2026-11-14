using UnityEngine;

namespace SagittariusA.Gameplay.Data
{
    [CreateAssetMenu(menuName = "Sagittarius A/Mission/Orbit Option", fileName = "Orbit_")]
    public sealed class OrbitOptionDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [Min(0f)] [SerializeField] private float altitudeKilometers;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public float AltitudeKilometers => altitudeKilometers;
    }

    [CreateAssetMenu(menuName = "Sagittarius A/Mission/Launcher Option", fileName = "Launcher_")]
    public sealed class LauncherOptionDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [Min(0f)] [SerializeField] private float cost;
        [Min(0f)] [SerializeField] private float payloadCapacityKilograms;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public float Cost => cost;
        public float PayloadCapacityKilograms => payloadCapacityKilograms;
    }
}
