using System;
using System.IO;
using SagittariusA.Gameplay.Flow;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SagittariusA.Gameplay.Editor
{
    public static class ReleaseBuilder
    {
        [MenuItem("Far Signal/Build Windows Release")]
        public static void Build()
        {
            try
            {
                // Release export only. Validation is run separately when requested.
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("GameplayFlow",typeof(GameFlowController));
                const string path="Assets/Scenes/Release.unity";EditorSceneManager.SaveScene(scene,path);
                PlayerSettings.companyName="Sagittarius-A";PlayerSettings.productName="Far Signal";PlayerSettings.bundleVersion="1.0.0-rc1";
                PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.runInBackground=true;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                var output=Path.GetFullPath("../outputs/FarSignal-Windows/FarSignal.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
                File.WriteAllText(Path.GetFullPath("../outputs/build-result.txt"),"Far Signal 1.0.0-rc1\nWindows x64 build succeeded.\nNo runtime checks performed for this export, at user request.\nBytes: "+report.summary.totalSize+"\n");
                Debug.Log("FARSIGNAL WINDOWS BUILD SUCCEEDED "+output);if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        public static void PlayCheck()
        {EditorSceneManager.OpenScene("Assets/Scenes/GameplayUI.unity");SagittariusA.EditorTools.Mars.MarsPlayChecks.Run();}
    }
}
