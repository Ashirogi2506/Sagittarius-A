using UnityEngine;
using UnityEngine.InputSystem;

namespace SagittariusA.Gameplay.Flow
{
    // Moon player movement and world interaction. Keys are provisional until the team
    // connects the shared rebinding settings and InputAction asset.
    [RequireComponent(typeof(CharacterController))]
    public sealed class MoonFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private MoonSceneConnector connector;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float mouseSensitivity = .1f;
        [SerializeField] private float interactionDistance = 3f;
        private CharacterController character;
        private float verticalSpeed, pitch;
        private MoonWorldTarget focused, held;

        public MoonWorldTarget FocusedTarget { get { return focused; } }
        public void Configure(Camera camera, MoonSceneConnector sceneConnector, LayerMask layers)
        { viewCamera = camera; connector = sceneConnector; interactionLayers = layers; }

        private void Awake() { character = GetComponent<CharacterController>(); }
        private void OnEnable() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        private void Update()
        {
            if (viewCamera == null || connector == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ReleaseHold();
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return;
            }
            Vector2 input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            if (character.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            character.Move((transform.right * input.x * moveSpeed + transform.forward * input.y * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            RaycastHit hit;
            focused = Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out hit,
                interactionDistance, interactionLayers, QueryTriggerInteraction.Collide)
                ? hit.collider.GetComponentInParent<MoonWorldTarget>() : null;
            if (held != null && (focused != held || !keyboard.eKey.isPressed)) ReleaseHold();
            string action = focused == null ? null : focused.AvailableId(connector);
            if (action == null) return;
            if (focused.RequiresHold && keyboard.eKey.isPressed)
            {
                if (held != focused) { ReleaseHold(); held = focused; }
                connector.HoldPatch(action, Time.deltaTime);
            }
            else if (!focused.RequiresHold && keyboard.eKey.wasPressedThisFrame)
                connector.Interact(action);
        }
        private void ReleaseHold() { if (held != null && connector != null) connector.EndHold(); held = null; }
        private void OnDisable() { ReleaseHold(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; focused = null; }
        private void OnApplicationFocus(bool focusedWindow) { if (!focusedWindow) ReleaseHold(); }
    }

}
