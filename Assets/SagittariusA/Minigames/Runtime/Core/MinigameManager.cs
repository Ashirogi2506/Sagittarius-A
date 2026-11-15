using System;
using System.Linq;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SagittariusA.Minigames
{
    // Single entry point for the game flow: StartMinigame(context, onCompleted).
    // Knows nothing about individual minigame rules; types are found through MinigameRegistry.
    public sealed class MinigameManager : MonoBehaviour
    {
        private static MinigameManager instance;
        public static MinigameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<MinigameManager>();
                    if (instance == null) instance = new GameObject("MinigameManager").AddComponent<MinigameManager>();
                }
                return instance;
            }
        }

        public event Action<MinigameContext> Started;
        public event Action<MinigameResult> Finished;
        public bool IsActive => session != null && session.IsActive;
        public MinigameRegistry Registry => registry ??= MinigameRegistry.FromAssemblies(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName.StartsWith("Unity") && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("mscorlib") && !a.FullName.StartsWith("Mono.") && !a.FullName.StartsWith("nunit") && !a.GetName().Name.Contains(".Tests")).ToArray());

        private MinigameRegistry registry;
        private MinigameSession session;
        private MinigameInputLock inputLock;
        private MinigameShellView shell;
        private AudioSource audioSource;

        private sealed class UnityLog : IMinigameLog
        {
            public void Error(string m) => Debug.LogError("[Minigames] " + m);
            public void Warning(string m) => Debug.LogWarning("[Minigames] " + m);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            inputLock = new MinigameInputLock();
            session = new MinigameSession(inputLock, new UnityLog());
            session.GameReportedResult += ShowResult;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.ignoreListenerPause = true;
            MinigameAudio.Bind(audioSource);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (instance == this) { if (IsActive) session.Cancel("The minigame was closed."); instance = null; }
        }

        // Starts a minigame. onCompleted receives exactly one result, after pause and input have been restored.
        public void StartMinigame(MinigameContext context, Action<MinigameResult> onCompleted)
        {
            if (session == null) Awake();
            session.Start(context, Registry, Create, result =>
            {
                CleanupShell();
                try { Finished?.Invoke(result); } catch (Exception e) { Debug.LogException(e); }
                onCompleted?.Invoke(result);
            });
            if (session.IsActive) Started?.Invoke(context);
        }

        public void CancelActive(string reason = "Task cancelled.") { if (IsActive) session.Cancel(reason); }

        // Same as pressing Continue on the result panel (for automated flows and tests).
        public void ContinueFromResult() { if (IsActive && session.PendingResult != null) session.ConfirmResult(); }
        public bool ShowingResult => IsActive && session.PendingResult != null;
        public MinigameRegistry.Entry ActiveEntry => IsActive && Registry.TryResolve(session.Context.MinigameId, out var e) ? e : null;
        public IMinigame ActiveMinigame => IsActive ? session.Current : null;

        // Lets tests or tools register extra minigames without editing the manager.
        public void Register(string id, Type type, MinigameDefinition definition) => Registry.Register(id, type, definition);

        private IMinigame Create(MinigameRegistry.Entry entry)
        {
            if (typeof(MinigameBase).IsAssignableFrom(entry.Type))
            {
                float limit = MinigameSettings.ResolveTimeLimit(session.Context, entry.Definition);
                bool restart = entry.Type.GetProperty("AllowRestart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as bool? ?? true;
                shell = MinigameShellView.Create(entry.Definition, session.Context, limit > 0, restart);
                shell.CloseRequested += () => CancelActive("Closed by the player.");
                var game = (MinigameBase)shell.Content.gameObject.AddComponent(entry.Type);
                game.Attach(shell, entry.Definition);
                return game;
            }
            return (IMinigame)Activator.CreateInstance(entry.Type);
        }

        private void ShowResult(MinigameResult result)
        {
            if (shell != null) shell.ShowResult(result, () => session.ConfirmResult());
            else session.ConfirmResult();
        }

        private void Update()
        {
            if (!IsActive) return;
            session.Tick(Time.unscaledDeltaTime);
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsActive && session.Context.AllowCancel && session.PendingResult == null)
                CancelActive("Closed by the player.");
        }

        private void OnSceneChanged(Scene from, Scene to) { if (IsActive) session.Cancel("The scene changed during the minigame."); }

        private void CleanupShell()
        {
            if (shell == null) return;
            shell.gameObject.SetActive(false);
            Destroy(shell.gameObject);
            shell = null;
        }
    }
}
