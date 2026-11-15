using UnityEngine;
using UnityEngine.UI;
using SagittariusA.Gameplay.Flow;

namespace SagittariusA.Gameplay.UI
{
    public sealed class GameScreenView : MonoBehaviour
    {
        [SerializeField] private GameScreen screen;
        [SerializeField] private Button primaryButton;
        [SerializeField] private GameScreen primaryTarget;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private GameScreen secondaryTarget;

        private GameFlowController flow;

        public GameScreen Screen => screen;

        public void Configure(
            GameScreen configuredScreen,
            Button configuredPrimaryButton,
            GameScreen configuredPrimaryTarget,
            Button configuredSecondaryButton,
            GameScreen configuredSecondaryTarget)
        {
            UnbindButtons();
            screen = configuredScreen;
            primaryButton = configuredPrimaryButton;
            primaryTarget = configuredPrimaryTarget;
            secondaryButton = configuredSecondaryButton;
            secondaryTarget = configuredSecondaryTarget;

            if (flow == null)
            {
                flow = GetComponentInParent<GameFlowController>();
            }

            BindButtons();
        }

        private void Awake()
        {
            flow = GetComponentInParent<GameFlowController>();
            BindButtons();
        }

        private void BindButtons()
        {
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(ShowPrimaryTarget);
                primaryButton.onClick.AddListener(ShowPrimaryTarget);
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveListener(ShowSecondaryTarget);
                secondaryButton.onClick.AddListener(ShowSecondaryTarget);
            }
        }

        private void UnbindButtons()
        {
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(ShowPrimaryTarget);
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveListener(ShowSecondaryTarget);
            }
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void ShowPrimaryTarget()
        {
            flow.Show(primaryTarget);
        }

        private void ShowSecondaryTarget()
        {
            flow.Show(secondaryTarget);
        }
    }
}
