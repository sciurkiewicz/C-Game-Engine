using System.Numerics;

static class MonsterNavigation
{
    public static bool CanTraverse(MapData map, MonsterData actor, Vector3 from, Vector3 to)
    {
        int samples = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z)) / .15f));
        var previous = from;
        for (int i = 1; i <= samples; i++)
        {
            var sample = Vector3.Lerp(from, to, (float)i / samples);
            var p = map.GroundPosition(sample.X, sample.Z);
            if (!WorldRenderer.CanWalk(map, p, previous, actor.Id, WorldRenderer.CollisionRadius(actor))) return false;
            previous = p;
        }
        return true;
    }

    // Only plan after a blocked move. The bounded search avoids scanning a 2048 x 2048 map.
    public static List<Vector3> FindRoute(MapData map, MonsterData actor, Vector3 from, Vector3 target)
    {
        (int X, int Z) Cell(Vector3 p) => ((int)MathF.Floor(p.X + map.Width / 2f), (int)MathF.Floor(p.Z + map.Depth / 2f));
        Vector3 Center((int X, int Z) cell) => map.GroundPosition(cell.X - map.Width / 2f + .5f, cell.Z - map.Depth / 2f + .5f);
        var start = Cell(from); var goal = Cell(target);
        float Estimate((int X, int Z) cell) => Math.Abs(cell.X - goal.X) + Math.Abs(cell.Z - goal.Z);
        var queue = new PriorityQueue<(int X, int Z), float>(); queue.Enqueue(start, Estimate(start));
        var costs = new Dictionary<(int X, int Z), int> { [start] = 0 };
        var parents = new Dictionary<(int X, int Z), (int X, int Z)>();
        var closed = new HashSet<(int X, int Z)>();
        (int X, int Z)[] directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.TryDequeue(out var cell, out _) && closed.Count < 1024)
        {
            if (!closed.Add(cell)) continue;
            var point = cell == start ? from : Center(cell);
            if (Math.Abs(cell.X - goal.X) + Math.Abs(cell.Z - goal.Z) <= 1 && CanTraverse(map, actor, point, target))
            {
                var route = new List<Vector3>(); var cursor = cell;
                while (cursor != start) { route.Add(Center(cursor)); cursor = parents[cursor]; }
                route.Reverse(); route.Add(target); return route;
            }
            foreach (var d in directions)
            {
                var next = (X: cell.X + d.X, Z: cell.Z + d.Z);
                if (next.X < 0 || next.Z < 0 || next.X >= map.Width || next.Z >= map.Depth || closed.Contains(next) ||
                    !CanTraverse(map, actor, point, Center(next))) continue;
                int cost = costs[cell] + 1;
                if (costs.TryGetValue(next, out int old) && old <= cost) continue;
                costs[next] = cost; parents[next] = cell; queue.Enqueue(next, cost + Estimate(next));
            }
        }
        return [];
    }
}
