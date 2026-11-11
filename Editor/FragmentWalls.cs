using System.Numerics;

static class FragmentWalls
{
    public static List<WallData> Capture(IEnumerable<WallData> walls, FragmentBounds bounds) => Clip(walls, bounds, true, null);
    public static List<WallData> Outside(IEnumerable<WallData> walls, FragmentBounds bounds, FragmentBounds mapBounds) => Clip(walls, bounds, false, mapBounds);
    static List<WallData> Clip(IEnumerable<WallData> walls, FragmentBounds bounds, bool inside, FragmentBounds? mapBounds)
    {
        var result = new List<WallData>();
        var offset = inside ? new Vector2(-bounds.MinX, -bounds.MinZ) : Vector2.Zero;
        foreach (var wall in walls)
        {
            var curve = wall.Points.Select(p => p.Vector).ToList();
            if (wall.Closed) curve.Add(curve[0]);
            bool fullyInside = curve.All(p => bounds.Contains(p.X, p.Y));
            if (inside && fullyInside)
            {
                var copy = ScenePrefab.Clone(wall);
                copy.Points = copy.Points.Select(p => new GroundPoint2(p.X + offset.X, p.Z + offset.Y)).ToList();
                result.Add(copy); continue;
            }
            var pieces = FragmentRoads.Pieces(curve, bounds, inside);
            if (!inside && pieces.Count == 1 && pieces[0].SequenceEqual(curve)) { result.Add(wall); continue; }
            bool first = true;
            foreach (var piece in pieces)
            {
                var points = new List<GroundPoint2>();
                foreach (var p in piece)
                {
                    var limit = inside ? bounds : mapBounds!.Value;
                    var clamped = new Vector2(Math.Clamp(p.X, limit.MinX, limit.MaxX - .001f), Math.Clamp(p.Y, limit.MinZ, limit.MaxZ - .001f)) + offset;
                    if (points.Count == 0 || Vector2.DistanceSquared(points[^1].Vector, clamped) >= .010001f) points.Add(new(clamped.X, clamped.Y));
                }
                // A clipped closed loop can have one extra endpoint. Split long pieces without dropping edges.
                for (int start = 0; start < points.Count - 1; start += WallData.PointLimit - 1)
                {
                    var copy = ScenePrefab.Clone(wall); copy.Closed = false;
                    copy.Points = points.Skip(start).Take(WallData.PointLimit).ToList();
                    if (inside || !first) copy.Id = Guid.NewGuid().ToString("N"); first = false;
                    result.Add(copy);
                }
            }
        }
        return result;
    }
}
