using NUnit.Framework;
using UnityEngine;
using MissionSimulation.FarSignal;

namespace MissionSimulation.Tests
{
    public class LunarDescentTests
    {
        EarthScenario scenario; LunarDescent descent; LunarDescentState state;
        [SetUp] public void Setup()
        {
            scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            descent = new LunarDescent(); state = new LunarDescentState { runId = "run-1", remainingFuelUnits = 12 };
        }
        [Test] public void PreviewNeverSpendsFuelAndFuelFailureRequestsPrelaunch()
        {
            var r = descent.Preview(scenario, state, "ridge", 3);
            Assert.AreEqual(DescentStatus.InsufficientFuel, r.status); Assert.AreEqual(RestartDestination.PreLaunchCheckpoint, r.restartDestination);
            Assert.AreEqual(12f, state.remainingFuelUnits); Assert.IsFalse(state.committed);
        }
        [Test] public void ValidConfirmationSpendsOnceOnReturnedState()
        {
            var result = descent.Confirm(scenario, state, "main", 3, "op-1");
            Assert.AreEqual(DescentStatus.Committed, result.status); Assert.AreEqual(2f, result.nextState.remainingFuelUnits); Assert.AreEqual(12f, state.remainingFuelUnits);
            var replay = descent.Confirm(scenario, result.nextState, "main", 3, "op-1");
            Assert.AreEqual(DescentStatus.AlreadyCommitted, replay.status); Assert.AreEqual(2f, replay.nextState.remainingFuelUnits);
            Assert.AreEqual(DescentStatus.OperationConflict, descent.Confirm(scenario, result.nextState, "main", 3, "op-2").status);
        }
        [Test] public void ShadowOrInvalidPlanCannotSpendFuel()
        {
            var shadow = descent.Confirm(scenario, state, "main", 4, "op-1");
            Assert.AreEqual(DescentStatus.InShadow, shadow.status); Assert.IsNull(shadow.nextState); Assert.AreEqual(12f, state.remainingFuelUnits);
            Assert.AreEqual(DescentStatus.InvalidInput, descent.Preview(scenario, state, "main", float.NaN).status);
            Assert.AreEqual(DescentStatus.InvalidInput, descent.Confirm(scenario, state, "main", 3, "").status);
        }
        [Test] public void ExactDaylightAndFuelBoundaryAreSupported()
        {
            state.remainingFuelUnits = 13; var ready = descent.Confirm(scenario, state, "ridge", 4, "op");
            Assert.AreEqual(DescentStatus.Committed, ready.status); Assert.AreEqual(0f, ready.nextState.remainingFuelUnits);
            Assert.AreEqual(DescentStatus.InShadow, descent.Preview(scenario, state, "ridge", 8).status);
        }
    }
}
