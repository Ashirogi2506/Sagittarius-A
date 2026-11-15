using SagittariusA.Gameplay.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SagittariusA.EditorTools.Ganymede
{
    public static class GanymedeStandaloneMenu
    {
        [MenuItem("Far Signal/Ganymede/Play Standalone Ganymede Test")]
        public static void PlayStandalone()
        {
            ChapterPlayLauncher.Play("ganymede");
        }
    }
}
