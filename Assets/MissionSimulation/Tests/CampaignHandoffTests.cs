using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MissionSimulation.FarSignal;
using MissionSimulation.Minigames;

namespace MissionSimulation.Tests
{
    public class CampaignHandoffTests
    {
        [Test] public void LunarSupplyAndOptionalBatteryTransactionsAreIdempotent()
        {
            var s = new CampaignState { lunarDescent = new LunarDescentState { committed = true }, inventory = new List<string> { "panel", "kit", "battery" } };
            Assert.IsFalse(CampaignProgress.DeliverPole7Supplies(s));
            Assert.IsTrue(CampaignProgress.MarkLunarRouteChecked(s));
            Assert.IsTrue(CampaignProgress.LeaveBatteryAtBeacon(s)); Assert.IsTrue(CampaignProgress.LeaveBatteryAtBeacon(s));
            Assert.IsFalse(s.inventory.Contains("battery"));
            Assert.IsTrue(CampaignProgress.DeliverPole7Supplies(s)); Assert.IsTrue(CampaignProgress.DeliverPole7Supplies(s));
            Assert.IsFalse(s.inventory.Contains("panel")); Assert.IsTrue(s.inventory.Contains("kit"));
            Assert.IsFalse(CampaignProgress.RestorePole7Relay(s, false));
            Assert.IsTrue(CampaignProgress.RestorePole7Relay(s, true));
            Assert.IsTrue(CampaignProgress.UploadLunarRoute(s)); Assert.IsTrue(CampaignProgress.ReceiveMarsApproach(s));
            Assert.IsFalse(CampaignProgress.CanContinueToMars(s));
            Assert.IsTrue(CampaignProgress.ReturnToShip(s)); Assert.IsTrue(CampaignProgress.CanContinueToMars(s));
        }
        [Test] public void ValidEarthCheckpointPreservesIdentityAndDescentIsAppliedOnce()
        {
            var scenario = JsonUtility.FromJson<EarthScenario>(Resources.Load<TextAsset>("FarSignal.Earth.proposed").text);
            var design = new EarthDesign { launcherId="l1", nozzleId="n1", fairingId="f1", engineId="e1", cargoModuleId="c1", powerSystemId="p1", landingPadId="main", cargo=new[] { new CargoPlacement { cargoId="panel",startSlot=0 }, new CargoPlacement { cargoId="kit",startSlot=2 } } };
            var packing = new CargoPackingMinigame("c1",3); packing.Place("panel",0); packing.Place("kit",2); packing.Lock("panel"); packing.Lock("kit");
            var power = new EarthPowerTest("p1",5f,false); power.SetCabin(true); power.SetRadio(true);
            var checkpoint = new PreLaunchCheckpointService().Create("run-1",new CommanderIdentity { commanderId="commander_female",playerName="Нара",portraitReference="portrait" },scenario,design,packing.Snapshot(),power.Snapshot(),new LandingPlan { padId="main",hasArrivalMinute=true,arrivalMinute=3 });
            Assert.IsTrue(checkpoint.success);
            var campaign = CampaignProgress.CreateFromCheckpoint(scenario, checkpoint.checkpoint);
            Assert.AreEqual("Нара", campaign.commanderName); Assert.Contains("panel", campaign.inventory);
            Assert.IsTrue(CampaignProgress.CommitLunarDescent(campaign, scenario, "main", 3, "descent-1"));
            Assert.AreEqual(5f, campaign.lunarDescent.remainingFuelUnits);
            Assert.IsTrue(CampaignProgress.CommitLunarDescent(campaign, scenario, "main", 3, "descent-1"));
            Assert.AreEqual(5f, campaign.lunarDescent.remainingFuelUnits);
            Assert.IsFalse(CampaignProgress.CommitLunarDescent(campaign, scenario, "ridge", 3, "descent-2"));
            checkpoint.checkpoint.fuelAtLaunchUnits = 999;
            Assert.Throws<ArgumentException>(() => CampaignProgress.CreateFromCheckpoint(scenario, checkpoint.checkpoint));
        }
    }
}
