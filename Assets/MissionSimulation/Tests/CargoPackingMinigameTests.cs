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
    }
}
