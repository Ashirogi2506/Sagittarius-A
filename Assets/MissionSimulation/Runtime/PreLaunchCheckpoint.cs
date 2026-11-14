using System;
using System.Linq;
using MissionSimulation.Minigames;

namespace MissionSimulation.FarSignal
{
    [Serializable] public class CommanderIdentity
    {
        public string commanderId, playerName, portraitReference;
    }
    [Serializable] public class PreLaunchCheckpoint
    {
        public int version;
        public string runId, missionId;
        public CommanderIdentity commander;
        public EarthDesign design;
        public CargoPackingState cargo;
        public EarthPowerTestState power;
        public LandingPlan plan;
        public float fuelAtLaunchUnits, batteryAtLaunchKwh;
    }
    [Serializable] public class CheckpointResult
    {
        public bool success;
        public string explanation;
        public PreLaunchCheckpoint checkpoint;
        public LaunchReport validation;
    }

    // Pure data preparation: the campaign save owner controls storage, restore and scene transitions.
    public sealed class PreLaunchCheckpointService
    {
        const int CurrentVersion = 1;
        public CheckpointResult Create(string runId, CommanderIdentity commander, EarthScenario scenario, EarthDesign design, CargoPackingState cargo, EarthPowerTestState power, LandingPlan plan)
        {
            var result = new CheckpointResult();
            if (string.IsNullOrWhiteSpace(runId) || commander == null || string.IsNullOrWhiteSpace(commander.commanderId) || string.IsNullOrWhiteSpace(commander.playerName))
            { result.explanation = "A run and commander identity are required."; return result; }
            var report = new EarthLaunchChecklist().Validate(scenario, design, cargo, power, plan);
            result.validation = report;
            if (!report.CanLaunch) { result.explanation = "Complete the launch checklist before recording the pre-launch checkpoint."; return result; }
            result.checkpoint = new PreLaunchCheckpoint { version = CurrentVersion, runId = runId, missionId = scenario.missionId,
                commander = CopyCommander(commander), design = CopyDesign(design), cargo = CopyCargo(cargo), power = CopyPower(power), plan = CopyPlan(plan),
                fuelAtLaunchUnits = report.fuelUnits, batteryAtLaunchKwh = report.batteryKwh };
            result.success = true; result.explanation = "Pre-launch checkpoint prepared; the campaign must save it before launch."; return result;
        }
        public CheckpointResult Restore(string runId, EarthScenario scenario, PreLaunchCheckpoint saved)
        {
            var result = new CheckpointResult();
            if (saved == null || saved.version != CurrentVersion || string.IsNullOrWhiteSpace(runId) || saved.runId != runId || scenario == null || saved.missionId != scenario.missionId)
            { result.explanation = "The pre-launch checkpoint does not match this run or mission."; return result; }
            var verified = Create(saved.runId, saved.commander, scenario, saved.design, saved.cargo, saved.power, saved.plan);
            if (!verified.success || verified.checkpoint.fuelAtLaunchUnits != saved.fuelAtLaunchUnits || verified.checkpoint.batteryAtLaunchKwh != saved.batteryAtLaunchKwh)
            { result.explanation = "The checkpoint is incomplete, inconsistent, or incompatible with the current mission data."; result.validation = verified.validation; return result; }
            verified.explanation = "Pre-launch checkpoint verified; the campaign controller can restore it."; return verified;
        }
        static CommanderIdentity CopyCommander(CommanderIdentity x) { return new CommanderIdentity { commanderId=x.commanderId, playerName=x.playerName, portraitReference=x.portraitReference }; }
        static EarthDesign CopyDesign(EarthDesign x)
        {
            return new EarthDesign { launcherId=x.launcherId, nozzleId=x.nozzleId, fairingId=x.fairingId, engineId=x.engineId, cargoModuleId=x.cargoModuleId, powerSystemId=x.powerSystemId, landingPadId=x.landingPadId,
                cargo=x.cargo.Select(p => new CargoPlacement { cargoId=p.cargoId, startSlot=p.startSlot }).ToArray() };
        }
        static CargoPackingState CopyCargo(CargoPackingState x) { return new CargoPackingState { slotCount=x.slotCount, items=x.items.Select(p => new PackedCargo { id=p.id, startSlot=p.startSlot, locked=p.locked }).ToList() }; }
        static EarthPowerTestState CopyPower(EarthPowerTestState x) { return new EarthPowerTestState { powerSystemId=x.powerSystemId, cabinOn=x.cabinOn, radioOn=x.radioOn, laserOn=x.laserOn, laserTripped=x.laserTripped }; }
        static LandingPlan CopyPlan(LandingPlan x) { return new LandingPlan { padId=x.padId, hasArrivalMinute=x.hasArrivalMinute, arrivalMinute=x.arrivalMinute }; }
    }
}
