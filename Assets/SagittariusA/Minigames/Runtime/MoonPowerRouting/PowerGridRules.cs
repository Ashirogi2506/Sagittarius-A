using System;
using System.Collections.Generic;
using System.Linq;

namespace SagittariusA.Minigames.MoonPowerRouting
{
    public enum GridNodeKind { Empty, Blocked, Source, Consumer }

    public sealed class GridNode
    {
        public int X, Y;
        public GridNodeKind Kind;
        public string Id;            // "solar", "battery", "comms", "thermal", "science", "lights"
        public string Label;
        public int Capacity;         // sources: power units
        public int Demand;           // consumers: power units
        public bool Critical;
        public int Priority;         // lower is served first
    }

    public sealed class GridLayout
    {
        public string Name;
        public int Width, Height, CableLimit;
        public List<GridNode> Special = new List<GridNode>();
        public List<(int x, int y)> Blocked = new List<(int, int)>();
        // A known valid route (pairs of node coordinates) used by tests to prove the layout is solvable within the limit.
        public List<((int x, int y) a, (int x, int y) b)> ReferenceSolution = new List<((int, int), (int, int))>();
    }

    // Three hand-made layouts of increasing difficulty. The seed may mirror a layout horizontally.
    public static class PowerGridLayouts
    {
        private static GridNode Src(string id, string label, int x, int y, int cap) => new GridNode { Id = id, Label = label, X = x, Y = y, Kind = GridNodeKind.Source, Capacity = cap };
        private static GridNode Load(string id, string label, int x, int y, bool critical, int priority) => new GridNode { Id = id, Label = label, X = x, Y = y, Kind = GridNodeKind.Consumer, Demand = 1, Critical = critical, Priority = priority };

        public static GridLayout Create(int difficulty, int seed)
        {
            var layout = MinigameDifficulty.Clamp(difficulty) switch { 1 => Level1(), 2 => Level2(), _ => Level3() };
            return (seed & 1) == 1 ? Mirror(layout) : layout;
        }

        private static IEnumerable<GridNode> Loads(int cx, int cy, int tx, int ty, int sx, int sy, int lx, int ly) => new[] {
            Load("comms", "Communications", cx, cy, true, 0), Load("thermal", "Thermal control", tx, ty, true, 1),
            Load("science", "Science instrument", sx, sy, true, 2), Load("lights", "Auxiliary lighting", lx, ly, false, 3) };

        // Path helper: consecutive cells -> segments.
        private static IEnumerable<((int, int), (int, int))> Path(params (int, int)[] cells)
        { for (int i = 1; i < cells.Length; i++) yield return (cells[i - 1], cells[i]); }

        private static GridLayout Level1()
        {
            var l = new GridLayout { Name = "Crater rim", Width = 7, Height = 5, CableLimit = 12 };
            l.Special.Add(Src("solar", "Solar array", 0, 2, 2)); l.Special.Add(Src("battery", "Battery", 6, 2, 2));
            l.Special.AddRange(Loads(3, 0, 3, 4, 5, 0, 5, 4));
            l.Blocked.AddRange(new[] { (2, 2), (4, 2), (3, 2) });
            l.ReferenceSolution.AddRange(Path((0, 2), (0, 1), (1, 1), (2, 1), (3, 1), (3, 0)));        // solar -> comms
            l.ReferenceSolution.AddRange(Path((0, 2), (0, 3), (1, 3), (2, 3), (3, 3), (3, 4)));        // solar -> thermal
            l.ReferenceSolution.AddRange(Path((6, 2), (6, 1), (5, 1), (5, 0)));                         // battery -> science
            l.CableLimit = l.ReferenceSolution.Count + 3;
            return l;
        }

        private static GridLayout Level2()
        {
            var l = new GridLayout { Name = "Boulder field", Width = 8, Height = 6, CableLimit = 14 };
            l.Special.Add(Src("solar", "Solar array", 0, 0, 2)); l.Special.Add(Src("battery", "Battery", 0, 5, 2));
            l.Special.AddRange(Loads(7, 0, 4, 3, 7, 5, 4, 0));
            l.Blocked.AddRange(new[] { (2, 0), (2, 1), (2, 2), (5, 2), (5, 3), (5, 4), (2, 4), (3, 4) });
            l.ReferenceSolution.AddRange(Path((0, 0), (1, 0), (1, 1), (1, 2), (1, 3), (2, 3), (3, 3), (4, 3)));              // solar -> thermal
            l.ReferenceSolution.AddRange(Path((4, 3), (4, 2), (4, 1), (5, 1), (6, 1), (7, 1), (7, 0)));                     // thermal -> comms (solar cap 2)
            l.ReferenceSolution.AddRange(Path((0, 5), (1, 5), (2, 5), (3, 5), (4, 5), (5, 5), (6, 5), (7, 5)));             // battery -> science (7)
            l.CableLimit = l.ReferenceSolution.Count + 2;
            return l;
        }

