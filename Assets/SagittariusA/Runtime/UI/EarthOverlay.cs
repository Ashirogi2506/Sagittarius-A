using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.UI
{
    public sealed class EarthOverlay : MonoBehaviour
    {
        private CanvasGroup owner;
        private bool previousInteraction, previousRaycasts;
        private GameObject previousSelection;
        public static EarthOverlay Open(Transform parent, string title)
        {
            var rect = EarthGarageView.Box(parent.parent, title, 0, 0, 1, 1, new Color(.025f, .035f, .055f, 1));
            var overlay = rect.gameObject.AddComponent<EarthOverlay>();
            overlay.owner = parent.GetComponent<CanvasGroup>();
            if (overlay.owner == null) overlay.owner = parent.gameObject.AddComponent<CanvasGroup>();
            overlay.previousInteraction = overlay.owner.interactable;
            overlay.previousRaycasts = overlay.owner.blocksRaycasts;
            overlay.previousSelection = EventSystem.current?.currentSelectedGameObject;
            overlay.owner.interactable = false; overlay.owner.blocksRaycasts = false;
            return overlay;
        }
        private void Update()
        {
            // Nested descriptions own Escape until closed.
            var ownGroup = GetComponent<CanvasGroup>();
            if ((ownGroup == null || ownGroup.interactable) && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
        private void OnDestroy()
        {
            if (owner != null) { owner.interactable = previousInteraction; owner.blocksRaycasts = previousRaycasts; }
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
        }
    }
}
