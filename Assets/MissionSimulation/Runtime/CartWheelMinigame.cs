using System;

namespace MissionSimulation.Minigames
{
    // State is serializable; scene effects, holds and persistence are owned by the caller.
    [Serializable] public class CartWheelState
    {
        public bool powered, coverOpen, foundJam, testPassed;
        public bool[] cleaned = new bool[3];
        public bool panelLoaded, panelLocked, kitLoaded, kitLocked;
    }
    [Serializable] public class CartWheelResponse
    {
        public bool accepted;
        public string message;
        public CartWheelResponse(bool accepted, string message) { this.accepted = accepted; this.message = message; }
    }
    public sealed class CartWheelMinigame
    {
        // Prototype settings explicitly supplied by the minigame specification.
        public const float HoldSecondsPerPatch = 1f;
        readonly CartWheelState state;
        int activePatch = -1;
        float heldSeconds;
        public CartWheelMinigame(CartWheelState savedState)
        {
            if (savedState == null || savedState.cleaned == null || savedState.cleaned.Length != 3) throw new ArgumentException("Cart wheel state must contain three dust patches.", "savedState");
            if (savedState.coverOpen && savedState.powered) throw new ArgumentException("The motor cannot run with an open cover.", "savedState");
            if (savedState.testPassed && (savedState.coverOpen || !AllClean(savedState))) throw new ArgumentException("A passed test requires a closed cover and clean wheel.", "savedState");
            state = savedState;
        }
        static bool AllClean(CartWheelState s) { return s.cleaned[0] && s.cleaned[1] && s.cleaned[2]; }
        public int CleanCount { get { int n = 0; foreach (bool clean in state.cleaned) if (clean) n++; return n; } }
        public CartWheelResponse StartCart()
        {
            if (state.coverOpen) return new CartWheelResponse(false, "Close the cover before testing.");
            state.powered = true;
            if (!AllClean(state)) { state.foundJam = true; state.testPassed = false; return new CartWheelResponse(false, "Power OK. Right wheel jammed. Switch off and clear the remaining dust."); }
            state.testPassed = true; return new CartWheelResponse(true, "Wheel test passed.");
        }
        public CartWheelResponse SwitchOff() { state.powered = false; return new CartWheelResponse(true, "Cart switched off."); }
        public CartWheelResponse OpenCover()
        {
            if (state.powered) return new CartWheelResponse(false, "The wheel could move. Switch the cart off first.");
            if (!state.foundJam) return new CartWheelResponse(false, "Start the cart to check its status first.");
            state.coverOpen = true; state.testPassed = false; return new CartWheelResponse(true, "Cover opened.");
        }
        public CartWheelResponse CloseCover()
        { if (!state.coverOpen) return new CartWheelResponse(false, "The cover is already closed."); state.coverOpen = false; activePatch = -1; heldSeconds = 0; return new CartWheelResponse(true, "Cover closed."); }
        // Call while holding on the selected patch. Call EndHold when released or target is lost.
        public CartWheelResponse HoldPatch(int patchIndex, float elapsedSeconds)
        {
            if (!state.coverOpen || state.powered) return new CartWheelResponse(false, "Switch off and open the cover first.");
            if (patchIndex < 0 || patchIndex >= 3 || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) return new CartWheelResponse(false, "Invalid cleaning input.");
            if (state.cleaned[patchIndex]) return new CartWheelResponse(false, "This patch is already clean.");
            if (activePatch != patchIndex) { activePatch = patchIndex; heldSeconds = 0; }
            heldSeconds += elapsedSeconds;
            if (heldSeconds < HoldSecondsPerPatch) return new CartWheelResponse(true, "Cleaning dust.");
            state.cleaned[patchIndex] = true; activePatch = -1; heldSeconds = 0;
            return new CartWheelResponse(true, "Dust cleared: " + CleanCount + "/3.");
        }
        public void EndHold() { activePatch = -1; heldSeconds = 0; }
        public float CurrentHoldProgress { get { return Math.Min(1f, heldSeconds / HoldSecondsPerPatch); } }
        public CartWheelResponse LoadAndLock(string id)
        {
            if (!state.testPassed) return new CartWheelResponse(false, "Repair and test the wheel before loading cargo.");
            if (id == "panel") { state.panelLoaded = true; state.panelLocked = true; }
            else if (id == "kit") { state.kitLoaded = true; state.kitLocked = true; }
            else return new CartWheelResponse(false, "Only the required repair items can be loaded here.");
            return new CartWheelResponse(true, "Cargo loaded and locked.");
        }
        public CartWheelState Snapshot()
        {
            return new CartWheelState { powered = state.powered, coverOpen = state.coverOpen, foundJam = state.foundJam, testPassed = state.testPassed,
                cleaned = (bool[])state.cleaned.Clone(), panelLoaded = state.panelLoaded, panelLocked = state.panelLocked, kitLoaded = state.kitLoaded, kitLocked = state.kitLocked };
        }
    }
}
