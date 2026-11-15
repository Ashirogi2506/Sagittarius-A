using System;
using System.Linq;
using System.Reflection;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.Editor
{
    // Explicit opt-in automation. Uses an isolated in-memory Moon save; never
    // writes the player's campaign. Also usable with -executeMethod in CI.
    [InitializeOnLoad]
    public static class CampaignTaskBriefingChecks
    {
        private const string AutomationKey = "FarSignal.CampaignTaskChecks";
        private static string expected;
        private static int phase;
        private static double deadline;
        static CampaignTaskBriefingChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(AutomationKey, false)) return;
                EditorApplication.delayCall += BeginPlayChecks;
            };
        }

        [MenuItem("Far Signal/Run Campaign Task Briefing Checks")]
        public static void CheckContent()
        {
            var state = new MoonStageContext { playerName = "Сайнбилэг" };
            var ids = typeof(MoonInteractionIds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()).ToArray();
            foreach (var id in ids)
            {
                var b = CampaignTaskBriefings.Moon(id, state);
                if (b == null || new[] { b.Title,b.Context,b.Purpose,b.Steps,b.Controls }.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException("Missing task explanation: " + id);
            }
            if (ids.Count(CampaignTaskBriefings.IsMoonMilestone) != 4)
                throw new InvalidOperationException("Moon should have only four automatic milestone explanations.");
            if (!CampaignTaskBriefings.Moon(MoonInteractionIds.ReturnToShip, state).Context.Contains(state.playerName))
                throw new InvalidOperationException("Mars invitation must preserve Unicode player name.");
            Debug.Log($"Campaign task content checks passed: {ids.Length} Moon interactions.");
        }

        [MenuItem("Far Signal/Run Full Campaign Task Automation")]
        public static void StartAutomation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before running the full suite.");
            CheckContent();
            SessionState.SetBool(AutomationKey,true);
            EditorSceneManager.OpenScene("Assets/Scenes/GameplayUI.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void BeginPlayChecks()
        {
            phase = 0;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += CheckDeadline;
            deadline = EditorApplication.timeSinceStartup + 120;
            try
            {
                CheckLayout();
                expected = "Moon repair panel checks passed:";
                MoonInputFlowChecks.RunPanels();
            }
            catch (Exception e) { Debug.LogException(e); Finish(false); }
        }

        private static void CheckLayout()
        {
            var state = new MoonStageContext { playerName = "Сайнбилэг" };
            foreach (var field in typeof(MoonInteractionIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
            {
                var view = TaskBriefingView.Open(null,CampaignTaskBriefings.Moon((string)field.GetRawConstantValue(),state),null);
                Canvas.ForceUpdateCanvases();
                foreach (var text in view.GetComponentsInChildren<Text>())
                    if (text.preferredHeight > text.rectTransform.rect.height + 2)
                        throw new InvalidOperationException("Briefing text clipped: " + view.Briefing.Id + " / " + text.text);
                view.Cancel();
            }
            Debug.Log("Campaign briefing layout checks passed: all Moon descriptions fit.");
        }

        private static void OnLog(string message,string stack,LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { Finish(false); return; }
            if (expected == null || !message.StartsWith(expected,StringComparison.Ordinal)) return;
            expected = null;
            EditorApplication.delayCall += Next;
        }
        private static void Next()
        {
            try
            {
                phase++; deadline = EditorApplication.timeSinceStartup + 180;
                if (phase == 1) { expected = "Moon mouse interaction flow passed:"; MoonInputFlowChecks.Run(); }
                else if (phase == 2) { expected = "Moon ridge surface flow checks passed:"; MoonSurfaceFlowChecks.RunRidge(); }
                else Finish(true);
            }
            catch (Exception e) { Debug.LogException(e); Finish(false); }
        }
        private static void CheckDeadline()
        {
            if (EditorApplication.timeSinceStartup <= deadline) return;
            Debug.LogError("Campaign task automation timed out while waiting for " + expected);
            Finish(false);
        }
        private static void Finish(bool success)
        {
            SessionState.SetBool(AutomationKey,false); expected = null;
            Application.logMessageReceived -= OnLog; EditorApplication.update -= CheckDeadline;
            if (success) Debug.Log("CAMPAIGN TASK AUTOMATION PASSED");
            EditorApplication.delayCall += () => { if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1); else EditorApplication.ExitPlaymode(); };
        }
    }
}
