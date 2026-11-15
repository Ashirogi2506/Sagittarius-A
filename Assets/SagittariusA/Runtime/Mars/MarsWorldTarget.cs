using UnityEngine;

namespace SagittariusA.Gameplay.Mars
{
    public sealed class MarsWorldTarget : MonoBehaviour
    {
        public enum PanelKind { Assembly, Bench, Loader, Weather, Practice, Airlock, PickSensor, PickAntenna, PickShield, MountSensor, MountAntenna, MountShield, PowerPort, DataPort, LeftClamp, RightClamp }
        [SerializeField] private PanelKind panel;
        [SerializeField] private string prompt;
        public PanelKind Panel { get { return panel; } }
        public string Prompt { get { return prompt; } }
        public bool IsPickup=>panel==PanelKind.PickSensor||panel==PanelKind.PickAntenna||panel==PanelKind.PickShield;
        public void Configure(PanelKind kind, string value) { panel = kind; prompt = value; }
    }
}
