using System;
using System.Collections.Generic;

namespace MissionSimulation.FarSignal
{
    [Serializable] public sealed class SurveyRecord
    {
        public string siteId, imageId;
        public long missionTimestampSeconds;
        public int simulatedDoseRateUsvPerHour;
    }
    // Draft campaign values. Persist the complete state atomically after each successful mutation.
    [Serializable] public sealed class CampaignState
    {
        public string commanderName, antenna = "light", shield = "basic", sensorMode = "standard";
        public int spares;
        public bool antennaRecovered, usedSpare, benchPassed, seated, clamp1, clamp2, planReceived;
        public bool craterPlainSaved, groovedRiseSaved, comparisonAccepted, returnedToOrbit, deployed, antennaAligned, earthAcknowledged;
        public List<string> receivedPackets = new List<string>();
        public List<SurveyRecord> surveyRecords = new List<SurveyRecord>();
    }

    public sealed class CampaignProgress
    {
        public const int CradleCapacityKg = 300;
        public static int OrbiterMass(string antenna, string shield)
        {
            if (antenna != "light" && antenna != "directional") throw new ArgumentException("Select a valid antenna.");
            if (shield != "basic" && shield != "reinforced") throw new ArgumentException("Select a valid shield.");
            return 210 + (antenna == "light" ? 20 : 45) + (shield == "basic" ? 20 : 50);
        }
        public static bool SelectModules(CampaignState s, string antenna, string shield, string sensorMode)
        {
            if (s == null || s.deployed || (sensorMode != "standard" && sensorMode != "frequent")) return false;
            try { OrbiterMass(antenna, shield); } catch (ArgumentException) { return false; }
            if (s.antenna == antenna && s.shield == shield && s.sensorMode == sensorMode) return true;
            s.antenna = antenna; s.shield = shield; s.sensorMode = sensorMode;
            s.benchPassed = s.seated = s.clamp1 = s.clamp2 = s.planReceived = false;
            return true;
        }
        public static bool UseSpareForAntenna(CampaignState s)
        {
            if (s == null) return false;
            if (s.usedSpare) return true;
            if (s.antennaRecovered || s.spares < 1) return false;
            s.spares--; s.usedSpare = s.antennaRecovered = true; return true;
        }
        public static bool RecoverAntennaManually(CampaignState s)
        { if (s == null) return false; s.antennaRecovered = true; return true; }
        public static bool CanLoad(CampaignState s)
        {
            if (s == null || !s.benchPassed) return false;
            try { return OrbiterMass(s.antenna, s.shield) <= CradleCapacityKg; }
            catch (ArgumentException) { return false; }
        }
        public static bool CanTravel(CampaignState s) => CanLoad(s) && s.seated && s.clamp1 && s.clamp2 && s.planReceived;
        public static bool SaveReading(CampaignState s, string siteId, long missionTimestampSeconds, string imageId)
        {
            if (s == null || s.returnedToOrbit || missionTimestampSeconds < 0 || string.IsNullOrWhiteSpace(imageId)) return false;
            if (siteId != "crater-plain" && siteId != "grooved-rise") return false;
            if (s.surveyRecords == null) s.surveyRecords = new List<SurveyRecord>();
            if (s.surveyRecords.Exists(r => r != null && r.siteId == siteId)) return true;
            s.surveyRecords.Add(new SurveyRecord { siteId = siteId, imageId = imageId, missionTimestampSeconds = missionTimestampSeconds, simulatedDoseRateUsvPerHour = siteId == "crater-plain" ? 24 : 16 });
            if (siteId == "crater-plain") s.craterPlainSaved = true;
            else s.groovedRiseSaved = true;
            return true;
        }
        public static bool AcceptComparison(CampaignState s, string conclusion)
        {
            if (s == null || s.surveyRecords == null ||
                !s.surveyRecords.Exists(r => r != null && r.siteId == "crater-plain") ||
                !s.surveyRecords.Exists(r => r != null && r.siteId == "grooved-rise") ||
                conclusion != "site-b-lower-when-measured") return false;
            s.comparisonAccepted = true; return true;
        }
        public static bool ReturnToOrbit(CampaignState s)
        {
            if (s == null || !s.comparisonAccepted) return false;
            s.returnedToOrbit = true; return true;
        }
        public static bool Deploy(CampaignState s, bool crewReady, bool atReleasePoint)
        {
            if (s == null) return false;
            if (s.deployed) return true;
            if (!CanTravel(s) || !s.returnedToOrbit || !crewReady || !atReleasePoint) return false;
            s.deployed = true; s.antennaAligned = s.antenna == "light"; return true;
        }
        // Completed packets persist; a packet that doesn't fit remains queued for the next window.
        public static List<string> SendWindow(CampaignState s, IEnumerable<string> queue)
        {
            var sent = new List<string>();
            if (s == null || !s.deployed || !s.antennaAligned || queue == null) return sent;
            if (s.receivedPackets == null) s.receivedPackets = new List<string>();
            int left = s.antenna == "directional" ? 14 : 10;
            foreach (var id in queue)
            {
                if (s.receivedPackets.Contains(id)) continue;
                int duration = id == "health" ? 2 : id == "sample" ? 4 : id == "full-graph" ? (s.sensorMode == "frequent" ? 12 : 9) : -1;
                if (duration < 0 || duration > left) break;
                left -= duration; s.receivedPackets.Add(id); sent.Add(id);
            }
            return sent;
        }
        public static bool Acknowledge(CampaignState s)
        {
            if (s == null || s.receivedPackets == null || !s.receivedPackets.Contains("health") || !s.receivedPackets.Contains("sample")) return false;
            s.earthAcknowledged = true; return true;
        }
    }
}
