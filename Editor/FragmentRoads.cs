using System.Numerics;

readonly record struct FragmentBounds(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public bool Contains(float x, float z) => x >= MinX && z >= MinZ && x < MaxX && z < MaxZ;
    public bool ContainsClosed(Vector2 point) => point.X >= MinX && point.Y >= MinZ && point.X <= MaxX && point.Y <= MaxZ;
}

static class FragmentRoads
{
    static bool Clip(Vector2 a, Vector2 b, FragmentBounds bounds, out float enter, out float leave)
    {
        float first = 0, last = 1;
        bool Slab(float origin, float direction, float min, float max)
        {
            if (Math.Abs(direction) < .000001f) return origin >= min && origin <= max;
            float lo = (min - origin) / direction, hi = (max - origin) / direction;
            if (lo > hi) (lo, hi) = (hi, lo);
            first = Math.Max(first, lo); last = Math.Min(last, hi);
            return first <= last;
        }
        bool hit = Slab(a.X, b.X - a.X, bounds.MinX, bounds.MaxX) && Slab(a.Y, b.Y - a.Y, bounds.MinZ, bounds.MaxZ);
        enter = first; leave = last; return hit;
    }
    internal static List<List<Vector2>> Pieces(IReadOnlyList<Vector2> curve, FragmentBounds bounds, bool inside)
    {
        var pieces = new List<List<Vector2>>();
        List<Vector2>? current = null;
        void Add(Vector2 a, Vector2 b)
        {
            if (Vector2.DistanceSquared(a, b) < .000001f) return;
            if (current == null || Vector2.DistanceSquared(current[^1], a) > .00001f)
            { current = [a]; pieces.Add(current); }
            current.Add(b);
        }
        for (int i = 1; i < curve.Count; i++)
        {
            var a = curve[i - 1]; var b = curve[i];
            if (!Clip(a, b, bounds, out float enter, out float leave))
            { if (!inside) Add(a, b); else current = null; continue; }
            if (inside)
            {
                Add(Vector2.Lerp(a, b, enter), Vector2.Lerp(a, b, leave));
                if (leave < 1) current = null;
            }
            else
            {
                if (enter > 0) Add(a, Vector2.Lerp(a, b, enter));
                current = null;
                if (leave < 1) Add(Vector2.Lerp(a, b, leave), b);
            }
        }
        return pieces;
    }
    static Vector2[] Simplify(IReadOnlyList<Vector2> points, float tolerance)
    {
        var keep = new bool[points.Count]; keep[0] = keep[^1] = true;
        var pending = new Stack<(int Start, int End)>(); pending.Push((0, points.Count - 1));
        while (pending.TryPop(out var span))
        {
            int index = -1; float largest = tolerance;
            for (int i = span.Start + 1; i < span.End; i++)
            {
                float distance = RoadGeometry.SegmentDistance(points[i], points[span.Start], points[span.End]);
                if (distance > largest) { largest = distance; index = i; }
            }
            if (index < 0) continue;
            keep[index] = true; pending.Push((span.Start, index)); pending.Push((index, span.End));
        }
        return points.Where((_, index) => keep[index]).ToArray();
    }
    static RoadData? FromPiece(RoadData source, List<Vector2> piece, Vector2 offset, FragmentBounds? clamp)
    {
        Vector2[] points; float tolerance = .02f;
        do { points = Simplify(piece, tolerance); tolerance *= 2; } while (points.Length > 128);
        var result = new List<GroundPoint2>();
        foreach (var input in points)
        {
            var point = clamp is { } bounds ? new Vector2(Math.Clamp(input.X, bounds.MinX, bounds.MaxX - .001f), Math.Clamp(input.Y, bounds.MinZ, bounds.MaxZ - .001f)) : input;
            point += offset;
            if (result.Count == 0 || Vector2.DistanceSquared(result[^1].Vector, point) >= .010001f) result.Add(new(point.X, point.Y));
        }
        if (result.Count < 2) return null;
        var copy = ScenePrefab.Clone(source); copy.Points = result; copy.Smooth = false;
        return copy;
    }
    public static List<RoadData> Capture(IEnumerable<RoadData> roads, FragmentBounds bounds)
    {
        var result = new List<RoadData>(); var offset = new Vector2(-bounds.MinX, -bounds.MinZ);
        foreach (var road in roads)
        {
            if (road.Curve.All(p => bounds.Contains(p.X, p.Y)))
            {
                var copy = ScenePrefab.Clone(road);
                copy.Points = road.Points.Select(p => new GroundPoint2(p.X + offset.X, p.Z + offset.Y)).ToList();
                result.Add(copy); continue;
            }
            foreach (var piece in Pieces(road.Curve, bounds, true))
                if (FromPiece(road, piece, offset, bounds) is { } copy)
                { copy.Id = Guid.NewGuid().ToString("N"); result.Add(copy); }
        }
        return result;
    }
    public static List<RoadData> Outside(IEnumerable<RoadData> roads, FragmentBounds bounds, FragmentBounds? mapBounds = null)
    {
        var result = new List<RoadData>();
        foreach (var road in roads)
        {
            bool intersects = false;
            for (int i = 1; i < road.Curve.Length && !intersects; i++)
                intersects = Clip(road.Curve[i - 1], road.Curve[i], bounds, out _, out _);
            if (!intersects) { result.Add(road); continue; }
            bool first = true;
            foreach (var piece in Pieces(road.Curve, bounds, false))
                if (FromPiece(road, piece, Vector2.Zero, mapBounds) is { } copy)
                { if (!first) copy.Id = Guid.NewGuid().ToString("N"); first = false; result.Add(copy); }
        }
        return result;
    }
}
