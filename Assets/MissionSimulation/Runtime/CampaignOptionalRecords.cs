using System;

namespace MissionSimulation.FarSignal
{
    // Player-produced, fictional records. Never derive radiation dose from particle counts.
    public static class CampaignOptionalRecords
    {
        public static bool SaveBenchGraph(CampaignState state, int[] simulatedParticleCounts)
        {
            if (state == null || state.deployed || !state.benchPassed || !state.powerCableConnected || !state.dataCableConnected || simulatedParticleCounts == null) return false;
            int expected = state.sensorMode == "standard" ? 6 : state.sensorMode == "frequent" ? 12 : 0;
            if (simulatedParticleCounts.Length != expected || expected == 0) return false;
            foreach (int sample in simulatedParticleCounts) if (sample < 0) return false;
            state.marsBenchSamples = (int[])simulatedParticleCounts.Clone();
            state.marsBenchSensorMode = state.sensorMode;
            return true;
        }

        // The practice link uses a fixed 10-second window, independent of the final antenna.
        // Only a passing complete queue is kept as the editable first-contact suggestion.
        public static bool SavePassingPracticeQueue(CampaignState state, string[] packetIds)
        {
            if (state == null || state.ganymedeLanded || packetIds == null || packetIds.Length != 3) return false;
            bool health = false, sample = false, graph = false; int left = 10;
            foreach (string id in packetIds)
            {
                int duration;
                if (id == "health" && !health) { health = true; duration = 2; }
                else if (id == "sample" && !sample) { sample = true; duration = 4; }
                else if (id == "full-graph" && !graph) { graph = true; duration = 9; }
                else return false;
                if (duration <= left) left -= duration;
                else break;
            }
            if (!health || !sample) return false;
            // Validate the entire input, including items remaining after the contact closes.
            if (Array.IndexOf(packetIds, "health") < 0 || Array.IndexOf(packetIds, "sample") < 0 || Array.IndexOf(packetIds, "full-graph") < 0) return false;
            state.marsPracticeQueue = (string[])packetIds.Clone();
            return true;
        }

        public static string[] SuggestedFirstContactQueue(CampaignState state)
        {
            if (state != null && state.marsPracticeQueue != null)
            {
                var candidate = new CampaignState();
                if (SavePassingPracticeQueue(candidate, state.marsPracticeQueue)) return (string[])candidate.marsPracticeQueue.Clone();
            }
            return new[] { "health", "sample", "full-graph" };
        }

        public static bool SaveTerrainPhoto(CampaignState state, string imageId, string darkSiteId, string brightSiteId)
        {
            if (state == null || !state.ganymedeLanded || state.returnedToOrbit || string.IsNullOrWhiteSpace(imageId) || darkSiteId != "crater-plain" || brightSiteId != "grooved-rise") return false;
            if (!string.IsNullOrEmpty(state.terrainPhotoId))
                return state.terrainPhotoId == imageId && state.terrainPhotoDarkSiteId == darkSiteId && state.terrainPhotoBrightSiteId == brightSiteId;
            state.terrainPhotoId = imageId; state.terrainPhotoDarkSiteId = darkSiteId; state.terrainPhotoBrightSiteId = brightSiteId;
            return true;
        }
    }
}
