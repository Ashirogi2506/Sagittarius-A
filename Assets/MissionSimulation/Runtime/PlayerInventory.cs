using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionSimulation.Player
{
    // Stores IDs only. Item effects and the inventory display belong to gameplay/UI.
    public sealed class PlayerInventory : MonoBehaviour
    {
        readonly List<string> items = new List<string>();
        public event Action Changed;
        public int Count { get { return items.Count; } }
        public string GetItem(int index) { return index >= 0 && index < items.Count ? items[index] : null; }
        public bool TryAdd(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            items.Add(itemId); if (Changed != null) Changed(); return true;
        }
        public bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= items.Count) return false;
            items.RemoveAt(index); if (Changed != null) Changed(); return true;
        }
    }
}