        private static GridLayout Level3()
        {
            var l = new GridLayout { Name = "Shadowed ridge", Width = 9, Height = 6, CableLimit = 16 };
            l.Special.Add(Src("solar", "Solar array", 4, 0, 2)); l.Special.Add(Src("battery", "Battery", 4, 5, 1));
            l.Special.AddRange(Loads(0, 2, 8, 2, 8, 5, 0, 5));
            l.Blocked.AddRange(new[] { (2, 1), (2, 2), (2, 3), (6, 2), (6, 3), (6, 4), (4, 2), (4, 3), (1, 4), (7, 0) });
            l.ReferenceSolution.AddRange(Path((4, 0), (3, 0), (2, 0), (1, 0), (0, 0), (0, 1), (0, 2)));                     // solar -> comms
            l.ReferenceSolution.AddRange(Path((4, 0), (5, 0), (6, 0), (6, 1), (7, 1), (8, 1), (8, 2)));                     // solar -> thermal
            l.ReferenceSolution.AddRange(Path((4, 5), (5, 5), (6, 5), (7, 5), (8, 5)));                                     // battery -> science
            l.CableLimit = l.ReferenceSolution.Count + 1;
            return l;
        }

        private static GridLayout Mirror(GridLayout l)
        {
            int M(int x) => l.Width - 1 - x;
            foreach (var n in l.Special) n.X = M(n.X);
            l.Blocked = l.Blocked.Select(b => (M(b.x), b.y)).ToList();
            l.ReferenceSolution = l.ReferenceSolution.Select(s => ((M(s.a.x), s.a.y), (M(s.b.x), s.b.y))).ToList();
            l.Name += " (mirrored)";
            return l;
        }
    }

    public sealed class ConnectResult
    {
        public bool Accepted; public string Message;
        public ConnectResult(bool ok, string msg) { Accepted = ok; Message = msg; }
    }

    // Rules for "Lunar Power Grid Routing": cables join orthogonally adjacent grid nodes.
    public sealed class PowerGridRules
    {
        public GridLayout Layout { get; }
        public int CableLimit { get; }
        public float TimeLimit { get; }
        public int Mistakes { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public MinigameOutcome? Outcome { get; private set; }

        private readonly GridNode[,] nodes;
        private readonly HashSet<(int, int, int, int)> cables = new HashSet<(int, int, int, int)>();
        public IEnumerable<((int x, int y) a, (int x, int y) b)> Cables => cables.Select(c => ((c.Item1, c.Item2), (c.Item3, c.Item4)));
        public int CablesUsed => cables.Count;
        public int CablesLeft => CableLimit - cables.Count;

        public PowerGridRules(MinigameContext context, float timeLimitSeconds)
        {
            int level = MinigameDifficulty.Clamp(context?.DifficultyLevel ?? 1);
            Layout = PowerGridLayouts.Create(level, context?.RandomSeed ?? 0);
            CableLimit = Layout.CableLimit; TimeLimit = timeLimitSeconds;
            nodes = new GridNode[Layout.Width, Layout.Height];
            for (int x = 0; x < Layout.Width; x++) for (int y = 0; y < Layout.Height; y++) nodes[x, y] = new GridNode { X = x, Y = y, Kind = GridNodeKind.Empty };
            foreach (var b in Layout.Blocked) nodes[b.x, b.y].Kind = GridNodeKind.Blocked;
            foreach (var s in Layout.Special) nodes[s.X, s.Y] = s;
        }

        public GridNode Node(int x, int y) => InBounds(x, y) ? nodes[x, y] : null;
        public IEnumerable<GridNode> Consumers => Layout.Special.Where(n => n.Kind == GridNodeKind.Consumer);
        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Layout.Width && y < Layout.Height;
        private static (int, int, int, int) Key((int x, int y) a, (int x, int y) b) =>
            (a.x < b.x || (a.x == b.x && a.y <= b.y)) ? (a.x, a.y, b.x, b.y) : (b.x, b.y, a.x, a.y);

        public bool HasCable((int x, int y) a, (int x, int y) b) => cables.Contains(Key(a, b));

        public ConnectResult Connect((int x, int y) a, (int x, int y) b)
        {
            if (Finished) return new ConnectResult(false, "The grid is already energized.");
            if (!InBounds(a.x, a.y) || !InBounds(b.x, b.y)) return Reject("That point is outside the site.");
            if (Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) != 1) return Reject("Cables only join neighbouring points.");
            if (nodes[a.x, a.y].Kind == GridNodeKind.Blocked || nodes[b.x, b.y].Kind == GridNodeKind.Blocked)
                return Reject("Damaged terrain. Cables cannot cross that point.");
            if (cables.Contains(Key(a, b))) return new ConnectResult(true, "That cable is already laid.");
            if (cables.Count >= CableLimit) return Reject($"Cable limit reached ({CableLimit}). Remove a cable first.");
            cables.Add(Key(a, b));
            return new ConnectResult(true, $"Cable laid. {CablesLeft} left.");
        }
        private ConnectResult Reject(string message) { Mistakes++; return new ConnectResult(false, message); }

