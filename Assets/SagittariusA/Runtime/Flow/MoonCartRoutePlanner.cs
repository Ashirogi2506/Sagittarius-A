using System.Collections.Generic;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    // Local grid search between surveyed checkpoints. Clearance includes the
    // vehicle's full footprint, so rocks and fence ends cannot be clipped by
    // an apparently clear centre line. No NavMesh bake or package is required.
    public sealed class MoonCartRoutePlanner
    {
        private const float Cell = 1.25f;
        private readonly Collider terrain;
        private readonly Transform cart;
        private readonly Collider[] overlaps = new Collider[128];
        private readonly Dictionary<Vector2Int, bool> free = new Dictionary<Vector2Int, bool>();
        public MoonCartRoutePlanner(Collider terrain, Transform cart) { this.terrain = terrain; this.cart = cart; }

        public bool IsClear(Vector3 point)
        {
            if (!terrain.Raycast(new Ray(new Vector3(point.x, 100, point.z), Vector3.down), out var ground, 200)) return false;
            if (Vector3.Angle(ground.normal, Vector3.up) > 22) return false;
            point.y = ground.point.y + 1.1f;
            int count = Physics.OverlapBoxNonAlloc(point, new Vector3(2.1f, 1.05f, 2.1f), overlaps,
                Quaternion.identity, ~(1 << 30), QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var obstacle = overlaps[i];
                if (obstacle == terrain || obstacle.transform.IsChildOf(cart)) continue;
                string name = obstacle.name;
                if (name.Contains("_Rock_") || name.Contains("safety rails") || name.Contains("_SurveyLedge") ||
                    name.Contains("_Bridge_Broken") || name.Contains("_Pole7_") || name.Contains("_Lander_") ||
                    name.Contains("_ServicePost") || name.Contains("_DeadBeacon")) return false;
            }
            return true;
        }

        public Vector3[] Plan(Vector3 start, Vector3[] checkpoints)
        {
            free.Clear();
            var complete = new List<Vector3>();
            Vector3 from = Flat(start);
            for (int i = 0; i < checkpoints.Length; i++)
            {
                Vector3 to = Flat(checkpoints[i]);
                // Transit anchors can move to nearby clear ground. Parking stops
                // are exact, keeping cargo targets and saved checkpoints aligned.
                if (i < checkpoints.Length - 1 && !IsClear(to))
                {
                    if (!FindFree(to, out var transit)) return null;
                    to = Position(transit);
                }
                var leg = Search(from, to);
                if (leg == null) return null;
                complete.AddRange(leg);
                from = to;
            }
            return complete.ToArray();
        }

        private List<Vector3> Search(Vector3 start, Vector3 end)
        {
            if (CanTravel(start, end)) return new List<Vector3> { end };
            if (!FindFree(start, out var first) || !FindFree(end, out var last)) return null;
            // Connect exact stops to the grid without cutting an obstacle.
            if (!CanTravel(start, Position(first)) || !CanTravel(Position(last), end)) return null;
            var open = new List<Vector2Int> { first };
            var closed = new HashSet<Vector2Int>();
            var score = new Dictionary<Vector2Int, float> { [first] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            int minX = Mathf.Min(first.x, last.x) - 24, maxX = Mathf.Max(first.x, last.x) + 24;
            int minY = Mathf.Min(first.y, last.y) - 24, maxY = Mathf.Max(first.y, last.y) + 24;
            for (int step = 0; open.Count > 0 && step < 16000; step++)
            {
                int best = 0; float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    float value = score[open[i]] + Vector2Int.Distance(open[i], last);
                    if (value < bestScore) { bestScore = value; best = i; }
                }
                var current = open[best]; open.RemoveAt(best);
                if (current == last)
                {
                    var route = new List<Vector3> { end, Position(last) };
                    while (previous.TryGetValue(current, out var parent)) { current = parent; route.Add(Position(current)); }
                    route.Add(start); route.Reverse();
                    // String-pull with the same footprint query to remove grid zigzags.
                    var simplified = new List<Vector3>();
                    int anchor = 0;
                    while (anchor < route.Count - 1)
                    {
                        int next = route.Count - 1;
                        while (next > anchor + 1 && !CanTravel(route[anchor], route[next])) next--;
                        simplified.Add(route[next]); anchor = next;
                    }
                    return simplified;
                }
                closed.Add(current);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    var next = current + new Vector2Int(x, y);
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY || closed.Contains(next) || !CellFree(next)) continue;
                    if (x != 0 && y != 0 && (!CellFree(current + new Vector2Int(x,0)) || !CellFree(current + new Vector2Int(0,y)))) continue;
                    if (!CanTravel(Position(current), Position(next))) continue;
                    float cost = score[current] + (x == 0 || y == 0 ? 1f : 1.414214f);
                    if (score.TryGetValue(next, out float old) && cost >= old) continue;
                    score[next] = cost; previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return null;
        }

        private bool FindFree(Vector3 point, out Vector2Int result)
        {
            var origin = new Vector2Int(Mathf.RoundToInt(point.x / Cell), Mathf.RoundToInt(point.z / Cell));
            for (int radius = 0; radius <= 8; radius++)
            {
                float nearest = float.PositiveInfinity; result = origin; bool found = false;
                for (int x = -radius; x <= radius; x++) for (int y = -radius; y <= radius; y++)
                {
                    var candidate = origin + new Vector2Int(x,y);
                    if (!CellFree(candidate)) continue;
                    float distance = (Position(candidate) - point).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; result = candidate; found = true; }
                }
                if (found) return true;
            }
            result = origin; return false;
        }
        private bool CellFree(Vector2Int cell)
        { if (!free.TryGetValue(cell, out bool value)) { value = IsClear(Position(cell)); free[cell] = value; } return value; }
        private bool CanTravel(Vector3 start, Vector3 end)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start,end) / .45f));
            for (int i = 0; i <= samples; i++) if (!IsClear(Vector3.Lerp(start,end,i/(float)samples))) return false;
            return true;
        }
        private static Vector3 Position(Vector2Int cell) => new Vector3(cell.x * Cell, 0, cell.y * Cell);
        private static Vector3 Flat(Vector3 point) { point.y = 0; return point; }
    }
}
