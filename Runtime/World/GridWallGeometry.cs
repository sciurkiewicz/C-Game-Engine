using System.Numerics;
using Raylib_cs;

static class GridWallGeometry
{
    // Outline order is clockwise when viewed from above; neighbours follow its four sides.
    public static readonly (int X, int Z)[] Directions = [(-1, 0), (0, 1), (1, 0), (0, -1)];
    public static (int X, int Z) Cell(MapData map, Vector3 point) => ((int)MathF.Floor(point.X + map.Width / 2f), (int)MathF.Floor(point.Z + map.Depth / 2f));
    static float Noise(float x, float z) => (MathF.Sin(x * 1.71f + z * 2.31f) + MathF.Sin(x * 4.13f - z * 3.17f) * .5f) / 1.5f;
    public static Vector2 Warp(Vector2 p, float roughness) => p + new Vector2(Noise(p.X + 11.3f, p.Y), Noise(p.X, p.Y + 37.1f)) * (.16f * roughness);
    public static Vector2[] Outline(MapData map, GridWallCell cell)
    {
        float x = cell.X - map.Width / 2f, z = cell.Z - map.Depth / 2f;
        Vector2[] corners = [new(x, z), new(x, z + 1), new(x + 1, z + 1), new(x + 1, z)];
        float roughness = cell.Style == WallStyle.Cave ? cell.Roughness : 0;
        var result = new Vector2[12];
        for (int side = 0; side < 4; side++)
        for (int i = 0; i < 3; i++) result[side * 3 + i] = Warp(Vector2.Lerp(corners[side], corners[(side + 1) % 4], i / 3f), roughness);
        return result;
    }
    public static WallMesh Build(MapData map, GridWallCell cell, Vector2[] outline)
    {
        var faces = new List<WallFace>();
        float roughness = cell.Style == WallStyle.Cave ? cell.Roughness : 0;
        Vector3 Point(Vector2 p, float t)
        {
            // Shared world coordinates join neighbouring cave cells without cracks.
            float height = cell.Height * (1 + roughness * .15f * Noise(p.X * .8f, p.Y * .8f));
            var side = p + new Vector2(Noise(p.X + 8, p.Y), Noise(p.X, p.Y + 19)) * (roughness * .035f * MathF.Sin(t * MathF.PI));
            return new(side.X, map.HeightAt(side.X, side.Y) + height * t - (t == 0 ? .06f : 0), side.Y);
        }
        for (int side = 0; side < 4; side++)
        {
            var neighbour = map.WallCellAt(cell.X + Directions[side].X, cell.Z + Directions[side].Z);
            if (neighbour != null && neighbour.Style == cell.Style && neighbour.Height == cell.Height && neighbour.Roughness == cell.Roughness) continue;
            for (int step = 0; step < 3; step++)
            for (int row = 0; row < 3; row++)
            {
                int i = side * 3 + step, next = (i + 1) % 12;
                var a = Point(outline[i], row / 3f); var b = Point(outline[next], row / 3f);
                var c = Point(outline[next], (row + 1) / 3f); var d = Point(outline[i], (row + 1) / 3f);
                Vector2 Uv(Vector3 p) => new(side is 0 or 2 ? p.Z : p.X, p.Y);
                faces.Add(new(a, b, c, d, Uv(a), Uv(b), Uv(c), Uv(d)));
            }
        }
        // A small top lattice gives the cave wall a broken crest instead of a flat tabletop.
        float ox = cell.X - map.Width / 2f, oz = cell.Z - map.Depth / 2f;
        Vector3 Top(float x, float z) => Point(Warp(new(ox + x, oz + z), roughness), 1);
        for (int z = 0; z < 3; z++)
        for (int x = 0; x < 3; x++)
        {
            var a = Top(x / 3f, z / 3f); var b = Top(x / 3f, (z + 1) / 3f);
            var c = Top((x + 1) / 3f, (z + 1) / 3f); var d = Top((x + 1) / 3f, z / 3f);
            faces.Add(new(a, b, c, d, new(a.X, a.Z), new(b.X, b.Z), new(c.X, c.Z), new(d.X, d.Z)));
        }
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var face in faces)
        foreach (var p in new[] { face.A, face.B, face.C, face.D }) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return new(faces.ToArray(), min, max);
    }
    public static IEnumerable<GridWallCell> Near(MapData map, Vector2 from, Vector2 to, float radius)
    {
        int ax = Math.Clamp((int)MathF.Floor(Math.Min(from.X, to.X) - radius - .21f + map.Width / 2f), 0, map.Width - 1);
        int bx = Math.Clamp((int)MathF.Floor(Math.Max(from.X, to.X) + radius + .21f + map.Width / 2f), 0, map.Width - 1);
        int az = Math.Clamp((int)MathF.Floor(Math.Min(from.Y, to.Y) - radius - .21f + map.Depth / 2f), 0, map.Depth - 1);
        int bz = Math.Clamp((int)MathF.Floor(Math.Max(from.Y, to.Y) + radius + .21f + map.Depth / 2f), 0, map.Depth - 1);
        for (int z = az; z <= bz; z++)
        for (int x = ax; x <= bx; x++) if (map.WallCellAt(x, z) is { } cell) yield return cell;
    }
    public static bool Blocks(MapData map, Vector2 from, Vector2 to, float radius) =>
        Near(map, from, to, radius).Any(c => c.Solid && CollisionGeometry.BlocksPolygon(c.Footprint(map), from, to, radius + (c.Style == WallStyle.Cave ? c.Roughness * .035f : 0)));
    public static IEnumerable<(int X, int Z)> Stroke((int X, int Z) from, (int X, int Z) to, int radius = 0)
    {
        int dx = Math.Abs(to.X - from.X), dz = Math.Abs(to.Z - from.Z), steps = Math.Max(dx, dz);
        for (int i = 0; i <= steps; i++)
        {
            int x = steps == 0 ? from.X : (int)MathF.Round(from.X + (to.X - from.X) * (i / (float)steps));
            int z = steps == 0 ? from.Z : (int)MathF.Round(from.Z + (to.Z - from.Z) * (i / (float)steps));
            for (int oz = -radius; oz <= radius; oz++)
            for (int ox = -radius; ox <= radius; ox++) yield return (x + ox, z + oz);
        }
    }
    public static IEnumerable<(int X, int Z)> Rectangle((int X, int Z) a, (int X, int Z) b)
    {
        int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), z0 = Math.Min(a.Z, b.Z), z1 = Math.Max(a.Z, b.Z);
        for (int x = x0; x <= x1; x++) { yield return (x, z0); if (z1 != z0) yield return (x, z1); }
        for (int z = z0 + 1; z < z1; z++) { yield return (x0, z); if (x1 != x0) yield return (x1, z); }
    }
}
