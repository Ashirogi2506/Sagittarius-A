using SagittariusA.Gameplay.Ganymede;
using SagittariusA.Gameplay.Mars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SagittariusA.Gameplay.Editor
{
    // Tests exist only in Play Mode. Stopping returns to the user's edit scene.
    [InitializeOnLoad]
    public static class ChapterPlayLauncher
    {
        private const string PendingKey = "FarSignal.ChapterPlay.Pending";
        private const string RestoreKey = "FarSignal.ChapterPlay.Restore";
        private const string PreviousKey = "FarSignal.ChapterPlay.PreviousScene";

        static ChapterPlayLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Play(string chapter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayUiSceneBuilder.ScenePath);
            if (scene == null)
            {
                Debug.LogError("Open or build the main game scene before starting a chapter test.");
                return;
            }
            SessionState.SetString(PreviousKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(RestoreKey, true);
            SessionState.SetString(PendingKey, chapter);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                string chapter = SessionState.GetString(PendingKey, "");
                SessionState.EraseString(PendingKey);
                if (!string.IsNullOrEmpty(chapter))
                    EditorApplication.delayCall += () => StartChapter(chapter);
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreKey, false))
            {
                string previous = SessionState.GetString(PreviousKey, "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                    ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.EraseString(PendingKey);
                SessionState.EraseString(PreviousKey);
                SessionState.EraseBool(RestoreKey);
            }
        }

        private static void StartChapter(string chapter)
        {
            if (!EditorApplication.isPlaying) return;
            if (chapter == "ganymede")
                new GameObject("Ganymede Standalone Test").AddComponent<GanymedePlayableExperience>().StartIsolatedTest();
            else if (chapter == "mars")
                new GameObject("Mars Standalone Test").AddComponent<MarsPlayableExperience>().StartIsolatedTest();
        }
    }
}
