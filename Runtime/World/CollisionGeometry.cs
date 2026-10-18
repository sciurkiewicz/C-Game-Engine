using System.Numerics;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<ColliderKind>))]
enum ColliderKind { Circle, Box, Polygon }
sealed record GroundPoint2(float X, float Z)
{
    [JsonIgnore] public Vector2 Vector => new(X, Z);
}
sealed class ColliderData
{
    public ColliderKind Kind { get; set; }
    public float Radius { get; set; } = .5f;
    public float Width { get; set; } = 1;
    public float Depth { get; set; } = 1;
    public float Rotation { get; set; }
    public List<GroundPoint2> Points { get; set; } = [];
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !MapData.FiniteRange(Radius, .05f, 64) || !MapData.FiniteRange(Width, .1f, 128) ||
            !MapData.FiniteRange(Depth, .1f, 128) || !MapData.FiniteRange(Rotation, -360, 360) || Points == null || Points.Count > 32 ||
            Points.Any(p => p == null || !MapData.FiniteRange(p.X, -64, 64) || !MapData.FiniteRange(p.Z, -64, 64)))
            throw new InvalidDataException("Invalid collider dimensions.");
        if (Kind != ColliderKind.Polygon) return;
        if (Points.Count < 3) throw new InvalidDataException("A polygon collider needs at least three points.");
        float area = 0;
        for (int i = 0; i < Points.Count; i++)
        {
            var a = Points[i].Vector; var b = Points[(i + 1) % Points.Count].Vector;
            if (Vector2.DistanceSquared(a, b) < .0001f) throw new InvalidDataException("Collider points must be distinct.");
            area += a.X * b.Y - b.X * a.Y;
            for (int j = i + 1; j < Points.Count; j++)
                if (j != i + 1 && !(i == 0 && j == Points.Count - 1) &&
                    CollisionGeometry.SegmentsDistance(a, b, Points[j].Vector, Points[(j + 1) % Points.Count].Vector) < .0001f)
                    throw new InvalidDataException("Collider edges must not cross.");
        }
        if (Math.Abs(area) < .01f) throw new InvalidDataException("Collider polygon has no area.");
    }
}

static class CollisionGeometry
{
    public static bool Solid(SceneObject item) => item.Solid && !(item.IsDoor && item.DoorOpen);
    public static float Radius(SceneObject item) => item.Collider is { Kind: ColliderKind.Circle } c ? c.Radius :
        item.Collider is { } shape ? shape.Kind == ColliderKind.Box ? MathF.Sqrt(shape.Width * shape.Width + shape.Depth * shape.Depth) / 2 :
            shape.Points.Select(p => p.Vector.Length()).DefaultIfEmpty(.5f).Max() : Math.Clamp(item.Height * .13f, .2f, .8f);
    public static Vector2[] Vertices(SceneObject item)
    {
        if (item.Collider is not { } c || c.Kind == ColliderKind.Circle) return [];
        var local = c.Kind == ColliderKind.Box ? new Vector2[] { new(-c.Width / 2, -c.Depth / 2), new(c.Width / 2, -c.Depth / 2),
            new(c.Width / 2, c.Depth / 2), new(-c.Width / 2, c.Depth / 2) } : c.Points.Select(p => p.Vector).ToArray();
        float angle = c.Rotation * MathF.PI / 180, cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        return local.Select(p => new Vector2(item.X + p.X * cos - p.Y * sin, item.Z + p.X * sin + p.Y * cos)).ToArray();
    }
    public static bool Blocks(SceneObject item, Vector2 from, Vector2 to, float radius)
    {
        if (!Solid(item)) return false;
        if (item.Collider == null || item.Collider.Kind == ColliderKind.Circle)
        {
            var center = new Vector2(item.X, item.Z); float total = Radius(item) + radius;
            // Let a player or monster step out if a door closes on them or their spawn overlaps an obstacle.
            if (Vector2.Distance(from, center) < total && Vector2.DistanceSquared(to, center) > Vector2.DistanceSquared(from, center) + .000001f &&
                Vector2.Dot(from - center, to - from) >= 0) return false;
            return PointSegmentDistance(center, from, to) < total;
        }
        if (PointSegmentDistance(new(item.X, item.Z), from, to) > Radius(item) + radius) return false;
        var vertices = Vertices(item);
        float Clearance(Vector2 p)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++) distance = Math.Min(distance, PointSegmentDistance(p, vertices[i], vertices[(i + 1) % vertices.Length]));
            return Inside(p, vertices) ? -distance : distance;
        }
        float penetration = Clearance(from);
        if (penetration < radius && Clearance(to) > penetration + .0001f && Clearance((from + to) / 2) >= penetration - .0001f) return false;
        if (Inside(from, vertices) || Inside(to, vertices)) return true;
        for (int i = 0; i < vertices.Length; i++)
            if (SegmentsDistance(from, to, vertices[i], vertices[(i + 1) % vertices.Length]) < radius + .00001f) return true;
        return false;
    }
    static bool Inside(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var a = vertices[i]; var b = vertices[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float length = ab.LengthSquared();
        return Vector2.Distance(p, a + ab * (length < 1e-12f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / length, 0, 1)));
    }
    public static float SegmentsDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
        var r = b - a; var s = d - c; float divisor = Cross(r, s);
        if (Math.Abs(divisor) > 1e-8f)
        {
            float t = Cross(c - a, s) / divisor, u = Cross(c - a, r) / divisor;
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0;
        }
        return Math.Min(Math.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
            Math.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)));
    }
}
