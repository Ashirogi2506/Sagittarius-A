using System;
using SagittariusA.Gameplay.Flow;
using UnityEngine;

namespace SagittariusA.Gameplay.Mars
{
    // Thin campaign adapter around the rule model. A scene may subscribe to
    // Changed and render Context without owning any save or validation rules.
    public sealed class MarsStageController : MonoBehaviour
    {
        private MarsMission mission;
        public bool Initialized { get { return mission != null; } }
        public MarsPhase Phase { get { return mission == null ? MarsPhase.AntennaRecovery : mission.State.phase; } }
        public string Notice { get { return mission == null ? "Mars controller not initialized." : mission.Notice; } }
        public MarsStageContext Context { get { return mission == null ? null : mission.Context(); } }
        public MarsMission Mission { get { return mission; } }
        public event Action Changed;
        public event Action MarsStageCompleted;

        public bool Initialize(MoonMarsHandoff handoff, IMarsDraftStore store = null)
        {
            try
            {
                if (mission != null) mission.Changed -= OnMissionChanged;
                mission = new MarsMission(handoff, store ?? new PlayerPrefsMarsDraftStore());
                mission.Changed += OnMissionChanged;
                Changed?.Invoke();
                return true;
            }
            catch (ArgumentException error)
            {
                Debug.LogError(error.Message);
                mission = null;
                Changed?.Invoke();
                return false;
            }
        }

        public bool TickManualAlignment(float direction, float deltaTime)
        { return mission != null && mission.TickManualAlignment(direction, deltaTime); }

        public MarsActionResult Act(MarsActionKind kind)
        {
            if (mission == null) return new MarsActionResult { status = MarsActionStatus.Rejected, message = "Mars controller not initialized." };
            MarsActionResult result = mission.Act(kind);
            if (result.status == MarsActionStatus.Applied && mission.State.phase == MarsPhase.Complete) MarsStageCompleted?.Invoke();
            return result;
        }

        public MarsGanymedeHandoff GetGanymedeHandoff()
        { return mission == null ? null : mission.GetGanymedeHandoff(); }

        private void OnMissionChanged() { Changed?.Invoke(); }
        private void OnDestroy() { if (mission != null) mission.Changed -= OnMissionChanged; }
    }
}
