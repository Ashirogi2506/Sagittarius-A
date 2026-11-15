using System;
using System.Collections.Generic;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEngine;

namespace SagittariusA.Gameplay.Mars
{
    public enum MarsPhase { AntennaRecovery, ArrivalPackets, MarsSurface, Complete }
    public enum MarsActionStatus { Applied, AlreadyApplied, Rejected }
    public enum MarsActionKind
    {
        RecordAntennaMiss, UseSpare, QueueArrival, QueueArchive, SendNextPacket, StartContactWindow, Land,
        SetStandardSensor, SetFrequentSensor, SelectLightAntenna, SelectDirectionalAntenna,
        SelectBasicShield, SelectReinforcedShield, SelectSensorPart, SelectAntennaPart, SelectShieldPart,
        PlaceSensorMount, PlaceAntennaMount, PlaceShieldMount, ConnectPower, ConnectData,
        BenchPowerOn, BenchSample, BenchTransmit, SeatOrbiter, CloseLeftClamp, CloseRightClamp,
        ReceiveDeploymentPlan, CompleteMars, DustAnswerMorning, DustAnswerAfternoon,
        PracticeClear, PracticeQueueHealth, PracticeQueueSample, PracticeQueueGraph, PracticeSend, ReturnOrbiter, PrioritizeArrival
    }

    [Serializable]
    public sealed class MarsAction
    {
        public string runId, operationId;
        public int expectedRevision;
        public MarsActionKind kind;
    }

    [Serializable]
    public sealed class MarsActionResult
    {
        public MarsActionStatus status;
        public string message;
        public bool Accepted { get { return status != MarsActionStatus.Rejected; } }
    }

    [Serializable]
    public sealed class MarsBenchGraph
    {
        public string title = "Mars Bench Test";
        public string xAxis = "Sample";
        public string yAxis = "Simulated particle count";
        public int[] samples = Array.Empty<int>();
        public string buildSignature;
    }

    [Serializable]
    public sealed class MarsDraftSave
    {
        public int version = 1;
        public int revision;
        public string runId, handoffJson;
        public MoonMarsHandoff handoff;
        public MarsPhase phase;
        public float antennaAngle = -38f, antennaTarget = 24f, antennaLockSeconds;
        public int antennaMisses;
        public bool showTarget, antennaLocked, spareSpent;
        public string antennaRecovery;
        public float contactSecondsLeft = 8f;
        public List<string> packetQueue = new List<string>();
        public List<string> completedPackets = new List<string>();
        public string sensorMode = "standard", antenna = "light", shield = "basic";
        public string selectedPart;
        public bool sensorInstalled, antennaInstalled, shieldInstalled;
        public bool powerConnected, dataConnected, benchPowered, sampleTaken, testPassed;
        public MarsBenchGraph benchGraph = new MarsBenchGraph();
        public bool orbiterSeated, loaderAtCradle, leftClamp, rightClamp, planReceived;
        public string deploymentPlan, firstContactSchedule;
        public bool dustDiaryComplete;
        public string dustSelectedTime;
        public int dustSelectedIndex;
        public List<string> practiceQueue = new List<string>();
        public List<string> practiceDelivered = new List<string>();
        public bool practicePassed;
        public List<string> appliedOperations = new List<string>();
    }

    [Serializable]
    public sealed class MarsStageContext
    {
        public string runId, commanderId, playerName;
        public MarsPhase phase;
        public int revision;
        public EarthBuild inventory;
        public float remainingFuelUnits;
        public float antennaAngle, antennaTarget, antennaLockSeconds, contactSecondsLeft;
        public int antennaMisses;
        public bool showTarget, antennaLocked, spareSpent;
        public string antennaRecovery;
        public string[] packetQueue, completedPackets;
        public string sensorMode, antenna, shield, selectedPart;
        public int massKg, sampleCount;
        public bool withinCapacity, sensorInstalled, antennaInstalled, shieldInstalled;
        public bool powerConnected, dataConnected, benchPowered, sampleTaken, testPassed;
        public MarsBenchGraph benchGraph;
        public bool orbiterSeated, loaderAtCradle, leftClamp, rightClamp, planReceived, canDepart;
        public string deploymentPlan, firstContactSchedule;
        public bool dustDiaryComplete;
        public string dustSelectedTime;
        public int dustSelectedIndex;
        public string[] practiceQueue, practiceDelivered;
        public bool practicePassed;
        public bool routeUploaded, relayRestored, beaconMarked, batteryLeft, usedLaser;
    }

    [Serializable]
    public sealed class MarsGanymedeHandoff
    {
        public string sourceRunId, missionId, nextMissionId, commanderId, playerName;
        public EarthBuild build;
        public float remainingFuelUnits;
        public int batteryKwh, supplyKw;
        public bool routeUploaded, relayRestored, beaconMarked, batteryLeft, usedLaser;
        public string antennaRecovery, sensorMode, antenna, shield;
        public int orbiterMassKg, sampleCount;
        public MarsBenchGraph benchGraph;
        public bool orbiterSeated, leftClamp, rightClamp, deploymentPlanReceived;
        public string deploymentPlan, firstContactSchedule;
        public bool dustDiaryComplete;
        public string dustSelectedTime;
        public int dustSelectedIndex;
        public bool practicePassed;
        public string[] practiceOrder;
    }

    public interface IMarsDraftStore
    {
        string Load();
        void Save(string json);
    }

    public sealed class PlayerPrefsMarsDraftStore : IMarsDraftStore
    {
        private const string Key = "FarSignal.MarsDraft.v1";
        public string Load() { return PlayerPrefs.GetString(Key, ""); }
        public void Save(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }

    public sealed class MemoryMarsDraftStore : IMarsDraftStore
    {
        private string json = "";
        public string Load() { return json; }
        public void Save(string value) { json = value ?? ""; }
        public string Json { get { return json; } }
    }
}
