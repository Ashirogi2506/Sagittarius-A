using UnityEngine;
using UnityEngine.Events;

namespace MissionSimulation.Player
{
    // Scene owner configures the responses; an object does not disappear or break by default.
    public sealed class PlayerWorldTarget : MonoBehaviour
    {
        public string itemId;
        public bool canPickUp, canInteract, canBreakOpen;
        public UnityEvent onInteract, onPickup, onBreakOpen;
        public bool TryInteract()
        {
            if (!canInteract || onInteract == null || onInteract.GetPersistentEventCount() == 0) return false;
            onInteract.Invoke(); return true;
        }
        public bool TryBreakOpen()
        {
            if (!canBreakOpen || onBreakOpen == null || onBreakOpen.GetPersistentEventCount() == 0) return false;
            onBreakOpen.Invoke(); return true;
        }
        // The configured callback is responsible for updating/removing the scene object.
        public bool TryPickup(PlayerInventory bag)
        {
            if (!canPickUp || bag == null || string.IsNullOrWhiteSpace(itemId) || onPickup == null || onPickup.GetPersistentEventCount() == 0) return false;
            if (!bag.TryAdd(itemId)) return false;
            canPickUp = false;
            onPickup.Invoke(); return true;
        }
    }
}
