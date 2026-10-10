using System.Numerics;

sealed class WaterRegion(int cells)
{
    public byte[] Masks { get; } = new byte[cells];
    public List<int> Cells { get; } = [];
}

static class WaterFill
{
    public static WaterRegion Find(MapData map, Vector3 point, bool existingOnly)
    {
        if (!map.Contains(point.X, point.Z) || map.HeightAt(point.X, point.Z) >= map.WaterLevel)
            return new(0);
        float gx = point.X + map.Width / 2f, gz = point.Z + map.Depth / 2f;
        int x = (int)gx, z = (int)gz, triangle = gx - x <= gz - z ? 0 : 1;
        return Collect(map, map.WaterLevel, [(z * map.Width + x) * 2 + triangle], existingOnly);
    }
    public static WaterRegion Expand(MapData map, float level)
    {
        IEnumerable<int> Seeds()
        {
            for (int cell = 0; cell < map.Tiles.Length; cell++)
            {
                byte mask = map.WaterMask(cell);
                if (mask == 0) continue;
                int x = cell % map.Width, z = cell / map.Width;
                float a = map.VertexHeight(x, z), c = map.VertexHeight(x + 1, z + 1);
                if ((mask & 1) != 0 && Math.Min(Math.Min(a, c), map.VertexHeight(x, z + 1)) < level) yield return cell * 2;
                if ((mask & 2) != 0 && Math.Min(Math.Min(a, c), map.VertexHeight(x + 1, z)) < level) yield return cell * 2 + 1;
            }
        }
        return Collect(map, level, Seeds(), false);
    }
    static WaterRegion Collect(MapData map, float level, IEnumerable<int> seeds, bool existingOnly)
    {
        var region = new WaterRegion(map.Tiles.Length);
        var queue = new Queue<int>();
        void Visit(int cell, int triangle)
        {
            byte bit = (byte)(1 << triangle);
            if ((region.Masks[cell] & bit) != 0 || (existingOnly && (map.WaterMask(cell) & bit) == 0)) return;
            if (region.Masks[cell] == 0) region.Cells.Add(cell);
            region.Masks[cell] |= bit; queue.Enqueue(cell * 2 + triangle);
        }
        foreach (int seed in seeds) Visit(seed / 2, seed % 2);
        while (queue.TryDequeue(out int current))
        {
            int cell = current / 2, triangle = current % 2, x = cell % map.Width, z = cell / map.Width;
            float a = map.VertexHeight(x, z), b = map.VertexHeight(x, z + 1), c = map.VertexHeight(x + 1, z + 1), d = map.VertexHeight(x + 1, z);
            void Cross(int nx, int nz, int neighbour, float p, float q)
            {
                if (Math.Min(p, q) >= level) return; // A dry ridge separates the reservoirs.
                if (nx < 0 || nz < 0 || nx >= map.Width || nz >= map.Depth) return;
                Visit(nz * map.Width + nx, neighbour);
            }
            Cross(x, z, 1 - triangle, a, c);
            if (triangle == 0) { Cross(x - 1, z, 1, a, b); Cross(x, z + 1, 1, b, c); }
            else { Cross(x + 1, z, 0, c, d); Cross(x, z - 1, 0, d, a); }
        }
        return region;
    }
}
