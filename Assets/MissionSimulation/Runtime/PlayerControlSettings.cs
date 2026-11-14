using System;
using UnityEngine;

namespace MissionSimulation.Player
{
    // Editable on the player in the Inspector. These are starter keys, not final team bindings.
    [Serializable] public class PlayerControlSettings
    {
        public KeyCode forward = KeyCode.W, backward = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
        public KeyCode interact = KeyCode.E, pickup = KeyCode.F, inventory = KeyCode.Tab;
        public KeyCode use = KeyCode.Q, breakOpen = KeyCode.R;
        public float moveSpeed = 4f, gravity = -20f, mouseSensitivity = 2f;
        public float interactionDistance = 3f;
    }
}
