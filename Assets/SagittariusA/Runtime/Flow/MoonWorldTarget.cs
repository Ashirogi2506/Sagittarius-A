using UnityEngine;
using System.Linq;

namespace SagittariusA.Gameplay.Flow
{
    // Add to a collider or its parent; gameplay validates the requested action.
    public sealed class MoonWorldTarget : MonoBehaviour
    {
        [SerializeField] private string interactionId;
        [SerializeField] private bool requiresHold;
        [SerializeField] private string prompt;
        private string[] choices;
        public string InteractionId { get { return interactionId; } }
        public bool RequiresHold { get { return requiresHold; } }
        public string Prompt { get { return prompt; } }
        public void Configure(string description, bool hold, params string[] ids)
        { prompt = description; requiresHold = hold; choices = ids; interactionId = ids != null && ids.Length > 0 ? ids[0] : null; }
        public string AvailableId(MoonSceneConnector connector)
        {
            if (connector == null) return null;
            return choices == null || choices.Length == 0
                ? (connector.IsAvailable(interactionId) ? interactionId : null)
                : choices.FirstOrDefault(connector.IsAvailable);
        }
    }
}
