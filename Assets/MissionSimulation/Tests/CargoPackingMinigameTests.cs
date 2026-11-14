using NUnit.Framework;
using MissionSimulation.Minigames;
namespace MissionSimulation.Tests
{
    public class CargoPackingMinigameTests
    {
        [Test] public void PanelCannotCrossRowOrOverlap()
        {
            var game = new CargoPackingMinigame(6);
            Assert.IsFalse(game.Place("panel", 2).accepted);
            Assert.IsTrue(game.Place("panel", 0).accepted);
            Assert.IsFalse(game.Place("kit", 1).accepted);
        }
        [Test] public void MovingLockedPanelRequiresRelocking()
        {
            var game = new CargoPackingMinigame(6);
            game.Place("panel", 0); game.Place("kit", 3); game.Lock("panel"); game.Lock("kit");
            Assert.IsTrue(game.LaunchCheck().accepted); game.Place("panel", 4);
            Assert.IsFalse(game.LaunchCheck().accepted); game.Lock("panel"); Assert.IsTrue(game.LaunchCheck().accepted);
        }
        [Test] public void EachLoadedOptionalItemMustBeLocked()
        {
            var game = new CargoPackingMinigame(4);
            game.Place("panel", 0); game.Place("kit", 2); game.Place("laser", 3); game.Lock("panel"); game.Lock("kit");
            Assert.IsFalse(game.LaunchCheck().accepted); game.Lock("laser"); Assert.IsTrue(game.LaunchCheck().accepted);
        }
        [Test] public void ChangingBaysWithEqualCapacityRequiresRepacking()
        {
            var game = new CargoPackingMinigame("c3",6); game.Place("panel",0); game.Place("kit",2); game.Lock("panel"); game.Lock("kit");
            Assert.IsTrue(game.ChangeBay("c4",6)); Assert.IsFalse(game.LaunchCheck().accepted); Assert.AreEqual(0,game.Snapshot().items.Count);
        }
        [Test] public void SameBayKeepsPackingAndInvalidChangeDoesNotEraseIt()
        {
            var game = new CargoPackingMinigame("c1",3); game.Place("panel",0); game.Place("kit",2); game.Lock("panel"); game.Lock("kit");
            Assert.IsTrue(game.ChangeBay("c1",3)); Assert.IsFalse(game.ChangeBay("invalid",5)); Assert.IsTrue(game.LaunchCheck().accepted);
        }
        [Test] public void SavedPackingRestoresIndependentlyAndRejectsMismatch()
        {
            var game = new CargoPackingMinigame("c1",3); game.Place("panel",0); game.Place("kit",2); game.Lock("panel"); game.Lock("kit");
            var saved = game.Snapshot(); CargoPackingMinigame restored;
            Assert.IsTrue(CargoPackingMinigame.TryRestore(saved,"c1",3,out restored)); saved.items[0].locked = false;
            Assert.IsTrue(restored.LaunchCheck().accepted);
            Assert.IsFalse(CargoPackingMinigame.TryRestore(saved,"another",3,out restored));
        }
        [Test] public void RestoringOverlappingCargoIsRejected()
        {
            var game = new CargoPackingMinigame("c1",3); game.Place("panel",0); game.Place("kit",2);
            var saved = game.Snapshot(); saved.items[1].startSlot = 1; CargoPackingMinigame restored;
            Assert.IsFalse(CargoPackingMinigame.TryRestore(saved,"c1",3,out restored)); Assert.IsNull(restored);
        }
    }
}
