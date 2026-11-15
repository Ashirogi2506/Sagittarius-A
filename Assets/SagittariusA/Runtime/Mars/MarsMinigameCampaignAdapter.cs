using System;
using System.Collections.Generic;
using SagittariusA.Minigames;
using SagittariusA.Minigames.CampaignTasks;

namespace SagittariusA.Gameplay.Mars
{
    // Commits successful 2D task results through the same action API used by
    // the 3D chapter. This keeps saves, validation, and the Ganymede handoff
    // authoritative in MarsMission.
    public static class MarsMinigameCampaignAdapter
    {
        public static MinigameContext CreateContext(string minigameId, MarsStageContext state)
        {
            return new MinigameContext {
                MinigameId = minigameId,
                MissionId = "mars-staging",
                EventId = "mars-" + (state == null ? "unknown" : state.revision.ToString()),
                DifficultyLevel = 1,
                RandomSeed = MinigameRandom.StableHash((state?.runId ?? "mars") + ":" + minigameId),
                AllowCancel = true,
                DisplayValues = new Dictionary<string, string> {
                    ["commander"] = state?.playerName ?? "Commander",
                    ["phase"] = state?.phase.ToString() ?? "Unknown"
                }
            };
        }

        public static MarsActionResult Commit(MarsStageController stage, MinigameResult result)
        {
            if (stage == null || !stage.Initialized)
                return Rejected("Mars mission state is unavailable.");
            if (result == null || result.Outcome != MinigameOutcome.Success)
                return Rejected("Only a successful task can change Mars campaign progress.");

            switch (result.MinigameId)
            {
                case MarsAntennaAlignmentMinigame.IdValue: return CommitAntenna(stage);
                case MarsArrivalUplinkMinigame.IdValue: return CommitArrival(stage, result);
                case MarsOrbiterBenchMinigame.IdValue: return Rejected("Assemble the real modules, run the bench test, and load the orbiter at their Mars stations. The standalone practice task cannot skip these steps.");
                case MarsDustDiaryMinigame.IdValue: return CommitDust(stage);
                case MarsPacketPracticeMinigame.IdValue: return CommitPractice(stage, result);
                default: return Rejected("Unknown Mars task: " + result.MinigameId);
            }
        }

        private static MarsActionResult CommitAntenna(MarsStageController stage)
        {
            if (stage.Phase != MarsPhase.AntennaRecovery)
                return Already("Mars antenna recovery was already completed.");

            for (int i = 0; i < 12 && stage.Phase == MarsPhase.AntennaRecovery; i++)
            {
                MarsStageContext state = stage.Context;
                float error = state.antennaTarget - state.antennaAngle;
                float direction = Math.Abs(error) <= 5f ? 0f : Math.Sign(error);
                stage.TickManualAlignment(direction, direction == 0f ? 1f : Math.Min(1f, Math.Abs(error) / 55f));
            }
            return stage.Phase == MarsPhase.ArrivalPackets
                ? Applied("Mars antenna locked manually. Arrival uplink is available.")
                : Rejected("The antenna task finished, but the campaign alignment could not be committed.");
        }

        private static MarsActionResult CommitArrival(MarsStageController stage, MinigameResult result)
        {
            if (stage.Phase != MarsPhase.ArrivalPackets)
                return Already("Mars arrival uplink was already completed.");

            bool archive = OutputBool(result, "archiveDelivered") && stage.Context.routeUploaded;
            if (archive && !Contains(stage.Context.completedPackets, "pole-7-archive"))
            {
                if (!Contains(stage.Context.packetQueue, "pole-7-archive")) Apply(stage, MarsActionKind.QueueArchive);
                Apply(stage, MarsActionKind.SendNextPacket);
            }

            if (!Contains(stage.Context.completedPackets, "arrival-update"))
            {
                if (!Contains(stage.Context.packetQueue, "arrival-update")) Apply(stage, MarsActionKind.QueueArrival);
                Apply(stage, MarsActionKind.SendNextPacket);
                if (!Contains(stage.Context.completedPackets, "arrival-update") && stage.Context.contactSecondsLeft <= .001f)
                {
                    Apply(stage, MarsActionKind.StartContactWindow);
                    Apply(stage, MarsActionKind.SendNextPacket);
                }
            }

            return Contains(stage.Context.completedPackets, "arrival-update")
                ? Applied("Required arrival update delivered. Mars landing is unlocked.")
                : Rejected("The arrival update was not committed to the campaign queue.");
        }

        private static MarsActionResult CommitDust(MarsStageController stage)
        {
            if (stage.Phase != MarsPhase.MarsSurface) return Rejected("The Dust Diary is available on the Mars surface.");
            if (stage.Context.dustDiaryComplete) return Already("Dust Diary was already saved.");
            return stage.Act(MarsActionKind.DustAnswerAfternoon);
        }

        private static MarsActionResult CommitPractice(MarsStageController stage, MinigameResult result)
        {
            if (stage.Phase != MarsPhase.MarsSurface) return Rejected("Packet practice is available on the Mars surface.");
            if (stage.Context.practicePassed) return Already("Packet practice was already completed.");
            if (!Apply(stage, MarsActionKind.PracticeClear) || !Apply(stage, MarsActionKind.PracticeQueueHealth) || !Apply(stage, MarsActionKind.PracticeQueueSample)) return Failed(stage);
            if (OutputBool(result, "graphDelivered")) Apply(stage, MarsActionKind.PracticeQueueGraph);
            if (!Apply(stage, MarsActionKind.PracticeSend)) return Failed(stage);
            return stage.Context.practicePassed
                ? Applied("Packet-priority practice saved to the Mars campaign.")
                : Failed(stage);
        }

        private static bool Apply(MarsStageController stage, MarsActionKind kind)
        {
            MarsActionResult result = stage.Act(kind);
            return result.status != MarsActionStatus.Rejected;
        }

        private static MarsActionResult Failed(MarsStageController stage) => Rejected(stage.Notice);
        private static MarsActionResult Applied(string message) => new MarsActionResult { status = MarsActionStatus.Applied, message = message };
        private static MarsActionResult Already(string message) => new MarsActionResult { status = MarsActionStatus.AlreadyApplied, message = message };
        private static MarsActionResult Rejected(string message) => new MarsActionResult { status = MarsActionStatus.Rejected, message = message };
        private static bool Contains(string[] values, string value) => Array.IndexOf(values ?? Array.Empty<string>(), value) >= 0;
        private static string Output(MinigameResult result, string key, string fallback) =>
            result.OutputValues != null && result.OutputValues.TryGetValue(key, out string value) ? value : fallback;
        private static bool OutputBool(MinigameResult result, string key) =>
            bool.TryParse(Output(result, key, "False"), out bool value) && value;
    }
}
