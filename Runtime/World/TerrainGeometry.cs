using System.Numerics;
using Raylib_cs;

static class TerrainGeometry
{
    public static Vector3 NormalAt(MapData map, float x, float z)
    {
        int tx = Math.Clamp((int)MathF.Floor(x + map.Width / 2f), 0, map.Width - 1);
        int tz = Math.Clamp((int)MathF.Floor(z + map.Depth / 2f), 0, map.Depth - 1);
        float u = x + map.Width / 2f - tx, v = z + map.Depth / 2f - tz;
        var a = map.Vertex(tx, tz); var c = map.Vertex(tx + 1, tz + 1);
        return Vector3.Normalize(u <= v ? Vector3.Cross(map.Vertex(tx, tz + 1) - a, c - a) :
            Vector3.Cross(c - a, map.Vertex(tx + 1, tz) - a));
    }
    public static Vector3? Raycast(MapData map, Ray ray)
    {
        float enter = 0, leave = float.MaxValue;
        bool Slab(float origin, float direction, float min, float max)
        {
            if (Math.Abs(direction) < 1e-7f) return origin >= min && origin <= max;
            float a = (min - origin) / direction, b = (max - origin) / direction;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a); leave = Math.Min(leave, b);
            return enter <= leave;
        }
        if (!Slab(ray.Position.X, ray.Direction.X, -map.Width / 2f, map.Width / 2f) ||
            !Slab(ray.Position.Z, ray.Direction.Z, -map.Depth / 2f, map.Depth / 2f) ||
            !Slab(ray.Position.Y, ray.Direction.Y, map.MinimumHeight - .001f, map.MaximumHeight + .001f) || leave < 0) return null;
        var first = ray.Position + ray.Direction * (enter + .0001f);
        int x = Math.Clamp((int)MathF.Floor(first.X + map.Width / 2f), 0, map.Width - 1);
        int z = Math.Clamp((int)MathF.Floor(first.Z + map.Depth / 2f), 0, map.Depth - 1);
        int sx = Math.Sign(ray.Direction.X), sz = Math.Sign(ray.Direction.Z);
        float dx = sx == 0 ? float.PositiveInfinity : 1 / Math.Abs(ray.Direction.X);
        float dz = sz == 0 ? float.PositiveInfinity : 1 / Math.Abs(ray.Direction.Z);
        float nextX = sx == 0 ? float.PositiveInfinity : (x + (sx > 0 ? 1 : 0) - map.Width / 2f - ray.Position.X) / ray.Direction.X;
        float nextZ = sz == 0 ? float.PositiveInfinity : (z + (sz > 0 ? 1 : 0) - map.Depth / 2f - ray.Position.Z) / ray.Direction.Z;
        for (int step = 0; step < map.Width + map.Depth + 4 && x >= 0 && z >= 0 && x < map.Width && z < map.Depth; step++)
        {
            var a = map.Vertex(x, z); var b = map.Vertex(x, z + 1); var c = map.Vertex(x + 1, z + 1); var d = map.Vertex(x + 1, z);
            float hit = Math.Min(IntersectTriangle(ray, a, b, c), IntersectTriangle(ray, a, c, d));
            if (float.IsFinite(hit) && hit >= enter - .001f && hit <= leave + .001f) return ray.Position + ray.Direction * hit;
            if (Math.Min(nextX, nextZ) > leave) break;
            if (nextX <= nextZ) { x += sx; nextX += dx; }
            else { z += sz; nextZ += dz; }
        }
        return null;
    }
    static float IntersectTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c)
    {
        var edge1 = b - a; var edge2 = c - a;
        var p = Vector3.Cross(ray.Direction, edge2); float determinant = Vector3.Dot(edge1, p);
        if (Math.Abs(determinant) < 1e-7f) return float.PositiveInfinity;
        float inverse = 1 / determinant;
        var t = ray.Position - a; float u = Vector3.Dot(t, p) * inverse;
        if (u < -.0001f || u > 1.0001f) return float.PositiveInfinity;
        var q = Vector3.Cross(t, edge1); float v = Vector3.Dot(ray.Direction, q) * inverse;
        if (v < -.0001f || u + v > 1.0001f) return float.PositiveInfinity;
        float distance = Vector3.Dot(edge2, q) * inverse;
        return distance >= 0 ? distance : float.PositiveInfinity;
    }
}
