using System;
using System.Collections.Generic;
using UnityEngine;
using MissionSimulation.Tutorial;

namespace MissionSimulation.Player
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInventory))]
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerControlSettings controls = new PlayerControlSettings();
        public Camera viewCamera;
        public LayerMask interactionLayers = ~0;
        public event Action<string> ActionCompleted;
        // UI controller subscribes and updates the inventory panel.
        public event Action InventoryOpenRequested;
        // Gameplay owner must handle item use and return true only when it succeeded.
        public Func<string, bool> UseItem;
        CharacterController character;
        PlayerInventory bag;
        float verticalSpeed, pitch;
        int selectedIndex;
        void Awake() { character = GetComponent<CharacterController>(); bag = GetComponent<PlayerInventory>(); }
        void Update()
        {
            if (controls == null || viewCamera == null) return;
            float x = (Input.GetKey(controls.right) ? 1 : 0) - (Input.GetKey(controls.left) ? 1 : 0);
            float z = (Input.GetKey(controls.forward) ? 1 : 0) - (Input.GetKey(controls.backward) ? 1 : 0);
            Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
            if (character.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += controls.gravity * Time.deltaTime;
            Vector3 before = transform.position;
            character.Move((move * controls.moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 displacement = transform.position - before; displacement.y = 0;
            if (move.sqrMagnitude > 0 && displacement.sqrMagnitude > 0.000001f) Report("move");
            float mouseX = Input.GetAxisRaw("Mouse X") * controls.mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * controls.mouseSensitivity;
            if (mouseX != 0 || mouseY != 0)
            {
                transform.Rotate(0, mouseX, 0); pitch = Mathf.Clamp(pitch - mouseY, -85f, 85f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0); Report("look");
            }
            if (Input.GetKeyDown(controls.inventory) && InventoryOpenRequested != null) InventoryOpenRequested();
            // UI must call NotifyInventoryOpened after it actually opens the panel.
            if (Input.GetKeyDown(controls.use))
            {
                var id = bag.GetItem(selectedIndex);
                if (id != null && UseItem != null && UseItem(id)) Report("use");
            }
            var target = FindTarget();
            if (target == null) return;
            if (Input.GetKeyDown(controls.interact) && target.TryInteract()) Report("interact");
            if (Input.GetKeyDown(controls.pickup) && target.TryPickup(bag)) Report("pickup");
            if (Input.GetKeyDown(controls.breakOpen) && target.TryBreakOpen()) Report("break");
        }
        PlayerWorldTarget FindTarget()
        {
            RaycastHit hit;
            if (!Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out hit, Mathf.Max(0, controls.interactionDistance), interactionLayers, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<PlayerWorldTarget>();
        }
        void Report(string id) { if (ActionCompleted != null) ActionCompleted(id); }
        public void NotifyInventoryOpened() { Report("inventory"); }
        public void SelectInventoryItem(int index) { selectedIndex = index; }
        public ControlLabel[] GetTutorialLabels()
        {
            return new[] {
                new ControlLabel { actionId="move", displayLabel=controls.forward+" / "+controls.left+" / "+controls.backward+" / "+controls.right },
                new ControlLabel { actionId="look", displayLabel="Mouse" },
                new ControlLabel { actionId="interact", displayLabel=controls.interact.ToString() },
                new ControlLabel { actionId="pickup", displayLabel=controls.pickup.ToString() },
                new ControlLabel { actionId="inventory", displayLabel=controls.inventory.ToString() },
                new ControlLabel { actionId="use", displayLabel=controls.use.ToString() },
                new ControlLabel { actionId="break", displayLabel=controls.breakOpen.ToString() }
            };
        }
    }
}
