using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionSimulation.Minigames
{
    [Serializable] public class PackedCargo { public string id; public int startSlot; public bool locked; }
    [Serializable] public class CargoPackingState { public int slotCount; public List<PackedCargo> items = new List<PackedCargo>(); }
    public sealed class CargoPackingMinigame
    {
        readonly CargoPackingState state;
        static readonly string[] Allowed = { "panel", "kit", "laser", "battery", "spares", "fuel" };
        public CargoPackingMinigame(int slots)
        {
            if (slots != 3 && slots != 4 && slots != 6) throw new ArgumentException("The bay must have 3, 4 or 6 slots.", "slots");
            state = new CargoPackingState { slotCount = slots };
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
        { return new CargoPackingState { slotCount = state.slotCount, items = state.items.Select(x => new PackedCargo { id = x.id, startSlot = x.startSlot, locked = x.locked }).ToList() }; }
    }
}
