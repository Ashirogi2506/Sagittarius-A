using System;
using System.Collections.Generic;
using System.IO;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Mars;
using SagittariusA.Minigames;
using SagittariusA.Minigames.CampaignTasks;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.EditorTools.Mars
{
    public static class MarsAcceptanceChecks
    {
        private static readonly List<string> passed = new List<string>();

        [MenuItem("Far Signal/Mars/Run Mars Acceptance Checks")]
        public static void RunMenu()
        {
            try { RunAll(); Debug.Log("Mars acceptance checks passed:\n- " + string.Join("\n- ", passed)); }
            catch (Exception error) { Debug.LogException(error); throw; }
        }

        // Intended for: Unity.exe -batchmode -projectPath ... -executeMethod
        // SagittariusA.EditorTools.Mars.MarsAcceptanceChecks.RunBatch -quit
        public static void RunBatch()
        {
            try
            {
                RunAll();
                string report = "Mars acceptance checks passed\n" + DateTime.UtcNow.ToString("u") + " UTC\n\n- " + string.Join("\n- ", passed) + "\n";
                string path = Path.Combine(Application.dataPath, "SagittariusA", "Documentation", "MarsAcceptanceResults.txt");
                File.WriteAllText(path, report);
                Debug.Log(report);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void RunAll()
        {
            passed.Clear();
            ManualHelpAndNoSpare();
            SpareSpendPersistsOnce();
            PacketOrderAndRetry();
            CapacityAndCorrection();
            BlankGraphThenRecovery();
            ChangeInvalidatesTest();
            ClampGateAndBasicDeparture();
            OptionalContentPersists();
            RevisionAndOrderProtection();
            UnicodeCommanderAndExactHandoff();
            IntegratedMinigameCampaign();
        }

        private static void ManualHelpAndNoSpare()
        {
            var mission = NewMission(false, "No Spare");
            Apply(mission, MarsActionKind.RecordAntennaMiss); Apply(mission, MarsActionKind.RecordAntennaMiss);
            Is(mission.State.showTarget, "Show target must appear after two misses.");
            AlignManually(mission);
            Is(mission.State.phase == MarsPhase.ArrivalPackets && mission.State.antennaRecovery == "manual", "Manual alignment must work without a spare.");
            passed.Add("No-spare manual alignment and help after two misses");
        }

        private static void SpareSpendPersistsOnce()
        {
            var store = new MemoryMarsDraftStore(); var handoff = Handoff(true, "Spare Test");
            var mission = new MarsMission(handoff, store); Apply(mission, MarsActionKind.UseSpare);
            Is(!mission.State.handoff.build.cargo.Contains("spares") && mission.State.spareSpent, "Spare must be removed exactly once.");
            var restored = new MarsMission(handoff, store);
            Is(restored.State.spareSpent && !restored.State.handoff.build.cargo.Contains("spares"), "Spent spare must persist on reload.");
            Is(restored.Act(MarsActionKind.UseSpare).status == MarsActionStatus.Rejected, "Reload must not permit a second spend.");
            passed.Add("Confirmed spare recovery persists without double spending");
        }

        private static void PacketOrderAndRetry()
        {
            var mission = NewMission(false, "Packets"); AlignManually(mission);
            Apply(mission, MarsActionKind.QueueArchive); Apply(mission, MarsActionKind.QueueArrival);
            Apply(mission, MarsActionKind.SendNextPacket);
            Is(mission.State.completedPackets.Contains("pole-7-archive") && mission.State.contactSecondsLeft == 1, "Archive must use seven seconds.");
            Apply(mission, MarsActionKind.SendNextPacket);
            Is(!mission.State.completedPackets.Contains("arrival-update") && mission.State.packetQueue[0] == "arrival-update" && mission.State.contactSecondsLeft == 0, "Unfinished arrival must remain queued.");
            Apply(mission, MarsActionKind.StartContactWindow); Apply(mission, MarsActionKind.SendNextPacket);
            Is(mission.State.completedPackets.Contains("arrival-update"), "Arrival must deliver in the fresh window.");
            passed.Add("8-second packet window preserves delivery and queue across retry");
        }

        private static void CapacityAndCorrection()
        {
            var mission = SurfaceMission("Mass");
            Apply(mission, MarsActionKind.SelectDirectionalAntenna); Apply(mission, MarsActionKind.SelectReinforcedShield);
            Is(mission.MassKg == 305 && !mission.WithinCapacity, "Directional plus reinforced must weigh 305 kg and be blocked.");
            Is(mission.Act(MarsActionKind.SeatOrbiter).message.Contains("5 kg over"), "Block must state the exact 5 kg correction.");
            Apply(mission, MarsActionKind.SelectLightAntenna); Is(mission.MassKg == 280 && mission.WithinCapacity, "Light plus reinforced must be legal at 280 kg.");
            passed.Add("Draft 300 kg cradle validation and exact 5 kg correction");
        }

        private static void BlankGraphThenRecovery()
        {
            var mission = SurfaceMission("Graph"); Assemble(mission); Apply(mission, MarsActionKind.ConnectPower);
            Apply(mission, MarsActionKind.BenchPowerOn); Apply(mission, MarsActionKind.BenchSample); Apply(mission, MarsActionKind.BenchTransmit);
            Is(!mission.CurrentTestPassed && mission.State.benchGraph.samples.Length == 0 && mission.Notice.Contains("DATA"), "Missing data must produce a blank graph and recovery hint.");
            Apply(mission, MarsActionKind.ConnectData); Apply(mission, MarsActionKind.BenchPowerOn); Apply(mission, MarsActionKind.BenchSample); Apply(mission, MarsActionKind.BenchTransmit);
            Is(mission.CurrentTestPassed && mission.State.benchGraph.samples.Length == 6, "Connected standard build must produce six samples.");
            Is(mission.State.benchGraph.xAxis == "Sample" && mission.State.benchGraph.yAxis == "Simulated particle count", "Graph axes must use the brief's exact labels.");
            passed.Add("Blank graph recovery and labelled successful bench graph");
        }

        private static void ChangeInvalidatesTest()
        {
            var mission = TestedMission("Invalidate");
            Apply(mission, MarsActionKind.SelectDirectionalAntenna);
            Is(!mission.CurrentTestPassed && !mission.State.testPassed, "Changing a module must invalidate a passed test.");
            Is(mission.Act(MarsActionKind.SeatOrbiter).status == MarsActionStatus.Rejected, "Invalidated build must not load.");
            passed.Add("Module changes invalidate the current bench test and loading");
        }

        private static void ClampGateAndBasicDeparture()
        {
            var mission = TestedMission("Clamp"); Apply(mission, MarsActionKind.SeatOrbiter); Apply(mission, MarsActionKind.CloseLeftClamp); Apply(mission, MarsActionKind.ReceiveDeploymentPlan);
            Is(!mission.CanDepart && mission.Act(MarsActionKind.CompleteMars).message.Contains("Both distinct"), "One clamp must not permit departure.");
            Apply(mission, MarsActionKind.CloseRightClamp); Is(mission.CanDepart, "Both clamps plus plan must permit departure.");
            Apply(mission, MarsActionKind.CompleteMars); Is(mission.State.phase == MarsPhase.Complete, "Basic build must complete without optional quests.");
            passed.Add("Distinct clamp gate and optional-free basic departure");
        }

        private static void OptionalContentPersists()
        {
            var store = new MemoryMarsDraftStore(); var handoff = Handoff(false, "Optional"); var mission = new MarsMission(handoff, store);
            AlignManually(mission); Apply(mission, MarsActionKind.QueueArrival); Apply(mission, MarsActionKind.SendNextPacket); Apply(mission, MarsActionKind.Land);
            Apply(mission, MarsActionKind.DustAnswerAfternoon); Apply(mission, MarsActionKind.PracticeQueueHealth); Apply(mission, MarsActionKind.PracticeQueueSample); Apply(mission, MarsActionKind.PracticeQueueGraph); Apply(mission, MarsActionKind.PracticeSend);
            Is(mission.State.practicePassed, "Health then sample must pass within ten seconds.");
            var restored = new MarsMission(handoff, store);
            Is(restored.State.dustDiaryComplete && restored.State.dustSelectedTime == "14:00" && restored.State.dustSelectedIndex == 7, "Dust result must persist.");
            Is(restored.State.practicePassed && restored.State.practiceQueue[0] == "health" && restored.State.practiceQueue[1] == "sample", "Passing editable packet suggestion must persist.");
            passed.Add("Dust Diary and packet practice persist across reload");
        }

        private static void RevisionAndOrderProtection()
        {
            var mission = NewMission(false, "Transactions"); AlignManually(mission);
            var command = new MarsAction { runId = mission.State.runId, expectedRevision = mission.State.revision, operationId = "same-operation", kind = MarsActionKind.QueueArrival };
            Is(mission.Apply(command).status == MarsActionStatus.Applied, "First command must apply.");
            Is(mission.Apply(command).status == MarsActionStatus.AlreadyApplied && mission.State.packetQueue.Count == 1, "Duplicate operation must not apply twice.");
            var stale = new MarsAction { runId = mission.State.runId, expectedRevision = command.expectedRevision, operationId = "stale-operation", kind = MarsActionKind.QueueArchive };
            Is(mission.Apply(stale).status == MarsActionStatus.Rejected, "Stale revision must be rejected.");
            Is(mission.Act(MarsActionKind.BenchTransmit).status == MarsActionStatus.Rejected, "Out-of-order surface action must be rejected in transit.");
            passed.Add("Duplicate, stale and out-of-order commands are rejected");
        }

        private static void UnicodeCommanderAndExactHandoff()
        {
            var mission = TestedMission("Нара"); Apply(mission, MarsActionKind.SeatOrbiter); Apply(mission, MarsActionKind.CloseLeftClamp); Apply(mission, MarsActionKind.CloseRightClamp); Apply(mission, MarsActionKind.ReceiveDeploymentPlan); Apply(mission, MarsActionKind.CompleteMars);
            var handoff = mission.GetGanymedeHandoff();
            Is(handoff != null && handoff.playerName == "Нара", "Unicode commander name must survive the handoff.");
            Is(handoff.sensorMode == "standard" && handoff.antenna == "light" && handoff.shield == "basic" && handoff.orbiterMassKg == 250, "Exact tested basic orbiter must survive the handoff.");
            Is(handoff.nextMissionId == "ganymede" && handoff.benchGraph.samples.Length == 6, "Ganymede contract must contain the tested graph.");
            passed.Add("Unicode commander and exact tested orbiter reach the Ganymede contract");
        }

        private static void IntegratedMinigameCampaign()
        {
            var owner = new GameObject("Mars minigame acceptance controller");
            try
            {
                var stage = owner.AddComponent<MarsStageController>();
                Is(stage.Initialize(Handoff(false, "Integrated"), new MemoryMarsDraftStore()), "Integrated Mars controller failed to initialize.");

                Is(MarsMinigameCampaignAdapter.Commit(stage, Success(MarsAntennaAlignmentMinigame.IdValue)).Accepted, "Antenna result did not commit.");
                Is(stage.Phase == MarsPhase.ArrivalPackets && stage.Context.antennaRecovery == "manual", "Antenna result did not unlock the arrival phase.");

                var arrival = Success(MarsArrivalUplinkMinigame.IdValue);
                arrival.OutputValues["archiveDelivered"] = "True";
                Is(MarsMinigameCampaignAdapter.Commit(stage, arrival).Accepted, "Arrival result did not commit.");
                Is(Array.IndexOf(stage.Context.completedPackets, "arrival-update") >= 0, "Arrival packet was not persisted.");
                Is(stage.Act(MarsActionKind.Land).Accepted && stage.Phase == MarsPhase.MarsSurface, "Integrated arrival did not unlock landing.");

                var orbiter = Success(MarsOrbiterBenchMinigame.IdValue);
                orbiter.OutputValues["antenna"] = "light";
                orbiter.OutputValues["shield"] = "reinforced";
                Is(!MarsMinigameCampaignAdapter.Commit(stage, orbiter).Accepted && !stage.Context.testPassed && !stage.Context.orbiterSeated, "A standalone practice result must not auto-complete physical assembly and loading.");
                foreach(var action in new[]{MarsActionKind.SelectReinforcedShield,MarsActionKind.SelectSensorPart,MarsActionKind.PlaceSensorMount,
                    MarsActionKind.SelectAntennaPart,MarsActionKind.PlaceAntennaMount,MarsActionKind.SelectShieldPart,MarsActionKind.PlaceShieldMount,
                    MarsActionKind.ConnectPower,MarsActionKind.ConnectData,MarsActionKind.BenchPowerOn,MarsActionKind.BenchSample,MarsActionKind.BenchTransmit,
                    MarsActionKind.SeatOrbiter,MarsActionKind.CloseLeftClamp,MarsActionKind.CloseRightClamp})
                    Is(stage.Act(action).Accepted,"Physical action rejected: "+action);
                Is(stage.Context.massKg == 280 && stage.Context.testPassed && stage.Context.orbiterSeated && stage.Context.leftClamp && stage.Context.rightClamp,
                    "Integrated orbiter choices, test, loading, or clamps were not persisted.");

                Is(MarsMinigameCampaignAdapter.Commit(stage, Success(MarsDustDiaryMinigame.IdValue)).Accepted && stage.Context.dustDiaryComplete,
                    "Dust Diary result did not commit.");
                Is(MarsMinigameCampaignAdapter.Commit(stage, Success(MarsPacketPracticeMinigame.IdValue)).Accepted && stage.Context.practicePassed,
                    "Packet-practice result did not commit.");
                Is(stage.Act(MarsActionKind.ReceiveDeploymentPlan).Accepted && stage.Act(MarsActionKind.CompleteMars).Accepted,
                    "Integrated task path did not reach Mars completion.");
                Is(stage.GetGanymedeHandoff() != null && stage.GetGanymedeHandoff().orbiterMassKg == 280,
                    "Integrated task path did not produce the exact Ganymede handoff.");
                passed.Add("Transit and optional tasks integrate; practice cannot bypass physical assembly, test, transport, and clamps");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static MinigameResult Success(string id)
        {
            return new MinigameResult { MinigameId = id, MissionId = "mars-staging", Outcome = MinigameOutcome.Success,
                OutputValues = new Dictionary<string, string>() };
        }

        private static MarsMission NewMission(bool spares, string name) { return new MarsMission(Handoff(spares, name), new MemoryMarsDraftStore()); }
        private static MarsMission SurfaceMission(string name)
        { var mission = NewMission(false, name); AlignManually(mission); Apply(mission, MarsActionKind.QueueArrival); Apply(mission, MarsActionKind.SendNextPacket); Apply(mission, MarsActionKind.Land); return mission; }
        private static MarsMission TestedMission(string name)
        { var mission = SurfaceMission(name); Assemble(mission); Apply(mission, MarsActionKind.ConnectPower); Apply(mission, MarsActionKind.ConnectData); Apply(mission, MarsActionKind.BenchPowerOn); Apply(mission, MarsActionKind.BenchSample); Apply(mission, MarsActionKind.BenchTransmit); return mission; }
        private static void Assemble(MarsMission mission)
        { Apply(mission, MarsActionKind.SelectSensorPart); Apply(mission, MarsActionKind.PlaceSensorMount); Apply(mission, MarsActionKind.SelectAntennaPart); Apply(mission, MarsActionKind.PlaceAntennaMount); Apply(mission, MarsActionKind.SelectShieldPart); Apply(mission, MarsActionKind.PlaceShieldMount); }
        private static void AlignManually(MarsMission mission)
        {
            while (mission.State.antennaAngle < mission.State.antennaTarget - .2f) mission.TickManualAlignment(1, 1f / 60f);
            for (int i = 0; i < 70 && mission.State.phase == MarsPhase.AntennaRecovery; i++) mission.TickManualAlignment(0, 1f / 60f);
            Is(mission.State.phase == MarsPhase.ArrivalPackets, "Antenna failed to lock after a one-second in-band hold.");
        }
        private static MoonMarsHandoff Handoff(bool spares, string name)
        {
            var build = new EarthBuild(); if (spares) build.cargo.Add("spares");
            return new MoonMarsHandoff { sourceRunId = Guid.NewGuid().ToString("N"), missionId = "pole-7", nextMissionId = "mars-staging", commanderId = "test", playerName = name,
                build = build, remainingFuelUnits = 5, batteryKwh = 6, supplyKw = 5, routeUploaded = true, relayRestored = true, beaconMarked = true };
        }
        private static MarsActionResult Apply(MarsMission mission, MarsActionKind kind)
        { var result = mission.Act(kind); Is(result.status == MarsActionStatus.Applied, kind + " rejected: " + result.message); return result; }
        private static void Is(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