        public bool Disconnect((int x, int y) a, (int x, int y) b) => !Finished && cables.Remove(Key(a, b));
        public void ResetRoute() { if (!Finished) cables.Clear(); }

        // Which consumers receive power. Each connected network shares its sources' capacity; critical systems are served first.
        public HashSet<string> Powered(out List<string> notes)
        {
            notes = new List<string>();
            var powered = new HashSet<string>();
            var seen = new HashSet<(int, int)>();
            foreach (var start in Layout.Special.Where(n => n.Kind == GridNodeKind.Source))
            {
                if (seen.Contains((start.X, start.Y))) continue;
                var network = new List<GridNode>();
                var queue = new Queue<(int, int)>(); queue.Enqueue((start.X, start.Y)); seen.Add((start.X, start.Y));
                while (queue.Count > 0)
                {
                    var (x, y) = queue.Dequeue(); network.Add(nodes[x, y]);
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                        if (InBounds(nx, ny) && !seen.Contains((nx, ny)) && cables.Contains(Key((x, y), (nx, ny)))) { seen.Add((nx, ny)); queue.Enqueue((nx, ny)); }
                }
                int supply = network.Where(n => n.Kind == GridNodeKind.Source).Sum(n => n.Capacity);
                foreach (var load in network.Where(n => n.Kind == GridNodeKind.Consumer).OrderBy(n => n.Priority))
                {
                    if (load.Demand <= supply) { supply -= load.Demand; powered.Add(load.Id); }
                    else notes.Add($"{load.Label} is connected but the network has no capacity left.");
                }
            }
            return powered;
        }
        public HashSet<string> Powered() => Powered(out _);

        public float Progress
        {
            get { var p = Powered(); var critical = Consumers.Where(c => c.Critical).ToList(); return critical.Count == 0 ? 1 : critical.Count(c => p.Contains(c.Id)) / (float)critical.Count; }
        }

        public MinigameOutcome Evaluate()
        {
            var p = Powered();
            bool comms = p.Contains("comms"), thermal = p.Contains("thermal"), science = p.Contains("science");
            if (comms && thermal && science && cables.Count <= CableLimit) return MinigameOutcome.Success;
            if (comms && thermal) return MinigameOutcome.PartialSuccess;
            return MinigameOutcome.Failure;
        }

        // Player presses "Energize grid".
        public MinigameOutcome Energize() { if (!Finished) { Finished = true; Outcome = Evaluate(); } return Outcome.Value; }

        public bool Tick(float dt)
        {
            if (Finished || dt <= 0) return false;
            Elapsed += dt;
            if (TimeLimit > 0 && Elapsed >= TimeLimit) { Elapsed = TimeLimit; Finished = true; Outcome = Evaluate(); return true; }
            return false;
        }

        public const string EducationalNote = "Lunar missions must prioritize communications and thermal control when available power is limited.";

        public MinigameResult BuildResult(MinigameContext context)
        {
            var outcome = Outcome ?? Evaluate();
            var powered = Powered(out var notes);
            var r = MinigameResult.For(context, outcome, outcome switch {
                MinigameOutcome.Success => $"All critical systems powered with {CablesUsed} of {CableLimit} cable segments" + (powered.Contains("lights") ? ", lighting too." : "."),
                MinigameOutcome.PartialSuccess => "Communications and thermal control are powered, but the science instrument is dark.",
                _ => "Communications or thermal control has no power. The site is at risk.",
            });
            int score = MinigameScoring.Standard(Mistakes, Elapsed, TimeLimit, 75f);
            r.Score = outcome == MinigameOutcome.Success ? score : outcome == MinigameOutcome.PartialSuccess ? Math.Min(score, 70) : Math.Min(score, 35);
            r.MistakeCount = Mistakes; r.CompletionTimeSeconds = Elapsed; r.CompletionPercent = Progress;
            r.EducationalNotes.Add(EducationalNote);
            r.EducationalNotes.AddRange(notes);
            switch (outcome)
            {
                case MinigameOutcome.Success: r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.PowerAvailabilityModifier, 0.10f, "Efficient routing leaves power margin.")); break;
                case MinigameOutcome.PartialSuccess: r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ScienceReturnModifier, -0.15f, "The science instrument was not powered.")); break;
                default:
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.ReliabilityModifier, -0.20f, "Unpowered communications or thermal control."));
                    r.SuggestedEffects.Add(new MinigameEffect(MinigameEffectTypes.MissionTimePenalty, 1f, "Crew time spent re-routing power."));
                    break;
            }
            return r;
        }
    }
}
