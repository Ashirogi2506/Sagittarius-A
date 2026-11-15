using System.Linq;
using SagittariusA.Minigames;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SagittariusA.Gameplay.Minigames
{
    // Safe test launcher (scene: Minigames/Scenes/Minigame_TestLauncher.unity).
    // Starts any registered minigame by ID with chosen difficulty, seed, time limit and cancel permission,
    // then shows the returned MinigameResult. Writes no saves and touches no campaign state.
    public sealed class MinigameTestLauncher : MonoBehaviour
    {
        private int difficulty = 1, seed = 12345;
        private bool timed = true, allowCancel = true;
        private Text settingsText, resultText;
        private string[] ids;
        private Transform launcherRoot;

        private void Start()
        {
            ids = MinigameManager.Instance.Registry.Entries.Select(e => e.Id).OrderBy(Order).ToArray();
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera"; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f, .055f, .095f);
            }
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var canvasGo = new GameObject("LauncherCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var root = Ui.Rect(canvasGo.transform, "Root", 0, 0, 1920, 1080); Ui.Stretch(root); launcherRoot = root;
            var panel = Ui.Panel(root, "Launcher", 160, 90, 1600, 900, MinigameTheme.Panel);
            var p = panel.transform;
            Ui.Label(p, "FARSIGNAL · MINIGAME TEST LAUNCHER", 48, 40, 1200, 24, 16, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(p, "Start any mission task by ID", 48, 68, 1200, 50, 38, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Button first = null;
            for (int i = 0; i < ids.Length; i++)
            {
                int index = i;
                var entry = MinigameManager.Instance.Registry.Entries.First(e => e.Id == ids[i]);
                var b = Ui.Button(p, $"{entry.Definition.MissionLabel}: {entry.Definition.Title}", 48, 150 + i * 84, 760, 70, () => ShowBriefing(index), Ui.ButtonStyle.Secondary, 21);
                if (first == null) first = b;
            }
            Ui.Label(p, "SETTINGS", 860, 150, 300, 22, 16, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            settingsText = Ui.Label(p, "", 860, 178, 690, 30, 20, MinigameTheme.WarmWhite);
            Ui.Button(p, "Difficulty -", 860, 220, 160, 56, () => { difficulty = Mathf.Max(1, difficulty - 1); Show(); });
            Ui.Button(p, "Difficulty +", 1030, 220, 160, 56, () => { difficulty = Mathf.Min(3, difficulty + 1); Show(); });
            Ui.Button(p, "New seed", 1200, 220, 160, 56, () => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; seed %= 100000; Show(); });
            Ui.Button(p, "Time limit on/off", 860, 290, 250, 56, () => { timed = !timed; Show(); });
            Ui.Button(p, "Cancel allowed on/off", 1120, 290, 270, 56, () => { allowCancel = !allowCancel; Show(); });
            Ui.Label(p, "LAST RESULT", 860, 380, 300, 22, 16, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            resultText = Ui.Label(p, "No minigame run yet.", 860, 408, 690, 420, 18, MinigameTheme.WarmWhite);
            Show();
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private static int Order(string id) => id.StartsWith("earth") ? 0 : id.StartsWith("moon") ? 1 : id.StartsWith("mars") ? 2 : id.StartsWith("jupiter") ? 3 : 4;

        private void Show() => settingsText.text = $"Difficulty {difficulty}   ·   Seed {seed}   ·   {(timed ? "Timed" : "Untimed")}   ·   {(allowCancel ? "Cancel allowed" : "No cancel")}";

        private static string Briefing(string id) => id switch
        {
            "earth-image-calibration" => "Match the reference image by adjusting focus and exposure, then choose the correct spectral band. Confirm each calibration when the preview matches.",
            "moon-power-routing" => "Connect power sources to the three critical systems. Select adjacent grid points to lay cable, avoid blocked terrain, and energize the grid when ready.",
            "mars-power-management" => "Keep communications, thermal control and navigation powered through the dust storm. Switch off nonessential systems when solar power falls.",
            "jupiter-shield-allocation" => "Protect the spacecraft before the radiation surge. Select a shield, then select a system; match each risk level while staying within the mass budget.",
            _ => "Read the mission instructions and complete the task."
        };

        private void ShowBriefing(int index)
        {
            var entry = MinigameManager.Instance.Registry.Entries.First(e => e.Id == ids[index]);
            var previous = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var backdrop = Ui.Panel(launcherRoot, "BriefingBackdrop", 0, 0, 1920, 1080, MinigameTheme.Backdrop, null, true);
            Ui.Stretch(backdrop.rectTransform);
            var panel = Ui.Panel(backdrop.transform, "Briefing", 400, 270, 1120, 540, MinigameTheme.Panel, null, true);
            Ui.Label(panel.transform, "MISSION BRIEFING", 48, 36, 1000, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, entry.Definition.Title, 48, 80, 1024, 60, 36, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, Briefing(entry.Id), 48, 166, 1024, 190, 25, MinigameTheme.WarmWhite);
            Ui.Label(panel.transform, "The timer starts when you select Start.", 48, 375, 900, 30, 18, MinigameTheme.SoftGray);
            Ui.Button(panel.transform, "Back", 48, 450, 200, 58, () => {
                Destroy(backdrop.gameObject);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previous);
            });
            var start = Ui.Button(panel.transform, "Start", 850, 450, 220, 58, () => {
                Destroy(backdrop.gameObject);
                Launch(index);
            }, Ui.ButtonStyle.Primary);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(start.gameObject);
        }

        private void Launch(int index)
        {
            var entry = MinigameManager.Instance.Registry.Entries.First(e => e.Id == ids[index]);
            var context = new MinigameContext {
                MinigameId = entry.Id, MissionId = entry.Definition.MissionId, EventId = "test-launcher",
                DifficultyLevel = difficulty, RandomSeed = seed, TimeLimitSeconds = timed ? -1f : 0f, AllowCancel = allowCancel };
            MinigameGameFlowAdapter.Run(context, r =>
            {
                resultText.text =
                    $"{r.MinigameId}\nOutcome: {r.Outcome}\nScore: {r.Score}   Mistakes: {r.MistakeCount}   Time: {r.CompletionTimeSeconds:0.0} s   Complete: {r.CompletionPercent * 100:0}%\n\n{r.Summary}\n\n" +
                    "Suggested effects:\n" + (r.SuggestedEffects.Count == 0 ? "none" : string.Join("\n", r.SuggestedEffects.Select(e => $"- {e.EffectType} {e.Value:+0.##;-0.##}  ({e.Explanation})")));
            });
        }
    }
}
