using System;
using System.Collections.Generic;

namespace MissionSimulation.FarSignal
{
    // An advisory view of an existing campaign save. It never mutates or validates the save.
    // UI/gameplay decides how to present the result and navigate to the interaction.
    public enum CampaignNextAction
    {
        RestoreEarthCheckpoint, ConfirmLunarDescent, CheckLunarRoute, DeliverSupplies,
        RestoreRelay, UploadRoute, ReceiveMarsApproach, ReturnToShip,
        RecoverAntenna, SendMarsArrival, ChooseOrbiterModules, ConnectBenchCables,
        CompleteBenchTest, SeatOrbiter, SecureClamps, ReceiveDeploymentPlan,
        LandOnGanymede, RecordSurfaceSites, CompareReadings, ReturnToOrbit,
        DeployOrbiter, AlignAntenna, SendRequiredPackets, ReceiveEarthAcknowledgement,
        CampaignComplete
    }

    public sealed class CampaignResumeAdvice
    {
        public CampaignNextAction action;
        // Site IDs or packet IDs that are still required for the indicated action.
        public string[] remainingIds = Array.Empty<string>();
    }

    public static class CampaignResumeGuide
    {
        public static CampaignResumeAdvice GetNextAction(CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.lunarDescent == null) return Advice(CampaignNextAction.RestoreEarthCheckpoint);
            if (!state.lunarDescent.committed) return Advice(CampaignNextAction.ConfirmLunarDescent);
            if (!state.routeChecked) return Advice(CampaignNextAction.CheckLunarRoute);
            if (!state.suppliesDelivered) return Advice(CampaignNextAction.DeliverSupplies);
            if (!state.pole7RelayRestored) return Advice(CampaignNextAction.RestoreRelay);
            if (!state.routeUploaded) return Advice(CampaignNextAction.UploadRoute);
            if (!state.marsApproachReceived) return Advice(CampaignNextAction.ReceiveMarsApproach);
            if (!state.returnedToShip) return Advice(CampaignNextAction.ReturnToShip);
            if (!state.antennaRecovered) return Advice(CampaignNextAction.RecoverAntenna);
            if (!CampaignProgress.MarsArrivalConfirmed(state)) return Advice(CampaignNextAction.SendMarsArrival);

            if (!ValidModules(state)) return Advice(CampaignNextAction.ChooseOrbiterModules);
            if (!state.powerCableConnected || !state.dataCableConnected) return Advice(CampaignNextAction.ConnectBenchCables);
            if (!state.benchPassed) return Advice(CampaignNextAction.CompleteBenchTest);
            if (!state.seated) return Advice(CampaignNextAction.SeatOrbiter);
            if (!state.clamp1 || !state.clamp2) return Advice(CampaignNextAction.SecureClamps);
            if (!state.planReceived) return Advice(CampaignNextAction.ReceiveDeploymentPlan);
            if (!state.ganymedeLanded) return Advice(CampaignNextAction.LandOnGanymede);

            var missingSites = new List<string>();
            if (!HasSite(state, "crater-plain")) missingSites.Add("crater-plain");
            if (!HasSite(state, "grooved-rise")) missingSites.Add("grooved-rise");
            if (missingSites.Count > 0) return Advice(CampaignNextAction.RecordSurfaceSites, missingSites);
            if (!state.comparisonAccepted) return Advice(CampaignNextAction.CompareReadings);
            if (!state.returnedToOrbit) return Advice(CampaignNextAction.ReturnToOrbit);
            if (!state.deployed) return Advice(CampaignNextAction.DeployOrbiter);
            if (!state.antennaAligned) return Advice(CampaignNextAction.AlignAntenna);
            var missingPackets = new List<string>();
            if (state.receivedPackets == null || !state.receivedPackets.Contains("health")) missingPackets.Add("health");
            if (state.receivedPackets == null || !state.receivedPackets.Contains("sample")) missingPackets.Add("sample");
            if (missingPackets.Count > 0) return Advice(CampaignNextAction.SendRequiredPackets, missingPackets);
            if (!state.earthAcknowledged) return Advice(CampaignNextAction.ReceiveEarthAcknowledgement);
            return Advice(CampaignNextAction.CampaignComplete);
        }

        private static bool ValidModules(CampaignState state)
        {
            if (state.sensorMode != "standard" && state.sensorMode != "frequent") return false;
            try { return CampaignProgress.OrbiterMass(state.antenna, state.shield) <= CampaignProgress.CradleCapacityKg; }
            catch (ArgumentException) { return false; }
        }
        private static bool HasSite(CampaignState state, string siteId)
            => state.surveyRecords != null && state.surveyRecords.Exists(record => record != null && record.siteId == siteId);
        private static CampaignResumeAdvice Advice(CampaignNextAction action, List<string> remaining = null)
            => new CampaignResumeAdvice { action = action, remainingIds = remaining == null ? Array.Empty<string>() : remaining.ToArray() };
    }
}
