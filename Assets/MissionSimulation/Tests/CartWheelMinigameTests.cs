using NUnit.Framework;
using MissionSimulation.Minigames;

namespace MissionSimulation.Tests
{
    public class CartWheelMinigameTests
    {
        [Test] public void LiveCoverCannotOpenAndCleanProgressSurvivesRetest()
        {
            var game = new CartWheelMinigame(new CartWheelState());
            Assert.IsFalse(game.StartCart().accepted); Assert.IsFalse(game.OpenCover().accepted);
            game.SwitchOff(); Assert.IsTrue(game.OpenCover().accepted);
            game.HoldPatch(0, 1f); game.HoldPatch(1, 1f); game.CloseCover();
            Assert.IsFalse(game.StartCart().accepted); Assert.AreEqual(2, game.CleanCount);
            game.SwitchOff(); game.OpenCover(); game.HoldPatch(2, 1f); game.CloseCover();
            Assert.IsTrue(game.StartCart().accepted); Assert.IsTrue(game.LoadAndLock("panel").accepted); Assert.IsTrue(game.LoadAndLock("kit").accepted);
        }
        [Test] public void ChangingPatchOrReleasingCancelsPartialHold()
        {
            var game = new CartWheelMinigame(new CartWheelState()); game.StartCart(); game.SwitchOff(); game.OpenCover();
            game.HoldPatch(0, .7f); game.HoldPatch(1, .5f); game.HoldPatch(0, .5f); Assert.AreEqual(0, game.CleanCount);
            game.EndHold(); game.HoldPatch(0, .5f); Assert.AreEqual(0, game.CleanCount); game.HoldPatch(0, .5f); Assert.AreEqual(1, game.CleanCount);
            Assert.IsFalse(game.HoldPatch(0, 1f).accepted); Assert.AreEqual(1, game.CleanCount);
        }
        [Test] public void LoadingBeforeSuccessfulTestIsRejected()
        { var game = new CartWheelMinigame(new CartWheelState()); Assert.IsFalse(game.LoadAndLock("panel").accepted); }
        [Test] public void SnapshotDoesNotShareDustArray()
        {
            var game = new CartWheelMinigame(new CartWheelState()); game.StartCart(); game.SwitchOff(); game.OpenCover(); game.HoldPatch(0, 1f);
            var snapshot = game.Snapshot(); snapshot.cleaned[0] = false; Assert.AreEqual(1, game.CleanCount);
        }
    }
}
