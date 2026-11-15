using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Mars;
using UnityEngine;

namespace SagittariusA.Gameplay.Ganymede
{
    public static class GanymedeCampaignBridge
    {
        // Resume only from this expedition's completed Moon handoff. A previous
        // commander's standalone or old campaign draft cannot hijack Continue.
        public static bool TryContinue(GameObject owner)
        {
            var handoff = owner.GetComponent<MoonStageController>()?.GetMarsHandoff();
            if (handoff == null) return false;
            var mars = owner.GetComponent<MarsStageController>() ?? owner.AddComponent<MarsStageController>();
            if (!mars.Initialize(handoff)) return false;
            var finalHandoff = mars.GetGanymedeHandoff();
            if (finalHandoff != null)
            {
                var finale = owner.GetComponent<GanymedePlayableExperience>() ?? owner.AddComponent<GanymedePlayableExperience>();
                finale.StartCampaign(finalHandoff);
            }
            else
            {
                var experience = owner.GetComponent<MarsPlayableExperience>() ?? owner.AddComponent<MarsPlayableExperience>();
                experience.StartCampaign();
            }
            return true;
        }
    }
}
