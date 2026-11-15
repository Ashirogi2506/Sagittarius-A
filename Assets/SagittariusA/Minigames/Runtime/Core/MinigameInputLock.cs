using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SagittariusA.Minigames
{
    // Saves and restores everything a minigame touches outside itself:
    // Time.timeScale, 3D-input behaviours, PlayerInput action maps (UI map stays on), cursor state, UI selection and the EventSystem.
    public sealed class MinigameInputLock : IMinigameEnvironment
    {
        // Behaviours that read 3D/player input directly (for example first-person controllers). Registered by the game flow adapter.
        private static readonly List<Func<IEnumerable<Behaviour>>> blockerProviders = new List<Func<IEnumerable<Behaviour>>>();
        public static void RegisterBlocker(Func<IEnumerable<Behaviour>> provider) { if (provider != null && !blockerProviders.Contains(provider)) blockerProviders.Add(provider); }
        public static void RegisterBlockerType<T>() where T : Behaviour =>
            RegisterBlocker(() => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        public static void ClearBlockers() => blockerProviders.Clear();

        public bool IsCaptured { get; private set; }
        private float timeScale = 1f;
        private CursorLockMode cursorLock; private bool cursorVisible;
        private GameObject previousSelection;
        private GameObject createdEventSystem;
        private readonly List<Behaviour> disabled = new List<Behaviour>();
        private readonly List<InputActionMap> disabledMaps = new List<InputActionMap>();

        public void Capture(MinigameDefinition definition)
        {
            if (IsCaptured) return;
            IsCaptured = true;
            timeScale = Time.timeScale;
            if (definition == null || definition.PauseWorld) Time.timeScale = 0f;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;

            var es = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                createdEventSystem = new GameObject("MinigameEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                createdEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                Debug.LogWarning("[Minigames] No EventSystem found. A temporary one was created for the minigame.");
            }
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            foreach (var provider in blockerProviders)
            {
                IEnumerable<Behaviour> found;
                try { found = provider(); } catch (Exception e) { Debug.LogWarning("[Minigames] Input blocker provider failed: " + e.Message); continue; }
                if (found == null) continue;
                foreach (var b in found) if (b != null && b.enabled && !disabled.Contains(b)) { b.enabled = false; disabled.Add(b); }
            }
            foreach (var player in PlayerInput.all)
            {
                if (player == null || player.actions == null) continue;
                foreach (var map in player.actions.actionMaps)
                    if (map.enabled && !string.Equals(map.name, "UI", StringComparison.OrdinalIgnoreCase)) { map.Disable(); disabledMaps.Add(map); }
            }
            // Behaviours disabled above may have changed the cursor in OnDisable; the minigame needs a free cursor.
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Restore()
        {
            if (!IsCaptured) return;
            IsCaptured = false;
            foreach (var map in disabledMaps) if (map != null) map.Enable();
            disabledMaps.Clear();
            foreach (var b in disabled) if (b != null) b.enabled = true;   // destroyed during a scene change -> skipped
            disabled.Clear();
            Time.timeScale = timeScale;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            if (createdEventSystem != null) { UnityEngine.Object.Destroy(createdEventSystem); createdEventSystem = null; }
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
        }
    }
}
