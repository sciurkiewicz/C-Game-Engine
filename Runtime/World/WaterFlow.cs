using System.Numerics;

readonly record struct WaterFlowVector(float X, float Z);

sealed partial class MapData
{
    // Cell-centred world-space velocity in metres per second. Missing entries retain the default wave animation.
    public Dictionary<int, WaterFlowVector> WaterFlow { get; set; } = [];

    void ValidateWaterFlow()
    {
        if (WaterFlow == null || WaterFlow.Count > Width * Depth || WaterFlow.Any(pair =>
            pair.Key < 0 || pair.Key >= Width * Depth || !float.IsFinite(pair.Value.X) || !float.IsFinite(pair.Value.Z) ||
            (double)pair.Value.X * pair.Value.X + (double)pair.Value.Z * pair.Value.Z > 16.00001))
            throw new InvalidDataException("Invalid water flow. Cell indices must belong to the map and speed must not exceed 4 m/s.");
    }

    // A masked triangle may be completely above the water level after later sculpting.
    internal bool HasWaterSurface(int cell)
    {
        byte mask = WaterMask(cell);
        if (mask == 0) return false;
        int x = cell % Width, z = cell / Width;
        float a = VertexHeight(x, z), c = VertexHeight(x + 1, z + 1);
        return ((mask & 1) != 0 && Math.Min(Math.Min(a, c), VertexHeight(x, z + 1)) < WaterLevel) ||
            ((mask & 2) != 0 && Math.Min(Math.Min(a, c), VertexHeight(x + 1, z)) < WaterLevel);
    }

    public Vector2 SampleWaterFlow(float x, float z)
    {
        if (WaterFlow.Count == 0 || !Contains(x, z) || WaterDepthAt(x, z) <= 0) return Vector2.Zero;
        float gx = Math.Clamp(x + Width / 2f - .5f, 0, Width - 1);
        float gz = Math.Clamp(z + Depth / 2f - .5f, 0, Depth - 1);
        int x0 = (int)gx, z0 = (int)gz, x1 = Math.Min(x0 + 1, Width - 1), z1 = Math.Min(z0 + 1, Depth - 1);
        Vector2 At(int tx, int tz)
        {
            int cell = tz * Width + tx;
            return WaterFlow.TryGetValue(cell, out var flow) && HasWaterSurface(cell) ? new(flow.X, flow.Z) : Vector2.Zero;
        }
        return Vector2.Lerp(Vector2.Lerp(At(x0, z0), At(x1, z0), gx - x0),
            Vector2.Lerp(At(x0, z1), At(x1, z1), gx - x0), gz - z0);
    }
}

sealed partial class MapDocument
{
    public bool PaintWaterFlowSegment(Vector3 from, Vector3 to, float width, float speed, bool erase = false, bool checkpoint = false)
    {
        if (!FlowFinite(from) || !FlowFinite(to) || !MapData.FiniteRange(width, 2, 32) || !MapData.FiniteRange(speed, 0, 4))
            throw new ArgumentOutOfRangeException(nameof(width), "Flow width must be 2–32 m and speed 0–4 m/s; positions must be finite.");

        double ax = from.X, az = from.Z, bx = to.X, bz = to.Z, dx = bx - ax, dz = bz - az;
        double lengthSquared = dx * dx + dz * dz;
        if (!erase && lengthSquared <= .000000000001) return false;
        double length = Math.Sqrt(lengthSquared), radius = width * .5;
        Vector2 destination = erase || speed == 0 ? Vector2.Zero : new((float)(dx / length * speed), (float)(dz / length * speed));
        double left = -Map.Width / 2.0, top = -Map.Depth / 2.0;
        if (Math.Max(ax, bx) + radius < left || Math.Min(ax, bx) - radius > -left ||
            Math.Max(az, bz) + radius < top || Math.Min(az, bz) - radius > -top) return false;
        int minZ = (int)Math.Floor(Math.Clamp(Math.Min(az, bz) - radius - top, 0, Map.Depth - 1));
        int maxZ = (int)Math.Floor(Math.Clamp(Math.Max(az, bz) + radius - top, 0, Map.Depth - 1));
        var changes = new List<(int Cell, WaterFlowVector Flow)>();

        for (int z = minZ; z <= maxZ; z++)
        {
            double worldZ = z + top + .5;
            // Scan only rows and columns touched by this capsule, even on large maps.
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
            int minX = (int)Math.Floor(Math.Clamp(Math.Min(x0, x1) - radius - left, 0, Map.Width - 1));
            int maxX = (int)Math.Floor(Math.Clamp(Math.Max(x0, x1) + radius - left, 0, Map.Width - 1));
            for (int x = minX; x <= maxX; x++)
            {
                double worldX = x + left + .5;
                double t = lengthSquared > 0 ? Math.Clamp(((worldX - ax) * dx + (worldZ - az) * dz) / lengthSquared, 0, 1) : 0;
                double ox = worldX - ax - dx * t, oz = worldZ - az - dz * t;
                double distance = Math.Sqrt(ox * ox + oz * oz);
                if (distance >= radius) continue;
                int cell = z * Map.Width + x;
                if (!Map.HasWaterSurface(cell)) continue;
                var previous = Map.WaterFlow.GetValueOrDefault(cell);
                Vector2 next;
                if (erase) next = Vector2.Zero;
                else
                {
                    float edge = Math.Clamp(((float)(distance / radius) - .75f) / .25f, 0, 1);
                    float blend = 1 - edge * edge * (3 - 2 * edge);
                    next = blend >= 1 ? destination : Vector2.Lerp(new(previous.X, previous.Z), destination, blend);
                }
                // Drop negligible values so erasing/stopping a current keeps the storage sparse.
                if (next.LengthSquared() < .00000001f) next = Vector2.Zero;
                var flow = new WaterFlowVector(next.X, next.Y);
                if (flow != previous) changes.Add((cell, flow));
            }
        }
        if (changes.Count == 0) return false;
        if (checkpoint) Checkpoint();
        foreach (var change in changes)
            if (change.Flow == default) Map.WaterFlow.Remove(change.Cell);
            else Map.WaterFlow[change.Cell] = change.Flow;
        return true;
    }

    static bool FlowFinite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
