using System.Numerics;

readonly record struct WaterTriangle(Vector3 A, Vector3 B, Vector3 C, float Depth);

static class WaterGeometry
{
    public static float SurfaceHeight(MapData map, float x, float z, float time)
    {
        float depth = Math.Max(0, map.WaterLevel - map.HeightAt(x, z));
        float wave = MathF.Sin(x * 1.7f + time * 1.35f) * .65f + MathF.Sin(z * 2.2f - time * 1.1f) * .35f;
        return map.WaterLevel + wave * map.WaveHeight * Math.Clamp(depth / .4f, 0, 1);
    }
    public static List<WaterTriangle> BuildTile(MapData map, int x, int z, float time)
    {
        var triangles = new List<WaterTriangle>();
        byte mask = map.WaterMask(z * map.Width + x);
        if (mask == 0) return triangles;
        var a = map.Vertex(x, z); var b = map.Vertex(x, z + 1); var c = map.Vertex(x + 1, z + 1); var d = map.Vertex(x + 1, z);
        void Surface(Vector3 p, Vector3 q, Vector3 r)
        {
            var polygon = new List<Vector3>();
            var input = new[] { p, q, r };
            for (int i = 0; i < 3; i++)
            {
                var start = input[i]; var end = input[(i + 1) % 3];
                bool inside = start.Y < map.WaterLevel, endInside = end.Y < map.WaterLevel;
                if (inside) polygon.Add(start);
                if (inside != endInside) polygon.Add(Vector3.Lerp(start, end, (map.WaterLevel - start.Y) / (end.Y - start.Y)));
            }
            if (polygon.Count < 3) return;
            float depth = polygon.Average(v => Math.Max(0, map.WaterLevel - v.Y));
            for (int i = 0; i < polygon.Count; i++)
            {
                var v = polygon[i]; polygon[i] = new(v.X, SurfaceHeight(map, v.X, v.Z, time), v.Z);
            }
            for (int i = 1; i < polygon.Count - 1; i++) triangles.Add(new(polygon[0], polygon[i], polygon[i + 1], depth));
        }
        if ((mask & 1) != 0) Surface(a, b, c);
        if ((mask & 2) != 0) Surface(a, c, d);
        return triangles;
    }
}
