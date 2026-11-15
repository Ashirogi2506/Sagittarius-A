using SagittariusA.Gameplay.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SagittariusA.EditorTools.Mars
{
    public static class MarsStandaloneMenu
    {
        [MenuItem("Far Signal/Mars/Play Standalone Mars Test")]
        public static void PlayStandalone()
        {
            ChapterPlayLauncher.Play("mars");
        }
    }
}
