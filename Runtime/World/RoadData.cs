using System.Numerics;
using System.Text.Json.Serialization;

sealed class RoadData
{
    Vector2[]? curve;
    int curveVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Road";
    public string Material { get; set; } = "dirt";
    public float Width { get; set; } = 3;
    public float Softness { get; set; } = .75f;
    public bool ClearVegetation { get; set; } = true;
    public bool Smooth { get; set; } = true;
    public List<GroundPoint2> Points { get; set; } = [];
    [JsonIgnore] public Vector2[] Curve
    {
        get
        {
            var hash = new HashCode(); hash.Add(Smooth); foreach (var point in Points) { hash.Add(point.X); hash.Add(point.Z); }
            int version = hash.ToHashCode();
            if (curve == null || version != curveVersion) { curve = RoadGeometry.Sample(Points, Smooth); curveVersion = version; }
            return curve;
        }
    }
    public void Validate(MapData map) => Validate(map.Contains);
    public void Validate(Func<float, float, bool> contains)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 ||
            !ProjectPaths.ValidAssetId(Material) || Material == "water" || !MapData.FiniteRange(Width, .5f, 64) ||
            !MapData.FiniteRange(Softness, 0, Width / 2) || Points == null || Points.Count is < 2 or > 128 ||
            Points.Any(p => p == null || !contains(p.X, p.Z))) throw new InvalidDataException("Invalid road.");
        for (int i = 1; i < Points.Count; i++)
            if (Vector2.DistanceSquared(Points[i - 1].Vector, Points[i].Vector) < .01f)
                throw new InvalidDataException("Road points must be at least 0.1 m apart.");
    }
}

sealed partial class MapData
{
    public List<RoadData> Roads { get; set; } = [];
    void ValidateRoads()
    {
        if (Roads == null || Roads.Count > 128 || Roads.Any(r => r == null) || Roads.Select(r => r.Id).Distinct().Count() != Roads.Count)
            throw new InvalidDataException("Invalid roads.");
        foreach (var road in Roads) road.Validate(this);
    }
}

static class RoadGeometry
{
    public static Vector2[] Sample(IReadOnlyList<GroundPoint2> points, bool smooth = true)
    {
        if (points.Count < 2 || !smooth) return points.Select(p => p.Vector).ToArray();
        var samples = new List<Vector2> { points[0].Vector };
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)].Vector; var p1 = points[i].Vector;
            var p2 = points[i + 1].Vector; var p3 = points[Math.Min(points.Count - 1, i + 2)].Vector;
            int steps = Math.Clamp((int)MathF.Ceiling((Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3)) * 2), 4, 2048);
            for (int j = 1; j <= steps; j++)
            {
                float t = j / (float)steps, t2 = t * t, t3 = t2 * t;
                samples.Add(.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        return samples.ToArray();
    }
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var direction = b - a;
        float t = direction.LengthSquared() < .000001f ? 0 : Math.Clamp(Vector2.Dot(point - a, direction) / direction.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + direction * t);
    }
    public static float Distance(Vector2 point, IReadOnlyList<Vector2> curve)
    {
        float nearest = float.MaxValue;
        for (int i = 1; i < curve.Count; i++) nearest = Math.Min(nearest, SegmentDistance(point, curve[i - 1], curve[i]));
        return nearest;
    }
    public static float Coverage(float distance, float width, float softness)
    {
        float radius = width / 2;
        if (distance >= radius) return 0;
        if (softness <= .0001f || distance <= radius - softness) return 1;
        float t = (radius - distance) / softness;
        return t * t * (3 - 2 * t);
    }
    public static int Version(IEnumerable<RoadData> roads)
    {
        var hash = new HashCode();
        foreach (var road in roads)
        {
            hash.Add(road.Material); hash.Add(road.Width); hash.Add(road.Softness); hash.Add(road.Smooth);
            foreach (var point in road.Points) { hash.Add(point.X); hash.Add(point.Z); }
        }
        return hash.ToHashCode();
    }
}

sealed partial class MapDocument
{
    public RoadData ApplyRoad(RoadData road, string? replaceId = null)
    {
        road.Validate(Map);
        int index = replaceId == null ? -1 : Map.Roads.FindIndex(r => r.Id == replaceId);
        if (replaceId != null && index < 0) throw new InvalidDataException("The selected road no longer exists.");
        if (index < 0 && Map.Roads.Count >= 128) throw new InvalidDataException("Road limit reached (128).");
        if (Map.Roads.Any(r => r.Id == road.Id && r.Id != replaceId)) throw new InvalidDataException("Road ID already exists.");
        var curve = road.Curve;
        Checkpoint();
        if (index < 0) Map.Roads.Add(road); else Map.Roads[index] = road;
        if (road.ClearVegetation)
            Map.Objects.RemoveAll(o => o.Asset.StartsWith("objects/vegetation/", StringComparison.OrdinalIgnoreCase) &&
                RoadGeometry.Distance(new(o.X, o.Z), curve) < road.Width / 2 + .2f);
        return road;
    }
    public void DeleteRoad(string id)
    {
        var road = Map.Roads.FirstOrDefault(r => r.Id == id); if (road == null) return;
        Checkpoint(); Map.Roads.Remove(road);
    }
}
