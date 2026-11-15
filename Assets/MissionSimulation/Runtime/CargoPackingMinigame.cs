using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionSimulation.Minigames
{
    [Serializable] public class PackedCargo { public string id; public int startSlot; public bool locked; }
    [Serializable] public class CargoPackingState { public string bayId; public int slotCount; public List<PackedCargo> items = new List<PackedCargo>(); }
    public sealed class CargoPackingMinigame
    {
        readonly CargoPackingState state;
        static readonly string[] Allowed = { "panel", "kit", "laser", "battery", "spares", "fuel" };
        public CargoPackingMinigame(int slots)
        {
            if (slots != 3 && slots != 4 && slots != 6) throw new ArgumentException("The bay must have 3, 4 or 6 slots.", "slots");
            state = new CargoPackingState { slotCount = slots };
        }
        // The bay ID distinguishes different bays with the same number of slots.
        public CargoPackingMinigame(string bayId, int slots) : this(slots)
        {
            if (string.IsNullOrWhiteSpace(bayId)) throw new ArgumentException("A bay ID is required.", "bayId");
            state.bayId = bayId;
        }
        public bool ChangeBay(string bayId, int slots)
        {
            if (string.IsNullOrWhiteSpace(bayId) || (slots != 3 && slots != 4 && slots != 6)) return false;
            if (state.bayId == bayId && state.slotCount == slots) return true;
            state.bayId = bayId; state.slotCount = slots; state.items.Clear();
            return true;
        }
        // A mismatched or malformed snapshot is rejected without creating partial state.
        public static bool TryRestore(CargoPackingState saved, string expectedBayId, int expectedSlots, out CargoPackingMinigame restored)
        {
            restored = null;
            if (saved == null || saved.items == null || string.IsNullOrWhiteSpace(expectedBayId) || saved.bayId != expectedBayId || saved.slotCount != expectedSlots || (expectedSlots != 3 && expectedSlots != 4 && expectedSlots != 6)) return false;
            var candidate = new CargoPackingMinigame(expectedBayId, expectedSlots);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in saved.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || !seen.Add(item.id) || !candidate.Place(item.id, item.startSlot).accepted) return false;
                if (item.locked && !candidate.Lock(item.id).accepted) return false;
            }
            restored = candidate; return true;
        }
        static int Width(string id) { return id == "panel" ? 2 : 1; }
        public bool CanPlace(string id, int start)
        {
            if (!Allowed.Contains(id) || start < 0 || start >= state.slotCount) return false;
            int width = Width(id);
            if (start + width > state.slotCount || (state.slotCount == 6 && width == 2 && start / 3 != (start + 1) / 3)) return false;
            foreach (var other in state.items)
            {
                if (other.id == id) continue;
                if (start < other.startSlot + Width(other.id) && other.startSlot < start + width) return false;
            }
            return true;
        }
        public CartWheelResponse Place(string id, int start)
        {
            if (!CanPlace(id, start)) return new CartWheelResponse(false, id == "panel" ? "The P-4 panel needs two empty spaces side by side." : "The item does not fit in those slots.");
            var item = state.items.Find(x => x.id == id);
            if (item == null) state.items.Add(new PackedCargo { id = id, startSlot = start });
            else if (item.startSlot != start) { item.startSlot = start; item.locked = false; }
            return new CartWheelResponse(true, "Cargo placed. Lock it before launch.");
        }
        public CartWheelResponse Remove(string id)
        {
            var item = state.items.Find(x => x.id == id);
            if (item == null) return new CartWheelResponse(false, "Item is not in the bay.");
            state.items.Remove(item); return new CartWheelResponse(true, "Item removed.");
        }
        public CartWheelResponse Lock(string id)
        {
            var item = state.items.Find(x => x.id == id);
            if (item == null) return new CartWheelResponse(false, "Place the item before locking it.");
            item.locked = true; return new CartWheelResponse(true, "Item locked.");
        }
        public CartWheelResponse LaunchCheck()
        {
            if (!state.items.Exists(x => x.id == "panel") || !state.items.Exists(x => x.id == "kit")) return new CartWheelResponse(false, "Pack the P-4 panel and insulated repair kit before launch.");
            var loose = state.items.Find(x => !x.locked);
            if (loose != null) return new CartWheelResponse(false, loose.id + " is loose. Lock it before launch.");
            return new CartWheelResponse(true, "All required cargo is packed and locked.");
        }
        public CargoPackingState Snapshot()
        { return new CargoPackingState { bayId = state.bayId, slotCount = state.slotCount, items = state.items.Select(x => new PackedCargo { id = x.id, startSlot = x.startSlot, locked = x.locked }).ToList() }; }
    }
}
