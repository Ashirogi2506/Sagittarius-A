using System;
using System.Linq;

namespace MissionSimulation.FarSignal
{
    // A serializable transaction record. The campaign save owner must persist this entire object atomically.
    [Serializable] public class LunarDescentState
    {
        public string runId, committedOperationId, committedPadId;
        public float remainingFuelUnits, committedArrivalMinute, committedCostUnits;
        public bool committed;
    }
    public enum DescentStatus { InvalidInput, InsufficientFuel, InShadow, Ready, Committed, AlreadyCommitted, OperationConflict }
    [Serializable] public class DescentResult
    {
        public DescentStatus status;
        public string message;
        public float fuelRequiredUnits, fuelAvailableUnits;
        public RestartDestination restartDestination;
        public LunarDescentState nextState;
    }
    public sealed class LunarDescent
    {
        static bool FiniteNonnegative(float x) { return !float.IsNaN(x) && !float.IsInfinity(x) && x >= 0; }
        static LunarDescentState Copy(LunarDescentState s)
        {
            return new LunarDescentState { runId = s.runId, committed = s.committed, committedOperationId = s.committedOperationId, committedPadId = s.committedPadId, remainingFuelUnits = s.remainingFuelUnits, committedArrivalMinute = s.committedArrivalMinute, committedCostUnits = s.committedCostUnits };
        }
        public DescentResult Preview(EarthScenario scenario, LunarDescentState state, string padId, float arrivalMinute)
        {
            var result = new DescentResult { status = DescentStatus.InvalidInput, message = "Landing data is incomplete or invalid." };
            if (scenario == null || scenario.landingMap == null || state == null || string.IsNullOrWhiteSpace(state.runId) || string.IsNullOrWhiteSpace(padId) || !FiniteNonnegative(state.remainingFuelUnits) || !FiniteNonnegative(arrivalMinute) || arrivalMinute > 10) return result;
            var matches = scenario.landingMap.Where(x => x != null && x.id == padId).ToArray();
            if (matches.Length != 1) return result;
            var pad = matches[0];
            if (!FiniteNonnegative(pad.fuelUnits) || pad.openMinute < 0 || pad.closeMinute <= pad.openMinute) return result;
            result.fuelRequiredUnits = pad.fuelUnits; result.fuelAvailableUnits = state.remainingFuelUnits;
            if (state.committed) { result.status = DescentStatus.AlreadyCommitted; result.message = "Descent is already committed for this run."; return result; }
            if (state.remainingFuelUnits < pad.fuelUnits)
            {
                result.status = DescentStatus.InsufficientFuel;
                result.restartDestination = RestartDestination.PreLaunchCheckpoint;
                result.message = "This pad needs " + pad.fuelUnits + " fuel units. You have " + state.remainingFuelUnits + ". Return to the pre-launch checkpoint to restart the flight.";
                return result;
            }
            if (arrivalMinute < pad.openMinute || arrivalMinute >= pad.closeMinute)
            { result.status = DescentStatus.InShadow; result.message = "This pad is in shadow at the selected time. Choose an earlier time or another pad."; return result; }
            result.status = DescentStatus.Ready; result.message = "Fuel and daylight checks passed."; return result;
        }
        // The returned copy is a proposed update, not a durable save or a concurrent transaction.
        public DescentResult Confirm(EarthScenario scenario, LunarDescentState state, string padId, float arrivalMinute, string operationId)
        {
            if (state != null && state.committed)
            {
                if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(state.committedOperationId) || string.IsNullOrWhiteSpace(state.committedPadId) || string.IsNullOrWhiteSpace(state.runId) || !FiniteNonnegative(state.remainingFuelUnits) || !FiniteNonnegative(state.committedArrivalMinute) || !FiniteNonnegative(state.committedCostUnits))
                    return new DescentResult { status = DescentStatus.InvalidInput, message = "The saved descent record is invalid." };
                bool same = state.committedOperationId == operationId && state.committedPadId == padId && state.committedArrivalMinute == arrivalMinute;
                return new DescentResult { status = same ? DescentStatus.AlreadyCommitted : DescentStatus.OperationConflict, message = same ? "This descent was already committed." : "A different descent was already committed.", nextState = Copy(state) };
            }
            var result = Preview(scenario, state, padId, arrivalMinute);
            if (string.IsNullOrWhiteSpace(operationId)) { result.status = DescentStatus.InvalidInput; result.message = "A unique operation ID is required."; return result; }
            if (result.status != DescentStatus.Ready) return result;
            var next = Copy(state);
            next.remainingFuelUnits -= result.fuelRequiredUnits; next.committed = true; next.committedOperationId = operationId;
            next.committedPadId = padId; next.committedArrivalMinute = arrivalMinute; next.committedCostUnits = result.fuelRequiredUnits;
            result.nextState = next; result.status = DescentStatus.Committed; result.message = "Descent approved. Save the returned state before advancing.";
            return result;
        }
    }
}
