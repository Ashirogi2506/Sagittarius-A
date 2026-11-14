using System;

namespace SagittariusA.Gameplay.Data
{
    [Serializable]
    public sealed class MissionResourceTotals
    {
        public float costCredits;
        public float massKilograms;
        public float powerSupplyWatts;
        public float powerDemandWatts;
        public float dataGenerationMegabitsPerSecond;
        public float dataStorageGigabits;
        public float communicationsCapacityMegabitsPerSecond;
        public float missionDurationDays;
        public float scientificValuePoints;
        public float riskPercent;

        public MissionResourceTotals Copy()
        {
            return (MissionResourceTotals)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class MissionResourceLimits
    {
        public float budgetCredits;
        public float maximumMissionDurationDays;
        public float maximumRiskPercent;
        public float requiredScientificValuePoints;
    }
}
