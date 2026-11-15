using System.Numerics;

sealed partial class MapDocument
{
    // The editor can checkpoint the first actual change, then append segments to the same stroke.
    public bool PaintRiverSegment(Vector3 from, Vector3 to, float width, float depth, bool checkpoint = false)
    {
        if (!RiverFinite(from) || !RiverFinite(to) || !MapData.FiniteRange(width, 2, 32) ||
            !MapData.FiniteRange(depth, .25f, 8))
            throw new ArgumentOutOfRangeException(nameof(width), "River width must be 2–32 m and depth 0.25–8 m; positions must be finite.");

        double radius = width * .5, ax = from.X, az = from.Z, bx = to.X, bz = to.Z;
        double left = -Map.Width / 2.0, top = -Map.Depth / 2.0;
        if (Math.Max(ax, bx) + radius < left || Math.Min(ax, bx) - radius > -left ||
            Math.Max(az, bz) + radius < top || Math.Min(az, bz) - radius > -top) return false;
        int minZ = (int)Math.Floor(Math.Clamp(Math.Min(az, bz) - radius - top, 0, Map.Depth));
        int maxZ = (int)Math.Ceiling(Math.Clamp(Math.Max(az, bz) + radius - top, 0, Map.Depth));
        double dx = bx - ax, dz = bz - az;
        var heights = new List<(int X, int Z, float Height)>();
        var wetVertices = new HashSet<int>();

        for (int z = minZ; z <= maxZ; z++)
        {
            double worldZ = z + top;
            // Restrict each scanline to the capsule. Even a long diagonal does not scan the whole map.
            double first = 0, last = 1;
            if (Math.Abs(dz) > .0000001)
            {
                first = (worldZ - radius - az) / dz; last = (worldZ + radius - az) / dz;
                if (first > last) (first, last) = (last, first);
                if (last < 0 || first > 1) continue;
                first = Math.Clamp(first, 0, 1); last = Math.Clamp(last, 0, 1);
            }
            else if (Math.Abs(worldZ - az) > radius) continue;
            double x0 = ax + dx * first, x1 = ax + dx * last;
            int minX = (int)Math.Floor(Math.Clamp(Math.Min(x0, x1) - radius - left, 0, Map.Width));
            int maxX = (int)Math.Ceiling(Math.Clamp(Math.Max(x0, x1) + radius - left, 0, Map.Width));
            for (int x = minX; x <= maxX; x++)
            {
                double distance = RiverDistance(x + left, worldZ, ax, az, bx, bz);
                if (distance >= radius) continue;
                float bank = Math.Clamp(((float)(distance / radius) - .65f) / .35f, 0, 1);
                bank = bank * bank * (3 - 2 * bank);
                float destination = MapData.SnapElevation(Math.Clamp(Map.WaterLevel - depth * (1 - bank),
                    MapData.MinElevation, MapData.MaxElevation));
                float current = Map.VertexHeight(x, z), next = Math.Min(current, destination);
                if (next < current) heights.Add((x, z, next));
                if (next < Map.WaterLevel) wetVertices.Add(z * (Map.Width + 1) + x);
            }
        }

        var candidates = new HashSet<int>();
        foreach (int vertex in wetVertices)
        {
            int x = vertex % (Map.Width + 1), z = vertex / (Map.Width + 1);
            for (int oz = -1; oz <= 0; oz++)
            for (int ox = -1; ox <= 0; ox++)
                if (x + ox >= 0 && z + oz >= 0 && x + ox < Map.Width && z + oz < Map.Depth)
                    candidates.Add((z + oz) * Map.Width + x + ox);
        }
        var water = new List<(int Cell, byte Mask)>();
        foreach (int cell in candidates)
        {
            int x = cell % Map.Width, z = cell / Map.Width, a = z * (Map.Width + 1) + x;
            int b = a + Map.Width + 1, c = b + 1, d = a + 1;
            byte original = Map.WaterMask(cell), mask = original;
            // Shared lowered vertices need water on both incident halves; geometry still clips the dry bank.
            if (wetVertices.Contains(a) || wetVertices.Contains(b) || wetVertices.Contains(c)) mask |= 1;
            if (wetVertices.Contains(a) || wetVertices.Contains(c) || wetVertices.Contains(d)) mask |= 2;
            if (mask != original) water.Add((cell, mask));
        }
        if (heights.Count == 0 && water.Count == 0) return false;
        if (checkpoint) Checkpoint();
        foreach (var change in heights) ChangeHeight(change.X, change.Z, change.Height);
        foreach (var change in water) ChangeWater(change.Cell, change.Mask);
        return true;
    }
    static bool RiverFinite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
    static double RiverDistance(double x, double z, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az, length = dx * dx + dz * dz;
        double t = length > 0 ? Math.Clamp(((x - ax) * dx + (z - az) * dz) / length, 0, 1) : 0;
        double ox = x - ax - dx * t, oz = z - az - dz * t;
        return Math.Sqrt(ox * ox + oz * oz);
    }
}
