using System.Collections.Generic;
using System.Linq;
using SagittariusA.Minigames.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SagittariusA.Minigames.MoonPowerRouting
{
    // Moon · Lunar Power Grid Routing. Rules: PowerGridRules.
    // Press a point and drag across neighbours to lay cable, or select a point then a neighbour. Click a cable to remove it.
    [Minigame(IdValue)]
    public sealed class MoonPowerRoutingMinigame : MinigameBase
    {
        public const string IdValue = "moon-power-routing";
        public static MinigameDefinition Definition => new MinigameDefinition {
            Id = IdValue, MissionId = "moon", MissionLabel = "Moon exploration", Title = "Lunar Power Grid Routing",
            Instruction = "Connect the power source to every critical lunar system without exceeding the cable limit.",
            EducationalSubtitle = "Each source can only feed a limited number of systems. Critical systems are served first.",
            DefaultTimeLimitSeconds = 100f, EducationalNote = PowerGridRules.EducationalNote };
        public override string Id => IdValue;

        private PowerGridRules rules;
        private RectTransform gridRoot, cableLayer;
        private float cell, ox, oy;
        private readonly Dictionary<(int, int), Image> nodeImages = new Dictionary<(int, int), Image>();
        private readonly Dictionary<(int, int), Text> nodeTexts = new Dictionary<(int, int), Text>();
        private readonly List<GameObject> cableObjects = new List<GameObject>();
        private (int x, int y)? selected, dragFrom;
        private Text cableText, sourceText;
        private RectTransform cableMeter; private Image cableMeterImg;
        private readonly Dictionary<string, (Text chip, Image bg)> status = new Dictionary<string, (Text, Image)>();
        private Button energize;

        protected override float Elapsed => rules.Elapsed;
        protected override void OnInitialize() { rules = new PowerGridRules(Context, TimeLimit); }

        private static string Abbrev(string id) => id switch { "solar" => "SOL", "battery" => "BAT", "comms" => "COM", "thermal" => "THM", "science" => "SCI", "lights" => "LGT", _ => "" };

        protected override void BuildContent(RectTransform c)
        {
            var area = Ui.Panel(c, "Site", 0, 0, 1080, 610, MinigameTheme.Card);
            Ui.Label(area.transform, "SITE MAP · " + rules.Layout.Name.ToUpperInvariant(), 24, 16, 700, 22, 15, MinigameTheme.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            var L = rules.Layout;
            cell = Mathf.Min((1080 - 80) / (float)L.Width, (610 - 90) / (float)L.Height);
            ox = (1080 - cell * (L.Width - 1)) / 2f; oy = 50 + (560 - cell * (L.Height - 1)) / 2f;
            gridRoot = Ui.Rect(area.transform, "Grid", 0, 0, 1080, 610);
            cableLayer = Ui.Rect(gridRoot, "Cables", 0, 0, 1080, 610);

            for (int x = 0; x < L.Width; x++)
                for (int y = 0; y < L.Height; y++)
                {
                    var n = rules.Node(x, y); var p = Center(x, y);
                    float size = n.Kind == GridNodeKind.Source || n.Kind == GridNodeKind.Consumer ? Mathf.Min(78, cell * .86f) : n.Kind == GridNodeKind.Blocked ? Mathf.Min(62, cell * .7f) : Mathf.Min(64, cell * .7f);
                    var hit = Ui.Panel(gridRoot, $"Node{x}_{y}", p.x - size / 2, p.y - size / 2, size, size, new Color(1, 1, 1, .001f), MinigameTheme.Circle, true);
                    Image visual; Text text = null;
                    switch (n.Kind)
                    {
                        case GridNodeKind.Blocked:
                            visual = Ui.Panel(hit.transform, "Crater", 0, 0, size, size, new Color(.06f, .07f, .09f, 1), MinigameTheme.Circle);
                            var hatch = Ui.Panel(visual.transform, "Hatch", size * .18f, size * .18f, size * .64f, size * .64f, new Color(.45f, .3f, .25f, .7f), MinigameTheme.Hatch);
                            hatch.type = Image.Type.Tiled;
                            Ui.Label(visual.transform, "X", 0, 0, size, size, 20, MinigameTheme.Bad, TextAnchor.MiddleCenter, FontStyle.Bold);
                            break;
                        case GridNodeKind.Empty:
                            visual = Ui.Panel(hit.transform, "Dot", size / 2 - 9, size / 2 - 9, 18, 18, MinigameTheme.Line, MinigameTheme.Circle);
                            break;
                        default:
                            bool src = n.Kind == GridNodeKind.Source;
                            visual = Ui.Panel(hit.transform, "Station", 0, 0, size, size, src ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised, MinigameTheme.Rounded);
                            text = Ui.Label(visual.transform, Abbrev(n.Id) + "\n" + (src ? "cap " + n.Capacity : n.Critical ? "critical" : "optional"), 0, 0, size, size, 15, MinigameTheme.WarmWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
                            break;
                    }
                    nodeImages[(x, y)] = visual; if (text != null) nodeTexts[(x, y)] = text;
                    var sel = hit.gameObject.AddComponent<Selectable>(); sel.targetGraphic = visual;
                    var colors = sel.colors; colors.highlightedColor = new Color(1.2f, 1.15f, 1.05f, 1); colors.selectedColor = new Color(1.25f, 1.1f, .9f, 1); sel.colors = colors;
                    var relay = hit.gameObject.AddComponent<UiPointerRelay>();
                    int cx = x, cy = y;
                    relay.Down = e => { NodePressed((cx, cy)); dragFrom = (cx, cy); };
                    relay.Enter = e => { MinigameAudio.Play(MinigameCue.Hover, .25f); if (dragFrom.HasValue && Mouse.current != null && Mouse.current.leftButton.isPressed) DragInto((cx, cy)); };
                    relay.Submit = () => NodePressed((cx, cy));
                }

            // Side panel.
            var side = Ui.Panel(c, "Status", 1110, 0, 410, 610, MinigameTheme.Card);
            Ui.Label(side.transform, "CABLE", 24, 18, 200, 20, 15, MinigameTheme.SoftGray, TextAnchor.UpperLeft, FontStyle.Bold);
            cableText = Ui.Label(side.transform, "", 24, 40, 360, 36, 28, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
            cableMeter = Ui.Meter(side.transform, "CableMeter", 24, 84, 362, 10, MinigameTheme.Orange, out cableMeterImg);
            sourceText = Ui.Label(side.transform, "", 24, 108, 362, 50, 15, MinigameTheme.SoftGray);
            float y0 = 166;
            foreach (var n in rules.Consumers.OrderBy(n => n.Priority))
            {
                var row = Ui.Panel(side.transform, "Row_" + n.Id, 24, y0, 362, 62, MinigameTheme.Panel);
                Ui.Label(row.transform, Abbrev(n.Id) + "  " + n.Label, 14, 8, 220, 22, 16, MinigameTheme.WarmWhite, TextAnchor.UpperLeft, FontStyle.Bold);
                Ui.Label(row.transform, n.Critical ? "Critical" : "Optional", 14, 32, 200, 20, 14, MinigameTheme.SoftGray);
                var chip = Ui.Chip(row.transform, "NO POWER", 232, 15, 118, 32, MinigameTheme.Warn, out var bg);
                status[n.Id] = (chip, bg); y0 += 70;
            }
            Ui.Button(side.transform, "Reset route", 24, 470, 170, 60, () => { rules.ResetRoute(); selected = null; Feedback("Route cleared. All cable is back on the reel.", FeedbackKind.Info); Refresh(); });
            energize = Ui.Button(side.transform, "Energize grid", 206, 470, 180, 60, Energize, Ui.ButtonStyle.Primary);
            Ui.Label(side.transform, "Drag across points to lay cable. Click a cable to remove it.", 24, 540, 362, 60, 14, MinigameTheme.SoftGray);
            Feedback("Start at SOL or BAT and drag to neighbouring points to lay cable.", FeedbackKind.Info);
            var first = rules.Layout.Special.First(n => n.Kind == GridNodeKind.Source);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(nodeImages[(first.X, first.Y)].transform.parent.gameObject);
        }

        private Vector2 Center(int x, int y) => new Vector2(ox + x * cell, oy + y * cell);

        private void NodePressed((int x, int y) n)
        {
            if (!IsRunning) return;
            if (rules.Node(n.x, n.y).Kind == GridNodeKind.Blocked) { Feedback("Damaged terrain. Cables cannot cross that point.", FeedbackKind.Bad); return; }
            if (selected.HasValue && selected.Value != n && Adjacent(selected.Value, n))
            {
                if (rules.HasCable(selected.Value, n)) { rules.Disconnect(selected.Value, n); Feedback("Cable removed.", FeedbackKind.Info); }
                else Lay(selected.Value, n);
            }
            selected = selected == n ? ((int, int)?)null : n;
            Refresh();
        }

        private void DragInto((int x, int y) n)
        {
            if (!IsRunning || !dragFrom.HasValue || dragFrom.Value == n) return;
            if (!Adjacent(dragFrom.Value, n)) { dragFrom = n; return; }
            if (!rules.HasCable(dragFrom.Value, n) && !Lay(dragFrom.Value, n)) { dragFrom = null; Refresh(); return; }
            dragFrom = n; selected = n;
            Refresh();
        }

        private bool Lay((int, int) a, (int, int) b)
        {
            var r = rules.Connect(a, b);
            Feedback(r.Message, r.Accepted ? FeedbackKind.Good : FeedbackKind.Bad);
            return r.Accepted;
        }
        private static bool Adjacent((int x, int y) a, (int x, int y) b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

        private void Energize()
        {
            if (!IsRunning) return;
            var outcome = rules.Energize();
            Feedback(outcome == MinigameOutcome.Success ? "Grid energized. Every critical system has power." : "Grid energized with gaps.", outcome == MinigameOutcome.Success ? FeedbackKind.Good : FeedbackKind.Bad);
            Finish();
        }

        protected override bool OnTick(float dt)
        {
            if (dragFrom.HasValue && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) dragFrom = null;
            return rules.Tick(dt);
        }

        protected override void OnRestart() { rules.ResetRoute(); selected = null; dragFrom = null; Refresh(); }

        protected override void Refresh()
        {
            if (cableLayer == null) return;
            foreach (var go in cableObjects) Destroy(go);
            cableObjects.Clear();
            var powered = rules.Powered(out var notes);
            var live = LiveNodes();
            foreach (var (a, b) in rules.Cables)
            {
                bool on = live.Contains(a) && live.Contains(b);
                var pa = Center(a.x, a.y); var pb = Center(b.x, b.y);
                bool horizontal = a.y == b.y; float t = on ? 14 : 10;
                float x = Mathf.Min(pa.x, pb.x) - (horizontal ? 0 : t / 2), y = Mathf.Min(pa.y, pb.y) - (horizontal ? t / 2 : 0);
                var img = Ui.Panel(cableLayer, "Cable", x, y, horizontal ? cell : t, horizontal ? t : cell, on ? MinigameTheme.Orange : MinigameTheme.SoftGray, on ? MinigameTheme.RoundedSmall : MinigameTheme.Hatch, true);
                if (!on) img.type = Image.Type.Tiled;   // striped = laid but not energized (pattern, not only colour)
                var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
                var ca = a; var cb = b;
                btn.onClick.AddListener(() => { if (!IsRunning) return; rules.Disconnect(ca, cb); MinigameAudio.Play(MinigameCue.Press); Feedback("Cable removed.", FeedbackKind.Info); Refresh(); });
                cableObjects.Add(img.gameObject);
            }
            foreach (var kv in nodeImages)
            {
                var n = rules.Node(kv.Key.Item1, kv.Key.Item2);
                bool isSel = selected.HasValue && selected.Value == kv.Key;
                if (n.Kind == GridNodeKind.Empty) kv.Value.color = isSel ? MinigameTheme.Orange : live.Contains(kv.Key) ? new Color(1f, .7f, .45f) : MinigameTheme.Line;
                else if (n.Kind == GridNodeKind.Consumer) kv.Value.color = powered.Contains(n.Id) ? new Color(.2f, .42f, .35f) : isSel ? MinigameTheme.OrangeDim : MinigameTheme.CardRaised;
                else if (n.Kind == GridNodeKind.Source) kv.Value.color = isSel ? MinigameTheme.Orange : MinigameTheme.OrangeDim;
                kv.Value.transform.parent.localScale = isSel ? new Vector3(1.08f, 1.08f, 1) : Vector3.one;
                if (n.Kind == GridNodeKind.Consumer && nodeTexts.TryGetValue(kv.Key, out var tx))
                    tx.text = Abbrev(n.Id) + "\n" + (powered.Contains(n.Id) ? "ON" : n.Critical ? "critical" : "optional");
            }
            foreach (var kv in status)
            {
                bool on = powered.Contains(kv.Key);
                Ui.SetChip(kv.Value.chip, kv.Value.bg, on ? "POWERED" : "NO POWER", on ? MinigameTheme.Good : MinigameTheme.Warn);
            }
            cableText.text = $"{rules.CablesUsed} / {rules.CableLimit} segments";
            Ui.SetMeter(cableMeter, rules.CablesUsed / (float)rules.CableLimit);
            cableMeterImg.color = rules.CablesLeft == 0 ? MinigameTheme.Warn : MinigameTheme.Orange;
            var solar = rules.Layout.Special.First(n => n.Id == "solar"); var bat = rules.Layout.Special.First(n => n.Id == "battery");
            sourceText.text = $"SOL solar array feeds {solar.Capacity} systems · BAT battery feeds {bat.Capacity}." + (notes.Count > 0 ? "\n" + notes[0] : "");
            int critical = rules.Consumers.Count(n => n.Critical);
            Shell.SetProgress(rules.Progress, $"Critical systems {rules.Consumers.Count(n => n.Critical && powered.Contains(n.Id))} / {critical}");
            energize.interactable = IsRunning;
        }

        // Grid points that sit on an energized network (used for cable styling).
        private HashSet<(int, int)> LiveNodes()
        {
            var live = new HashSet<(int, int)>(); var q = new Queue<(int, int)>();
            foreach (var s in rules.Layout.Special.Where(n => n.Kind == GridNodeKind.Source)) { live.Add((s.X, s.Y)); q.Enqueue((s.X, s.Y)); }
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var nb in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    if (!live.Contains(nb) && rules.HasCable((x, y), nb)) { live.Add(nb); q.Enqueue(nb); }
            }
            return live;
        }

        protected override MinigameResult BuildResult() => rules.BuildResult(Context);
    }
}
