using System.Collections.Generic;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Minigames.MarsPowerManagement
{
    // Mars · Dust Storm Power Management. Rules: DustStormRules (display values only, not the mission power model).
    [Minigame(IdValue)]
    public sealed class MarsPowerManagementMinigame : MinigameBase
    {
        public const string IdValue = "mars-power-management";
        public static bool AllowRestart => false;   // the storm is a timeline; restarting would undo its consequences
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "mars", MissionLabel = "Mars mission", Title = "Dust Storm Power Management",
            Instruction = "Keep essential systems powered while solar output falls during the dust storm.",
            EducationalSubtitle = "When demand is higher than solar supply, the battery covers the gap until it runs out.",
            DefaultTimeLimitSeconds = DustStormRules.Duration, EducationalNote = DustStormRules.EducationalNote };
        public override string Id => IdValue;

        private DustStormRules rules;
        private Text stormText, phaseText, solarText, demandText, batteryText, batteryTrend;
        private RectTransform stormFill, solarFill, demandFill, batteryFill;
        private Image batteryFillImg, demandChipBg; private Text demandChip;
        private readonly Dictionary<string, (Button toggle, Text warning, Image card)> cards = new Dictionary<string, (Button, Text, Image)>();
        private bool warnedLow;

        protected override float Elapsed => rules.Elapsed;
        protected override void OnInitialize() { rules = new DustStormRules(Context); }

        protected override void BuildContent(RectTransform c)
        {
            var storm = Ui.Panel(c, "Storm", 0, 0, 1520, 74, MinigameTheme.Card);
            Ui.Label(storm.transform, "DUST STORM", 24, 14, 200, 20, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            stormText = Ui.Label(storm.transform, "", 24, 38, 300, 26, 18, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            stormFill = Ui.Meter(storm.transform, "StormBar", 330, 30, 900, 14, new Color(.72f, .5f, .32f), out _);
            phaseText = Ui.Label(storm.transform, "", 1250, 24, 250, 30, 17, MinigameTheme.SoftGray, TextAnchor.UpperRight, FontStyle.Bold);

            MeterCard(c, "SOLAR AVAILABLE", 0, out solarText, out solarFill, out _, MinigameTheme.Warn);
            var demandCard = MeterCard(c, "SYSTEM DEMAND", 515, out demandText, out demandFill, out _, MinigameTheme.SoftGray);
            demandChip = Ui.Chip(demandCard, "", 318, 18, 150, 30, MinigameTheme.Good, out demandChipBg);
            var batteryCard = MeterCard(c, "BATTERY RESERVE", 1030, out batteryText, out batteryFill, out batteryFillImg, MinigameTheme.Good);
            batteryTrend = Ui.Label(batteryCard, "", 238, 18, 230, 30, 15, MinigameTheme.SoftGray, TextAnchor.UpperRight, FontStyle.Bold);

            Selectable first = null;
            for (int i = 0; i < rules.Systems.Count; i++)
            {
                var s = rules.Systems[i];
                float x = (i % 3) * 515, y = 270 + (i / 3) * 175;
                var card = Ui.Panel(c, "System_" + s.Id, x, y, 490, 160, MinigameTheme.Card);
                Ui.Label(card.transform, s.Label, 22, 18, 300, 28, 21, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
                Color catColor = s.Category == SystemCategory.Essential ? MinigameTheme.Orange : s.Category == SystemCategory.Mission ? MinigameTheme.SoftGray : MinigameTheme.Line;
                Ui.Chip(card.transform, s.Category.ToString().ToUpperInvariant(), 22, 56, 120, 28, catColor, out _);
                Ui.Label(card.transform, $"{s.DemandKw:0} kW", 154, 56, 100, 28, 18, MinigameTheme.SoftGray, TextAnchor.MiddleLeft, FontStyle.Bold);
                var warning = Ui.Label(card.transform, "", 22, 100, 290, 50, 15, MinigameTheme.Warn);
                string id = s.Id;
                var toggle = Ui.Button(card.transform, "ON", 320, 40, 148, 80, () => Toggle(id), Ui.ButtonStyle.Secondary, 24);
                cards[s.Id] = (toggle, warning, card);
                if (first == null) first = toggle;
            }
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            Feedback("Solar power will fall. Switch off what you can spare; keep the essentials on.", FeedbackKind.Info);
        }

        private Transform MeterCard(RectTransform c, string title, float x, out Text value, out RectTransform fill, out Image fillImage, Color color)
        {
            var card = Ui.Panel(c, "Card_" + title, x, 92, 490, 160, MinigameTheme.Card);
            Ui.Label(card.transform, title, 22, 20, 300, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            value = Ui.Label(card.transform, "", 22, 54, 300, 46, 36, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            fill = Ui.Meter(card.transform, "Meter", 22, 116, 446, 18, color, out fillImage);
            return card.transform;
        }

        private void Toggle(string id)
        {
            if (!IsRunning) return;
            var s = rules.Find(id); bool on = !s.On;
            rules.SetSystem(id, on);
            if (!on && s.Category == SystemCategory.Essential)
                Feedback($"{s.Label} is essential. Turn it back on within {rules.EssentialTolerance:0.#} seconds.", FeedbackKind.Bad);
            else Feedback($"{s.Label} switched {(on ? "on" : "off")}. Demand is now {rules.DemandKw:0} kW.", on ? FeedbackKind.Info : FeedbackKind.Good);
            Refresh();
        }

        protected override bool OnTick(float dt)
        {
            bool done = rules.Tick(dt);
            if (!warnedLow && rules.BatteryPercent < 25f) { warnedLow = true; Feedback("Battery below 25%. Shed more load or the storm will drain it.", FeedbackKind.Bad); }
            if (rules.BatteryPercent > 35f) warnedLow = false;
            Refresh();
            return done;
        }

        protected override void Refresh()
        {
            if (stormText == null) return;
            stormText.text = $"{rules.Elapsed:0} / {DustStormRules.Duration:0} s";
            Ui.SetMeter(stormFill, rules.StormProgress);
            float solar = rules.AvailableKw, ahead = rules.SolarAt(rules.Elapsed + 2f);
            phaseText.text = ahead < solar - .2f ? "Solar falling ▼" : ahead > solar + .2f ? "Solar recovering ▲" : "Solar steady";
            solarText.text = $"{solar:0.0} kW"; Ui.SetMeter(solarFill, solar / 14f);
            float demand = rules.DemandKw; bool over = demand > solar;
            demandText.text = $"{demand:0} kW"; Ui.SetMeter(demandFill, demand / 14f);
            Ui.SetChip(demandChip, demandChipBg, over ? "OVER SUPPLY" : "WITHIN SUPPLY", over ? MinigameTheme.Warn : MinigameTheme.Good);
            batteryText.text = $"{rules.BatteryPercent:0}%"; Ui.SetMeter(batteryFill, rules.BatteryPercent / 100f);
            batteryFillImg.color = rules.BatteryPercent <= 20 ? MinigameTheme.Bad : rules.BatteryPercent <= 40 ? MinigameTheme.Warn : MinigameTheme.Good;
            batteryTrend.text = over ? "DRAINING ▼" : rules.BatteryPercent >= 99.5f ? "FULL" : "CHARGING ▲";
            batteryTrend.color = over ? MinigameTheme.Warn : MinigameTheme.Good;

            foreach (var s in rules.Systems)
            {
                var ui = cards[s.Id];
                Ui.SetButtonLabel(ui.toggle, s.On ? "ON" : "OFF");
                ((Image)ui.toggle.targetGraphic).color = s.On ? MinigameTheme.Good : MinigameTheme.Line;
                ui.toggle.GetComponentInChildren<Text>().color = s.On ? new Color(.06f, .12f, .09f) : MinigameTheme.WarmWhite;
                if (s.Category == SystemCategory.Essential && !s.On)
                {
                    ui.warning.text = $"OFF {s.OffStreak:0.0} s · restore within {rules.EssentialTolerance:0.#} s";
                    ui.warning.color = MinigameTheme.Bad;
                }
                else ui.warning.text = s.Category == SystemCategory.Essential ? "Must stay on" : s.On ? "" : $"Saving {s.DemandKw:0} kW";
                if (s.Category != SystemCategory.Essential || s.On) ui.warning.color = MinigameTheme.SoftGray;
                ui.toggle.interactable = IsRunning;
            }
            Shell.SetProgress(rules.StormProgress, $"Storm {Mathf.RoundToInt(rules.StormProgress * 100)}% · science {Mathf.RoundToInt(rules.ScienceRatio * 100)}% on");
        }

        protected override MinigameResult BuildResult() => rules.BuildResult(Context);
    }
}
