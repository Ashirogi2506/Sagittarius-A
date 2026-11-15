using System.Linq;
using SagittariusA.Gameplay.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SagittariusA.Gameplay.Editor
{
    public static class GameplayUiSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GameplayUI.unity";

        [MenuItem("Far Signal/Open Main Game Scene", false, 0)]
        public static void OpenMainGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) BuildGameplayUiScene();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [InitializeOnLoadMethod]
        private static void QueueInitialSceneBuild()
        {
            EditorApplication.update -= BuildInitialSceneWhenReady;
            EditorApplication.update += BuildInitialSceneWhenReady;
        }

        private static void BuildInitialSceneWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= BuildInitialSceneWhenReady;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) BuildGameplayUiScene();
        }

        [MenuItem("Far Signal/Build Gameplay UI Scene")]
        public static void BuildGameplayUiScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before building the gameplay scene.");
                return;
            }
            Scene previous = SceneManager.GetActiveScene();
            // Unity rejects additive creation while any open scene is untitled.
            // Preserve named scenes; prompt before replacing an unsaved scene.
            bool keepPrevious = Enumerable.Range(0, SceneManager.sceneCount)
                .All(i => !string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
            if (!keepPrevious && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            keepPrevious = Enumerable.Range(0, SceneManager.sceneCount)
                .All(i => !string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                keepPrevious ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            // Runtime owns the UI so rebuilt scenes use the same interactive screens.
            new GameObject("GameplayFlow", typeof(GameFlowController));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(
                    new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            if (keepPrevious)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            Debug.Log($"Created gameplay scene at {ScenePath}.");
        }
    }
}
