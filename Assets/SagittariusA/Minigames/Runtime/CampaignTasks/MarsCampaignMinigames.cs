using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SagittariusA.Minigames.CampaignTasks
{
    // Shared lifecycle for the short, full-screen Mars task panels.
    public abstract class MarsTaskMinigame : MinigameBase
    {
        protected float elapsed;
        protected int mistakes;
        protected bool solved;
        protected string summary = "Task incomplete.";

        protected override float Elapsed => elapsed;
        protected virtual float Progress => solved ? 1f : 0f;

        protected override void OnInitialize()
        {
            elapsed = 0f;
            mistakes = 0;
            solved = false;
            ResetTask();
        }

        protected override bool OnTick(float dt)
        {
            elapsed += dt;
            if (TimeLimit > 0f && elapsed >= TimeLimit)
            {
                summary = "Time expired. Retry this Mars task locally; campaign progress is unchanged.";
                return true;
            }
            TickTask(dt);
            return false;
        }

        protected virtual void TickTask(float dt) { }
        protected abstract void ResetTask();

        protected void CompleteTask(string message)
        {
            if (solved || !IsRunning) return;
            solved = true;
            summary = message;
            Feedback(message, FeedbackKind.Good);
            Refresh();
            Finish();
        }

        protected void Reject(string message)
        {
            mistakes++;
            Feedback(message, FeedbackKind.Bad);
        }

        protected override MinigameResult BuildResult()
        {
            var result = MinigameResult.For(Context, solved ? MinigameOutcome.Success : MinigameOutcome.Failure, summary);
            result.Score = solved ? MinigameScoring.Standard(mistakes, elapsed, TimeLimit) : Mathf.Max(0, Mathf.RoundToInt(50f * Progress) - mistakes * 5);
            result.CompletionTimeSeconds = elapsed;
            result.MistakeCount = mistakes;
            result.CompletionPercent = Progress;
            if (!string.IsNullOrWhiteSpace(ActiveDefinition?.EducationalNote))
                result.EducationalNotes.Add(ActiveDefinition.EducationalNote);
            AddOutputValues(result);
            return result.Normalize();
        }

        protected virtual void AddOutputValues(MinigameResult result) { }
    }

    [Minigame(IdValue)]
    public sealed class MarsAntennaAlignmentMinigame : MarsTaskMinigame
    {
        public const string IdValue = "mars-antenna-alignment";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars-staging", MissionLabel = "Mars approach", Title = "Find the Antenna Signal",
            Instruction = "Move the antenna into the target band and hold Lock for one second.",
            EducationalSubtitle = "A spare can automate this in the campaign; manual alignment must always remain possible.",
            DefaultTimeLimitSeconds = 50f, EducationalNote = "Directional links need accurate pointing before useful data can pass."
        };
        public override string Id => IdValue;

        private float aim;
        private float holdTime;
        private bool holding;
        private Slider aimSlider;
        private Text signalText, holdText;
        private Button holdButton;
        private RectTransform signalFill;
        private Image signalFillImage;

        protected override float Progress
        {
            get
            {
                float alignment = Mathf.Clamp01(1f - Mathf.Abs(aim - .68f) * 2.5f) * .55f;
                return Mathf.Clamp01(alignment + holdTime * .45f);
            }
        }

        protected override void ResetTask()
        {
            aim = .18f;
            holdTime = 0f;
            holding = false;
        }

        protected override void BuildContent(RectTransform content)
        {
            var panel = Ui.Panel(content, "Antenna", 170, 20, 1180, 570, MinigameTheme.Card);
            Ui.Label(panel.transform, "MARS APPROACH · HIGH-GAIN ANTENNA", 38, 28, 900, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, "Sweep the antenna until the signal enters the marked center band.", 38, 72, 1080, 40, 24, MinigameTheme.WarmWhite);

            aimSlider = Ui.Slider(panel.transform, "AntennaAim", 70, 174, 1040, 72, v => {
                aim = v;
                if (holding) { holding = false; holdTime = 0f; }
                Refresh();
            });
            aimSlider.SetValueWithoutNotify(aim);
            Ui.Label(panel.transform, "LEFT", 70, 250, 130, 24, 14, MinigameTheme.SoftGray);
            Ui.Label(panel.transform, "TARGET BAND", 455, 250, 270, 24, 14, MinigameTheme.Orange, TextAnchor.UpperCenter, FontStyle.Bold);
            Ui.Label(panel.transform, "RIGHT", 980, 250, 130, 24, 14, MinigameTheme.SoftGray, TextAnchor.UpperRight);

            signalText = Ui.Label(panel.transform, "", 70, 304, 1040, 42, 27, MinigameTheme.WarmWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            signalFill = Ui.Meter(panel.transform, "Signal", 180, 360, 820, 18, MinigameTheme.Good, out signalFillImage);
            holdButton = Ui.Button(panel.transform, "HOLD TO LOCK", 350, 414, 480, 76, null, Ui.ButtonStyle.Primary, 24);
            var relay = holdButton.gameObject.AddComponent<UiPointerRelay>();
            relay.Down = _ => BeginHold();
            relay.Up = _ => EndHold();
            relay.Submit = BeginHold;
            holdText = Ui.Label(panel.transform, "", 350, 504, 480, 26, 16, MinigameTheme.SoftGray, TextAnchor.UpperCenter);
            Feedback("Sweep slowly. The signal readout and label confirm when you are inside the target band.", FeedbackKind.Info);
        }

        private void BeginHold()
        {
            if (!IsRunning) return;
            if (Mathf.Abs(aim - .68f) > .08f)
            {
                Reject("Signal is outside the target band. Move the antenna toward the center before holding Lock.");
                holding = false;
                holdTime = 0f;
                Refresh();
                return;
            }
            holding = true;
            Feedback("Target acquired. Hold steady for one second.", FeedbackKind.Info);
        }

        private void EndHold()
        {
            if (!IsRunning || solved) return;
            holding = false;
            if (holdTime < 1f)
            {
                holdTime = 0f;
                Feedback("Lock released early. Hold for the full second.", FeedbackKind.Info);
            }
            Refresh();
        }

        protected override void TickTask(float dt)
        {
            if (!holding) return;
            if (Mathf.Abs(aim - .68f) > .08f)
            {
                holding = false;
                holdTime = 0f;
                Reject("The antenna moved out of band. Recenter it and try the hold again.");
                Refresh();
                return;
            }
            holdTime += dt;
            if (holdTime >= 1f)
            {
                holdTime = 1f;
                CompleteTask("Mars antenna locked. The arrival link is ready.");
                return;
            }
            Refresh();
        }

        protected override void OnRestart()
        {
            ResetTask();
            if (aimSlider != null) aimSlider.SetValueWithoutNotify(aim);
            Refresh();
        }

        protected override void OnCancelled() { holding = false; }

        protected override void AddOutputValues(MinigameResult result)
        {
            if (solved) result.OutputValues["recovery"] = "manual";
        }

        protected override void Refresh()
        {
            if (signalText == null) return;
            float error = Mathf.Abs(aim - .68f);
            float strength = Mathf.Clamp01(1f - error * 2f);
            bool inBand = error <= .08f;
            signalText.text = solved ? "SIGNAL LOCKED" : inBand ? "TARGET ACQUIRED" : $"SIGNAL STRENGTH {strength * 100f:0}%";
            signalText.color = solved || inBand ? MinigameTheme.Good : MinigameTheme.Warn;
            Ui.SetMeter(signalFill, strength);
            signalFillImage.color = inBand ? MinigameTheme.Good : MinigameTheme.Warn;
            holdText.text = holding ? $"LOCKING {holdTime * 100f:0}%" : inBand ? "Hold continuously for 1 second" : "Find the marked band first";
            Ui.SetButtonLabel(holdButton, holding ? "KEEP HOLDING" : solved ? "LOCKED" : "HOLD TO LOCK");
            aimSlider.interactable = IsRunning && !holding;
            holdButton.interactable = IsRunning;
            Shell.SetProgress(Progress, "Antenna lock");
        }
    }

    [Minigame(IdValue)]
    public sealed class MarsArrivalUplinkMinigame : MarsTaskMinigame
    {
        public const string IdValue = "mars-arrival-uplink";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars-staging", MissionLabel = "Mars approach", Title = "Send the Arrival Update",
            Instruction = "Put the required 3-second arrival update inside an 8-second contact window.",
            EducationalSubtitle = "The optional Pole-7 archive takes 7 seconds, so packet order matters.",
            DefaultTimeLimitSeconds = 60f, EducationalNote = "Communication windows reward sending essential information before optional data."
        };
        public override string Id => IdValue;

        private readonly List<string> queue = new List<string>();
        private bool arrivalDelivered;
        private bool archiveDelivered;
        private Text queueText, deliveryText, timeText;
        private Button transmit;
        protected override float Progress => (arrivalDelivered ? .8f : 0f) + (archiveDelivered ? .2f : 0f);
        protected override void ResetTask() { queue.Clear(); arrivalDelivered = archiveDelivered = false; }

        protected override void BuildContent(RectTransform content)
        {
            var panel = Ui.Panel(content, "Uplink", 170, 20, 1180, 570, MinigameTheme.Card);
            Ui.Label(panel.transform, "MARS ARRIVAL LINK · 8-SECOND WINDOW", 38, 28, 900, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, "Select packets in transmission order. Delivered packets stay delivered on retry.", 38, 68, 1080, 40, 21, MinigameTheme.WarmWhite);
            Ui.Button(panel.transform, "ARRIVAL UPDATE · 3 s · REQUIRED", 38, 132, 520, 72, () => Add("arrival"));
            Ui.Button(panel.transform, "POLE-7 ARCHIVE · 7 s · OPTIONAL", 38, 220, 520, 72, () => Add("archive"));
            Ui.Button(panel.transform, "CLEAR QUEUE", 38, 314, 220, 50, () => { queue.Clear(); Refresh(); }, Ui.ButtonStyle.Ghost, 16);
            timeText = Ui.Label(panel.transform, "", 38, 400, 520, 72, 23, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            queueText = Ui.Label(panel.transform, "", 620, 132, 520, 210, 21, MinigameTheme.WarmWhite);
            deliveryText = Ui.Label(panel.transform, "", 620, 350, 520, 80, 18, MinigameTheme.SoftGray);
            transmit = Ui.Button(panel.transform, "OPEN WINDOW + TRANSMIT", 620, 454, 520, 70, Transmit, Ui.ButtonStyle.Primary);
            Feedback("The archive is useful but optional. Mars cannot clear the landing until the arrival update is received.", FeedbackKind.Info);
        }

        private void Add(string id)
        {
            bool delivered = id == "arrival" ? arrivalDelivered : archiveDelivered;
            if (delivered || queue.Contains(id))
            {
                Feedback("That packet is already delivered or queued.", FeedbackKind.Info);
                return;
            }
            queue.Add(id);
            Refresh();
        }

        private void Transmit()
        {
            if (queue.Count == 0) { Reject("The queue is empty. Add the required arrival update."); return; }
            int used = 0;
            var sent = new List<string>();
            foreach (string id in queue)
            {
                int duration = id == "arrival" ? 3 : 7;
                if (used + duration > 8) break;
                used += duration;
                sent.Add(id);
                if (id == "arrival") arrivalDelivered = true; else archiveDelivered = true;
            }
            queue.RemoveAll(sent.Contains);
            if (arrivalDelivered)
            {
                CompleteTask(archiveDelivered
                    ? "Arrival update and Pole-7 archive delivered. Mars landing is cleared."
                    : "Required arrival update delivered. Mars landing is cleared; the archive can wait.");
                return;
            }
            Reject("The optional archive used the window. Move the 3-second arrival update to the front on the next pass.");
            Refresh();
        }

        protected override void OnRestart() { ResetTask(); Refresh(); }

        protected override void AddOutputValues(MinigameResult result)
        {
            result.OutputValues["arrivalDelivered"] = arrivalDelivered.ToString();
            result.OutputValues["archiveDelivered"] = archiveDelivered.ToString();
        }

        protected override void Refresh()
        {
            if (queueText == null) return;
            int queuedSeconds = queue.Sum(id => id == "arrival" ? 3 : 7);
            queueText.text = queue.Count == 0 ? "QUEUE EMPTY" : string.Join("\n", queue.Select((id, i) => $"{i + 1}. {(id == "arrival" ? "Arrival update · 3 s" : "Pole-7 archive · 7 s")}"));
            timeText.text = $"QUEUED {queuedSeconds} s\nWINDOW 8 s";
            timeText.color = queuedSeconds <= 8 ? MinigameTheme.Good : MinigameTheme.Warn;
            deliveryText.text = $"Arrival update: {(arrivalDelivered ? "RECEIVED" : "WAITING")}\nPole-7 archive: {(archiveDelivered ? "RECEIVED" : "OPTIONAL")}";
            transmit.interactable = IsRunning;
            Shell.SetProgress(Progress, "Mars arrival packets");
        }
    }

    [Minigame(IdValue)]
    public sealed class MarsOrbiterBenchMinigame : MarsTaskMinigame
    {
        public const string IdValue = "mars-orbiter-bench";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars-staging", MissionLabel = "Mars assembly bay", Title = "Assemble and Test the Orbiter",
            Instruction = "Stay under 300 kg, connect power and data, pass the bench test, then close both clamps.",
            EducationalSubtitle = "The sensor needs one cable for electricity and another for its readings.",
            DefaultTimeLimitSeconds = 100f, EducationalNote = "Engineering tests separate power, data, mass, and mechanical securement into independent checks."
        };
        public override string Id => IdValue;

        private bool directional, reinforced, powerCable, dataCable, tested, leftClamp, rightClamp;
        private Text massText, cableText, graphText, clampText;
        private Button light, direction, basic, reinforce, power, data, run, left, right, verify;
        private int Mass => 210 + (directional ? 45 : 20) + (reinforced ? 50 : 20);
        protected override float Progress => ((Mass <= 300 ? 1 : 0) + (powerCable ? 1 : 0) + (dataCable ? 1 : 0) + (tested ? 1 : 0) + (leftClamp ? 1 : 0) + (rightClamp ? 1 : 0)) / 6f;
        protected override void ResetTask() { directional = reinforced = powerCable = dataCable = tested = leftClamp = rightClamp = false; }

        protected override void BuildContent(RectTransform content)
        {
            var build = Ui.Panel(content, "Build", 0, 0, 760, 610, MinigameTheme.Card);
            Ui.Label(build.transform, "ORBITER MODULES", 30, 24, 680, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            massText = Ui.Label(build.transform, "", 30, 62, 680, 45, 28, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(build.transform, "ANTENNA", 30, 136, 200, 22, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            light = Ui.Button(build.transform, "LIGHT · 20 kg", 30, 168, 330, 68, () => SetAntenna(false));
            direction = Ui.Button(build.transform, "DIRECTIONAL · 45 kg", 380, 168, 350, 68, () => SetAntenna(true));
            Ui.Label(build.transform, "SHIELDING", 30, 264, 200, 22, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            basic = Ui.Button(build.transform, "BASIC · 20 kg", 30, 296, 330, 68, () => SetShield(false));
            reinforce = Ui.Button(build.transform, "REINFORCED · 50 kg", 380, 296, 350, 68, () => SetShield(true));
            Ui.Label(build.transform, "Common frame + power bus + sensor: 210 kg", 30, 414, 680, 28, 18, MinigameTheme.SoftGray);
            graphText = Ui.Label(build.transform, "", 30, 458, 680, 78, 20, MinigameTheme.WarmWhite);

            var test = Ui.Panel(content, "Test", 790, 0, 730, 610, MinigameTheme.Card);
            Ui.Label(test.transform, "BENCH + CRADLE", 28, 24, 670, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            power = Ui.Button(test.transform, "POWER CABLE", 28, 78, 325, 64, () => ToggleCable(true));
            data = Ui.Button(test.transform, "DATA CABLE", 375, 78, 325, 64, () => ToggleCable(false));
            cableText = Ui.Label(test.transform, "", 28, 154, 672, 60, 18, MinigameTheme.WarmWhite);
            run = Ui.Button(test.transform, "RUN BENCH TEST", 28, 224, 672, 68, RunTest, Ui.ButtonStyle.Primary);
            Ui.Label(test.transform, "CARGO CLAMPS", 28, 328, 250, 22, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            left = Ui.Button(test.transform, "LEFT CLAMP", 28, 362, 325, 62, () => Clamp(true));
            right = Ui.Button(test.transform, "RIGHT CLAMP", 375, 362, 325, 62, () => Clamp(false));
            clampText = Ui.Label(test.transform, "", 28, 438, 672, 50, 18, MinigameTheme.WarmWhite);
            verify = Ui.Button(test.transform, "VERIFY FOR DEPARTURE", 28, 510, 672, 70, Verify, Ui.ButtonStyle.Primary);
            Feedback("The basic 250 kg build is selected. Connect both cables before running the test.", FeedbackKind.Info);
        }

        private void SetAntenna(bool value) { directional = value; InvalidateTest("Antenna changed. Repeat the bench test and clamps."); }
        private void SetShield(bool value) { reinforced = value; InvalidateTest("Shield changed. Repeat the bench test and clamps."); }
        private void InvalidateTest(string message)
        {
            tested = leftClamp = rightClamp = false;
            Feedback(message, FeedbackKind.Info);
            Refresh();
        }
        private void ToggleCable(bool isPower)
        {
            if (isPower) powerCable = !powerCable; else dataCable = !dataCable;
            tested = leftClamp = rightClamp = false;
            Feedback(isPower ? $"Power cable {(powerCable ? "connected" : "removed")}." : $"Data cable {(dataCable ? "connected" : "removed")}.", FeedbackKind.Info);
            Refresh();
        }
        private void RunTest()
        {
            if (Mass > 300) { Reject($"{Mass - 300} kg over the cradle limit. Switch to the light antenna or basic shield."); return; }
            if (!powerCable) { Reject("The sensor has no power. Connect the power cable first."); return; }
            if (!dataCable) { Reject("The sensor is on, but its readings cannot reach the computer. Connect the data cable."); return; }
            tested = true;
            Feedback("Bench trace received and saved with the Mars test label.", FeedbackKind.Good);
            Refresh();
        }
        private void Clamp(bool isLeft)
        {
            if (!tested) { Reject("Pass the bench test before securing the orbiter in its cradle."); return; }
            if (isLeft) leftClamp = true; else rightClamp = true;
            Feedback($"{(isLeft ? "Left" : "Right")} clamp closed.", FeedbackKind.Good);
            Refresh();
        }
        private void Verify()
        {
            if (Mass > 300) { Reject($"{Mass - 300} kg over the cradle limit. Change one module."); return; }
            if (!tested) { Reject("Run a successful bench test before departure."); return; }
            if (!leftClamp || !rightClamp) { Reject($"Close the {(!leftClamp ? "left" : "right")} clamp before departure."); return; }
            CompleteTask($"Orbiter tested and secured at {Mass} kg. Deployment plan can continue.");
        }
        protected override void OnRestart() { ResetTask(); Refresh(); }
        protected override void AddOutputValues(MinigameResult result)
        {
            result.OutputValues["antenna"] = directional ? "directional" : "light";
            result.OutputValues["shield"] = reinforced ? "reinforced" : "basic";
            result.OutputValues["massKg"] = Mass.ToString();
        }
        protected override void Refresh()
        {
            if (massText == null) return;
            bool legal = Mass <= 300;
            massText.text = $"MASS {Mass} / 300 kg   ·   {(legal ? $"{300 - Mass} kg margin" : $"{Mass - 300} kg OVER")}";
            massText.color = legal ? MinigameTheme.Good : MinigameTheme.Bad;
            ((Image)light.targetGraphic).color = !directional ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised;
            ((Image)direction.targetGraphic).color = directional ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised;
            ((Image)basic.targetGraphic).color = !reinforced ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised;
            ((Image)reinforce.targetGraphic).color = reinforced ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised;
            Ui.SetButtonLabel(power, powerCable ? "POWER · CONNECTED" : "POWER CABLE");
            Ui.SetButtonLabel(data, dataCable ? "DATA · CONNECTED" : "DATA CABLE");
            cableText.text = $"Power: {(powerCable ? "CONNECTED" : "MISSING")}   ·   Data: {(dataCable ? "CONNECTED" : "MISSING")}";
            graphText.text = tested ? "MARS BENCH TEST\nTRACE SAVED · READY FOR CRADLE" : legal ? "No valid bench trace saved." : "Loading blocked until mass is within 300 kg.";
            Ui.SetButtonLabel(left, leftClamp ? "LEFT · CLOSED" : "LEFT CLAMP");
            Ui.SetButtonLabel(right, rightClamp ? "RIGHT · CLOSED" : "RIGHT CLAMP");
            clampText.text = $"Left: {(leftClamp ? "SECURE" : "OPEN")}   ·   Right: {(rightClamp ? "SECURE" : "OPEN")}";
            run.interactable = IsRunning;
            verify.interactable = IsRunning;
            Shell.SetProgress(Progress, "Orbiter readiness");
        }
    }

    [Minigame(IdValue)]
    public sealed class MarsDustDiaryMinigame : MarsTaskMinigame
    {
        public const string IdValue = "mars-dust-diary";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars-staging", MissionLabel = "Mars optional", Title = "Dust Diary",
            Instruction = "Match each ridge photo time to the chart and choose when the instrument logged more airborne dust.",
            EducationalSubtitle = "Pictures give a clue; the simulated instrument value checks it.",
            DefaultTimeLimitSeconds = 45f, EducationalNote = "A visual observation is stronger when it is paired with a labeled measurement taken at the same time."
        };
        public override string Id => IdValue;

        private Text resultText;
        protected override float Progress => solved ? 1f : 0f;
        protected override void ResetTask() { }

        protected override void BuildContent(RectTransform content)
        {
            var panel = Ui.Panel(content, "Diary", 90, 20, 1340, 570, MinigameTheme.Card);
            Ui.Label(panel.transform, "MARS WEATHER MAST · DUST DIARY", 36, 26, 1000, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, "Which time logged more airborne dust?", 36, 66, 900, 42, 25, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            BuildPhoto(panel.transform, "09:00", "SHARP RIDGE", "Simulated dust index 2", 36, 132, false);
            BuildPhoto(panel.transform, "14:00", "HAZY RIDGE", "Simulated dust index 7", 694, 132, true);
            resultText = Ui.Label(panel.transform, "Match the photo time to the chart time before answering.", 36, 500, 1268, 40, 18, MinigameTheme.SoftGray, TextAnchor.MiddleCenter);
            Feedback("Compare both labels: the photo appearance and the simulated dust-index value.", FeedbackKind.Info);
        }

        private void BuildPhoto(Transform parent, string time, string photo, string chart, float x, float y, bool correct)
        {
            var card = Ui.Panel(parent, "Photo_" + time, x, y, 610, 330, MinigameTheme.Panel);
            Ui.Label(card.transform, time, 24, 20, 200, 34, 27, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            var image = Ui.Panel(card.transform, "Ridge", 24, 72, 562, 132, correct ? new Color(.29f, .22f, .18f) : new Color(.52f, .3f, .2f));
            Ui.Label(image.transform, photo, 0, 0, 562, 132, 27, correct ? MinigameTheme.SoftGray : MinigameTheme.WarmWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Label(card.transform, chart, 24, 218, 562, 34, 20, MinigameTheme.WarmWhite, TextAnchor.MiddleCenter);
            Ui.Button(card.transform, "SELECT " + time, 110, 266, 390, 48, () => Choose(correct), correct ? Ui.ButtonStyle.Primary : Ui.ButtonStyle.Secondary, 18);
        }

        private void Choose(bool correct)
        {
            if (!correct)
            {
                Reject("Match the time on the photo to the time on the chart. Index 7 is greater than index 2.");
                resultText.text = "09:00 is clearer and has index 2. Check the 14:00 record.";
                return;
            }
            resultText.text = "14:00 · Simulated dust index 7 · higher of the two recorded values";
            CompleteTask("Dust Diary saved: the instrument logged more airborne dust at 14:00.");
        }

        protected override void OnRestart() { ResetTask(); if (resultText != null) resultText.text = "Match the photo time to the chart time before answering."; }
        protected override void AddOutputValues(MinigameResult result)
        {
            if (!solved) return;
            result.OutputValues["time"] = "14:00";
            result.OutputValues["dustIndex"] = "7";
        }
        protected override void Refresh() { Shell.SetProgress(Progress, "Dust comparison"); }
    }

    [Minigame(IdValue)]
    public sealed class MarsPacketPracticeMinigame : MarsTaskMinigame
    {
        public const string IdValue = "mars-packet-practice";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars-staging", MissionLabel = "Mars optional", Title = "What Goes First?",
            Instruction = "Deliver the 2-second health report and 4-second small reading inside a 10-second practice window.",
            EducationalSubtitle = "The 9-second full graph is optional. Put essential messages first.",
            DefaultTimeLimitSeconds = 65f, EducationalNote = "Packet size and priority determine what fits into a limited communication window."
        };
        public override string Id => IdValue;

        private readonly List<string> queue = new List<string>();
        private bool health, sample, graph;
        private Text queueText, receivedText, timeText;
        private Button transmit;
        protected override float Progress => (health ? .45f : 0f) + (sample ? .45f : 0f) + (graph ? .1f : 0f);
        protected override void ResetTask() { queue.Clear(); health = sample = graph = false; }

        protected override void BuildContent(RectTransform content)
        {
            var panel = Ui.Panel(content, "Practice", 170, 20, 1180, 570, MinigameTheme.Card);
            Ui.Label(panel.transform, "MARS PRACTICE CONSOLE · 10 SECONDS", 38, 28, 900, 28, 18, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(panel.transform, "Build the order Earth should receive. Delivered packets stay delivered on retry.", 38, 68, 1080, 40, 21, MinigameTheme.WarmWhite);
            Ui.Button(panel.transform, "HEALTH REPORT · 2 s · REQUIRED", 38, 132, 520, 66, () => Add("health"));
            Ui.Button(panel.transform, "SMALL READING · 4 s · REQUIRED", 38, 210, 520, 66, () => Add("sample"));
            Ui.Button(panel.transform, "FULL GRAPH · 9 s · OPTIONAL", 38, 288, 520, 66, () => Add("graph"));
            Ui.Button(panel.transform, "CLEAR QUEUE", 38, 374, 220, 48, () => { queue.Clear(); Refresh(); }, Ui.ButtonStyle.Ghost, 16);
            timeText = Ui.Label(panel.transform, "", 38, 454, 520, 64, 22, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            queueText = Ui.Label(panel.transform, "", 620, 132, 520, 210, 20, MinigameTheme.WarmWhite);
            receivedText = Ui.Label(panel.transform, "", 620, 350, 520, 80, 18, MinigameTheme.SoftGray);
            transmit = Ui.Button(panel.transform, "RUN PRACTICE WINDOW", 620, 454, 520, 70, Transmit, Ui.ButtonStyle.Primary);
            Feedback("Queue the required packets first. Starting with the full graph leaves too little time.", FeedbackKind.Info);
        }

        private void Add(string id)
        {
            bool delivered = id == "health" ? health : id == "sample" ? sample : graph;
            if (delivered || queue.Contains(id))
            {
                Feedback("That packet is already received or queued.", FeedbackKind.Info);
                return;
            }
            queue.Add(id);
            Refresh();
        }

        private void Transmit()
        {
            if (queue.Count == 0) { Reject("The practice queue is empty. Add both required packets."); return; }
            int used = 0;
            var sent = new List<string>();
            foreach (string id in queue)
            {
                int duration = id == "health" ? 2 : id == "sample" ? 4 : 9;
                if (used + duration > 10) break;
                used += duration;
                sent.Add(id);
                if (id == "health") health = true; else if (id == "sample") sample = true; else graph = true;
            }
            queue.RemoveAll(sent.Contains);
            if (health && sample)
            {
                CompleteTask(graph
                    ? "Practice passed. Required packets and the optional graph fit across the completed windows."
                    : "Practice passed. Health report and small reading were prioritized; the graph can wait.");
                return;
            }
            Reject("The full graph took up the window. Move the health report and small reading to the front.");
            Refresh();
        }

        protected override void OnRestart() { ResetTask(); Refresh(); }

        protected override void AddOutputValues(MinigameResult result)
        {
            result.OutputValues["healthDelivered"] = health.ToString();
            result.OutputValues["sampleDelivered"] = sample.ToString();
            result.OutputValues["graphDelivered"] = graph.ToString();
        }

        protected override void Refresh()
        {
            if (queueText == null) return;
            int queuedSeconds = queue.Sum(id => id == "health" ? 2 : id == "sample" ? 4 : 9);
            queueText.text = queue.Count == 0 ? "QUEUE EMPTY" : string.Join("\n", queue.Select((id, i) => $"{i + 1}. {Label(id)}"));
            timeText.text = $"QUEUED {queuedSeconds} s\nWINDOW 10 s";
            timeText.color = queuedSeconds <= 10 ? MinigameTheme.Good : MinigameTheme.Warn;
            receivedText.text = $"Health: {(health ? "RECEIVED" : "WAITING")}   ·   Reading: {(sample ? "RECEIVED" : "WAITING")}\nFull graph: {(graph ? "RECEIVED" : "OPTIONAL")}";
            transmit.interactable = IsRunning;
            Shell.SetProgress(Progress, "Practice packets received");
        }

        private static string Label(string id) => id == "health" ? "Health report · 2 s" : id == "sample" ? "Small reading · 4 s" : "Full graph · 9 s";
    }
}

