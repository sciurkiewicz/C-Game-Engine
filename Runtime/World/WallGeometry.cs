using System.Numerics;
using Raylib_cs;

readonly record struct WallSection(Vector2 Center, Vector2 Side, float Distance);
readonly record struct WallFace(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector2 UvD)
{
    public Vector3 Normal => Vector3.Normalize(Vector3.Cross(B - A, C - A));
}
sealed record WallMesh(WallFace[] Faces, Vector3 Min, Vector3 Max);

static class WallGeometry
{
    public static WallSection[] Sample(WallData wall)
    {
        int count = wall.Points.Count;
        if (count < 2) return [];
        var sides = new Vector2[count];
        static Vector2 Left(Vector2 direction) => new(-direction.Y, direction.X);
        for (int i = 0; i < count; i++)
        {
            var p = wall.Points[i].Vector;
            var before = i == 0 && !wall.Closed ? p : wall.Points[(i + count - 1) % count].Vector;
            var after = i == count - 1 && !wall.Closed ? p : wall.Points[(i + 1) % count].Vector;
            var incoming = p - before; var outgoing = after - p;
            if (incoming.LengthSquared() < .000001f) incoming = outgoing;
            if (outgoing.LengthSquared() < .000001f) outgoing = incoming;
            if (incoming.LengthSquared() < .000001f || outgoing.LengthSquared() < .000001f) continue;
            var a = Left(Vector2.Normalize(incoming)); var b = Left(Vector2.Normalize(outgoing));
            var sum = a + b;
            var miter = sum.LengthSquared() < .00001f ? b : Vector2.Normalize(sum);
            sides[i] = miter * Math.Min(wall.Thickness, wall.Thickness / 2 / Math.Max(.25f, Vector2.Dot(miter, b)));
        }
        var result = new List<WallSection> { new(wall.Points[0].Vector, sides[0], 0) };
        float distance = 0;
        for (int i = 0; i < count - (wall.Closed ? 0 : 1); i++)
        {
            int next = (i + 1) % count; var a = wall.Points[i].Vector; var b = wall.Points[next].Vector;
            float length = Vector2.Distance(a, b);
            int steps = Math.Clamp((int)MathF.Ceiling(length * 2), 1, 4096);
            for (int j = 1; j <= steps; j++)
            {
                float t = j / (float)steps;
                result.Add(new(Vector2.Lerp(a, b, t), Vector2.Lerp(sides[i], sides[next], t), distance + length * t));
            }
            distance += length;
        }
        return result.ToArray();
    }

    public static WallMesh Build(MapData map, WallData wall, IReadOnlyList<WallSection> sections)
    {
        var faces = new List<WallFace>();
        Vector3 Bottom(Vector2 p) => map.GroundPosition(p.X, p.Y) - Vector3.UnitY * .06f;
        Vector3 Top(Vector2 p) => map.GroundPosition(p.X, p.Y) + Vector3.UnitY * wall.Height;
        for (int i = 1; i < sections.Count; i++)
        {
            var a = sections[i - 1]; var b = sections[i];
            var al = Bottom(a.Center + a.Side); var ar = Bottom(a.Center - a.Side);
            var bl = Bottom(b.Center + b.Side); var br = Bottom(b.Center - b.Side);
            var atl = Top(a.Center + a.Side); var atr = Top(a.Center - a.Side);
            var btl = Top(b.Center + b.Side); var btr = Top(b.Center - b.Side);
            faces.Add(new(al, bl, btl, atl, new(a.Distance, al.Y), new(b.Distance, bl.Y), new(b.Distance, btl.Y), new(a.Distance, atl.Y)));
            faces.Add(new(br, ar, atr, btr, new(b.Distance, br.Y), new(a.Distance, ar.Y), new(a.Distance, atr.Y), new(b.Distance, btr.Y)));
            faces.Add(new(atl, btl, btr, atr, new(atl.X, atl.Z), new(btl.X, btl.Z), new(btr.X, btr.Z), new(atr.X, atr.Z)));
            if (!wall.Closed && i == 1)
                faces.Add(new(ar, al, atl, atr, new(0, ar.Y), new(wall.Thickness, al.Y), new(wall.Thickness, atl.Y), new(0, atr.Y)));
            if (!wall.Closed && i == sections.Count - 1)
                faces.Add(new(bl, br, btr, btl, new(0, bl.Y), new(wall.Thickness, br.Y), new(wall.Thickness, btr.Y), new(0, btl.Y)));
        }
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        // Dragged draft points may temporarily overlap. Never send a degenerate face to the GPU.
        faces.RemoveAll(f => Vector3.Cross(f.B - f.A, f.C - f.A).LengthSquared() < .000001f);
        foreach (var f in faces)
        foreach (var p in new[] { f.A, f.B, f.C, f.D }) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return new(faces.ToArray(), faces.Count == 0 ? Vector3.Zero : min, faces.Count == 0 ? Vector3.Zero : max);
    }

    public static float? RayDistance(MapData map, WallData wall, Ray ray)
    {
        var geometry = wall.Geometry(map);
        float enter = 0, leave = float.MaxValue;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = ray.Position[axis], direction = ray.Direction[axis], min = geometry.Min[axis], max = geometry.Max[axis];
            if (Math.Abs(direction) < .000001f) { if (origin < min || origin > max) return null; continue; }
            float a = (min - origin) / direction, b = (max - origin) / direction;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a); leave = Math.Min(leave, b);
            if (enter > leave) return null;
        }
        float closest = float.MaxValue;
        foreach (var f in geometry.Faces)
        {
            closest = Math.Min(closest, Triangle(ray, f.A, f.B, f.C));
            closest = Math.Min(closest, Triangle(ray, f.A, f.C, f.D));
        }
        return closest < float.MaxValue ? closest : null;
    }
    static float Triangle(Ray ray, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(ray.Direction, e2);
        float determinant = Vector3.Dot(e1, p);
        if (Math.Abs(determinant) < .000001f) return float.MaxValue;
        float inverse = 1 / determinant; var t = ray.Position - a;
        float u = Vector3.Dot(t, p) * inverse; if (u < 0 || u > 1) return float.MaxValue;
        var q = Vector3.Cross(t, e1); float v = Vector3.Dot(ray.Direction, q) * inverse;
        if (v < 0 || u + v > 1) return float.MaxValue;
        float distance = Vector3.Dot(e2, q) * inverse;
        return distance >= 0 ? distance : float.MaxValue;
    }
}
