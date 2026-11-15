using System.Collections.Generic;
using System.Linq;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Minigames.JupiterShieldAllocation
{
    // Jupiter · Radiation Shield Allocation. Rules: ShieldAllocationRules.
    // Drag a shield tile onto a system zone, or select a tile then a zone. Selecting a shielded zone with no tile removes it (refund).
    [Minigame(IdValue)]
    public sealed class JupiterShieldAllocationMinigame : MinigameBase
    {
        public const string IdValue = "jupiter-shield-allocation";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "jupiter", MissionLabel = "Jupiter orbiter", Title = "Radiation Shield Allocation",
            Instruction = "Place limited shielding over the highest-risk spacecraft systems before the radiation surge.",
            EducationalSubtitle = "Heavier shields protect more but use more of the spacecraft's mass allowance.",
            DefaultTimeLimitSeconds = 50f, EducationalNote = ShieldAllocationRules.EducationalNote };
        public override string Id => IdValue;

        private ShieldAllocationRules rules;
        private RectTransform content, ghost;
        private ShieldType? selectedTile, draggingTile;
        private readonly Dictionary<string, (RectTransform rt, Text shield, Text state, Image stateBg, Image slot)> zones = new Dictionary<string, (RectTransform, Text, Text, Image, Image)>();
        private readonly Dictionary<ShieldType, Image> tiles = new Dictionary<ShieldType, Image>();
        private Text massText, surgeText; private RectTransform massFill; private Image massFillImg;
        private static readonly Dictionary<string, Vector2> ZonePos = new Dictionary<string, Vector2> {
            { "computer", new Vector2(30, 40) }, { "comms", new Vector2(670, 40) }, { "power", new Vector2(30, 410) }, { "science", new Vector2(670, 410) } };

        protected override float Elapsed => rules.Elapsed;
        protected override void OnInitialize() { rules = new ShieldAllocationRules(Context, TimeLimit); }

        protected override void BuildContent(RectTransform c)
        {
            content = c;
            var diagram = Ui.Panel(c, "Spacecraft", 0, 0, 940, 610, MinigameTheme.Card);
            Ui.Label(diagram.transform, "SPACECRAFT · TOP VIEW", 330, 16, 300, 20, 15, MinigameTheme.Orange, TextAnchor.UpperCenter, FontStyle.Bold);
            // Simplified orbiter: bus, high-gain antenna, solar wings, instrument boom.
            var dish = Ui.Panel(diagram.transform, "Antenna", 395, 70, 150, 150, MinigameTheme.CardRaised, MinigameTheme.Circle);
            Ui.Panel(dish.transform, "Feed", 65, 65, 20, 20, MinigameTheme.SoftGray, MinigameTheme.Circle);
            Ui.Panel(diagram.transform, "Mast", 462, 210, 16, 40, MinigameTheme.Line);
            var wingL = Ui.Panel(diagram.transform, "WingL", 290, 285, 90, 50, new Color(.12f, .2f, .32f), MinigameTheme.RoundedSmall);
            var wingR = Ui.Panel(diagram.transform, "WingR", 560, 285, 90, 50, new Color(.12f, .2f, .32f), MinigameTheme.RoundedSmall);
            Ui.Panel(diagram.transform, "Bus", 380, 245, 180, 130, MinigameTheme.Line);
            Ui.Label(diagram.transform, "BUS", 380, 245, 180, 130, 18, MinigameTheme.SoftGray, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Panel(diagram.transform, "Boom", 462, 375, 16, 150, MinigameTheme.Line);
            // Connector lines from zones to the craft.
            Ui.Panel(diagram.transform, "LinkComputer", 270, 120, 125, 4, MinigameTheme.Line);
            Ui.Panel(diagram.transform, "LinkComms", 545, 120, 125, 4, MinigameTheme.Line);
            Ui.Panel(diagram.transform, "LinkPower", 270, 490, 192, 4, MinigameTheme.Line);
            Ui.Panel(diagram.transform, "LinkScience", 478, 490, 192, 4, MinigameTheme.Line);

            foreach (var z in rules.Zones) BuildZone(diagram.transform, z);

            var side = Ui.Panel(c, "Shields", 970, 0, 550, 610, MinigameTheme.Card);
            Ui.Label(side.transform, "RADIATION SURGE", 24, 18, 300, 20, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            surgeText = Ui.Label(side.transform, "", 24, 40, 500, 34, 24, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(side.transform, "SHIELDING MASS", 24, 92, 300, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            massText = Ui.Label(side.transform, "", 24, 114, 500, 30, 22, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            massFill = Ui.Meter(side.transform, "MassMeter", 24, 150, 500, 14, MinigameTheme.Orange, out massFillImg);
            Ui.Label(side.transform, "SHIELD TILES · drag onto a system", 24, 186, 500, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            Selectable first = null;
            int i = 0;
            foreach (ShieldType t in new[] { ShieldType.Light, ShieldType.Medium, ShieldType.Heavy })
            {
                var tile = Ui.Panel(side.transform, "Tile_" + t, 24 + i * 170, 214, 158, 170, MinigameTheme.CardRaised, null, true);
                Ui.Label(tile.transform, t.ToString().ToUpperInvariant(), 14, 14, 130, 24, 19, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
                Pips(tile.transform, ShieldAllocationRules.ProtectionOf(t), 14, 54);
                Ui.Label(tile.transform, $"Protection {ShieldAllocationRules.ProtectionOf(t)}\nMass {ShieldAllocationRules.MassOf(t)}", 14, 92, 140, 60, 16, MinigameTheme.SoftGray);
                var sel = tile.gameObject.AddComponent<Selectable>(); sel.targetGraphic = tile;
                var relay = tile.gameObject.AddComponent<UiPointerRelay>();
                var type = t;
                relay.Down = e => SelectTile(type);
                relay.Submit = () => SelectTile(type);
                relay.BeginDrag = e => BeginTileDrag(type, e);
                relay.Drag = MoveGhost;
                relay.EndDrag = e => EndTileDrag(e);
                relay.Enter = e => MinigameAudio.Play(MinigameCue.Hover, .3f);
                tiles[t] = tile;
                if (first == null) first = sel;
                i++;
            }
            Ui.Label(side.transform, "Systems need protection equal to their risk level. The science instrument can run one level lower.", 24, 400, 500, 60, 15, MinigameTheme.SoftGray);
            Ui.Button(side.transform, "Reset layout", 24, 520, 220, 64, () => { rules.ResetLayout(); selectedTile = null; Feedback("All shields removed. Mass refunded.", FeedbackKind.Info); Refresh(); });
            Ui.Button(side.transform, "Confirm layout", 260, 520, 266, 64, Confirm, Ui.ButtonStyle.Primary);
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            Feedback("The radiation pattern is shown on each system. Protect the flight computer and communications first.", FeedbackKind.Info);
        }

        private void BuildZone(Transform parent, ShieldZone z)
        {
            var p = ZonePos[z.Id];
            var card = Ui.Panel(parent, "Zone_" + z.Id, p.x, p.y, 240, 160, MinigameTheme.Panel, null, true);
            Ui.Label(card.transform, z.Label, 14, 12, 220, 22, 17, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Label(card.transform, z.Critical ? "Critical" : "Non-critical", 14, 34, 120, 18, 13, MinigameTheme.SoftGray);
            // Risk: word + bar count + hatch density (never colour alone).
            Color riskColor = z.Risk == RiskLevel.High ? MinigameTheme.Bad : z.Risk == RiskLevel.Medium ? MinigameTheme.Warn : MinigameTheme.SoftGray;
            Ui.Chip(card.transform, "RISK " + z.Risk.ToString().ToUpperInvariant(), 132, 30, 96, 24, riskColor, out _);
            for (int b = 0; b < 3; b++) Ui.Panel(card.transform, "RiskBar" + b, 200 + b * 10, 60 + (2 - b) * 6, 7, 18 + b * 6, b < (int)z.Risk ? riskColor : MinigameTheme.Line, MinigameTheme.RoundedSmall);
            var slot = Ui.Panel(card.transform, "Slot", 14, 62, 170, 40, MinigameTheme.Card, MinigameTheme.RoundedSmall);
            var shield = Ui.Label(slot.transform, "", 10, 0, 160, 40, 15, MinigameTheme.WarmWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
            var state = Ui.Chip(card.transform, "", 14, 114, 212, 32, MinigameTheme.Warn, out var stateBg);
            var sel = card.gameObject.AddComponent<Selectable>(); sel.targetGraphic = card;
            var relay = card.gameObject.AddComponent<UiPointerRelay>();
            string id = z.Id;
            relay.Down = e => ZonePressed(id);
            relay.Submit = () => ZonePressed(id);
            relay.Enter = e => MinigameAudio.Play(MinigameCue.Hover, .3f);
            zones[z.Id] = (card.rectTransform, shield, state, stateBg, slot);
        }

        private void Pips(Transform parent, int count, float x, float y)
        {
            for (int k = 0; k < 3; k++) Ui.Panel(parent, "Pip" + k, x + k * 26, y, 20, 20, k < count ? MinigameTheme.Orange : MinigameTheme.Line, MinigameTheme.RoundedSmall);
        }

        private void SelectTile(ShieldType t)
        {
            if (!IsRunning) return;
            selectedTile = selectedTile == t ? (ShieldType?)null : t;
            if (selectedTile.HasValue) Feedback($"{t} shield picked up. Choose a system to place it on.", FeedbackKind.Info);
            Refresh();
        }

        private void ZonePressed(string zoneId)
        {
            if (!IsRunning || draggingTile.HasValue) return;
            if (selectedTile.HasValue) { Place(zoneId, selectedTile.Value); return; }
            var z = rules.Find(zoneId);
            if (z.Shield.HasValue && rules.Remove(zoneId)) Feedback($"Shield removed from {z.Label}. Mass refunded.", FeedbackKind.Info);
            else Feedback("Pick a shield tile first, then choose this system.", FeedbackKind.Info);
            Refresh();
        }

        private void Place(string zoneId, ShieldType t)
        {
            var r = rules.Place(zoneId, t);
            var z = rules.Find(zoneId);
            Feedback(r.Message, r.Accepted ? (z.Protected ? FeedbackKind.Good : FeedbackKind.Info) : FeedbackKind.Bad);
            if (r.Accepted) selectedTile = null;
            Refresh();
        }

        private void BeginTileDrag(ShieldType t, PointerEventData e)
        {
            if (!IsRunning) return;
            draggingTile = t; selectedTile = t;
            ghost = Ui.Panel(content, "Ghost", 0, 0, 150, 60, new Color(1f, .55f, .24f, .9f)).rectTransform;
            Ui.Label(ghost, t.ToString().ToUpperInvariant() + " SHIELD", 0, 0, 150, 60, 16, new Color(.1f, .07f, .05f), TextAnchor.MiddleCenter, FontStyle.Bold);
            MoveGhost(e);
            Refresh();
        }
        private void MoveGhost(PointerEventData e)
        {
            if (ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, e.position, e.pressEventCamera, out var local);
            ghost.anchoredPosition = new Vector2(local.x - 75, local.y + 30);   // content pivot is top-left
        }
        private void EndTileDrag(PointerEventData e)
        {
            if (ghost != null) { Destroy(ghost.gameObject); ghost = null; }
            if (!draggingTile.HasValue) return;
            var t = draggingTile.Value; draggingTile = null;
            var target = zones.FirstOrDefault(z => Ui.Contains(z.Value.rt, e));
            if (target.Key != null) Place(target.Key, t);
            else { Feedback("Drop the shield on one of the four systems.", FeedbackKind.Info); selectedTile = null; Refresh(); }
        }

        private void Confirm()
        {
            if (!IsRunning) return;
            var outcome = rules.Confirm();
            Feedback(outcome == MinigameOutcome.Success ? "Layout locked. Every system is protected." : "Layout locked with weak points.", outcome == MinigameOutcome.Success ? FeedbackKind.Good : FeedbackKind.Bad);
            Finish();
        }

        protected override bool OnTick(float dt)
        {
            bool done = rules.Tick(dt);
            if (surgeText != null) surgeText.text = TimeLimit > 0 ? $"Surge in {Mathf.CeilToInt(Mathf.Max(0, TimeLimit - rules.Elapsed))} s" : "No time limit";
            return done;
        }

        protected override void OnRestart() { rules.ResetLayout(); selectedTile = null; Refresh(); }
        protected override void OnCancelled() { if (ghost != null) Destroy(ghost.gameObject); }

        protected override void Refresh()
        {
            if (massText == null) return;
            foreach (var z in rules.Zones)
            {
                var ui = zones[z.Id];
                ui.shield.text = z.Shield.HasValue ? $"{z.Shield.Value.ToString().ToUpperInvariant()}  ·  P{z.Protection}" : "No shield";
                ui.slot.color = z.Shield.HasValue ? MinigameTheme.OrangeDim : MinigameTheme.Card;
                string need = z.RequiredProtection == 0 ? "no shield needed" : $"needs P{z.RequiredProtection}";
                Ui.SetChip(ui.state, ui.stateBg, z.Protected ? $"PROTECTED · {need}" : $"EXPOSED · {need}", z.Protected ? MinigameTheme.Good : z.Critical ? MinigameTheme.Bad : MinigameTheme.Warn);
                bool canDrop = selectedTile.HasValue && IsRunning;
                ui.rt.GetComponent<Image>().color = canDrop ? MinigameTheme.CardRaised : MinigameTheme.Panel;
            }
            foreach (var kv in tiles)
            {
                bool affordable = ShieldAllocationRules.MassOf(kv.Key) <= rules.MassLeft;
                kv.Value.color = selectedTile == kv.Key ? MinigameTheme.OrangeDim : affordable ? MinigameTheme.CardRaised : MinigameTheme.Panel;
            }
            massText.text = $"{rules.MassUsed} / {rules.MassBudget} units  ·  {rules.MassLeft} left";
            Ui.SetMeter(massFill, rules.MassUsed / (float)rules.MassBudget);
            massFillImg.color = rules.MassLeft <= 1 ? MinigameTheme.Warn : MinigameTheme.Orange;
            if (surgeText != null && string.IsNullOrEmpty(surgeText.text)) surgeText.text = TimeLimit > 0 ? $"Surge in {Mathf.CeilToInt(TimeLimit)} s" : "No time limit";
            Shell.SetProgress(rules.Progress, $"Systems protected {rules.Zones.Count(z => z.Protected)} / {rules.Zones.Count}");
        }

        protected override MinigameResult BuildResult() => rules.BuildResult(Context);
    }
}
