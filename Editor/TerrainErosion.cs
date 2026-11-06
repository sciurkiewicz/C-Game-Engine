using System.Numerics;

enum ErosionKind { Hydraulic, Talus }

sealed class ErosionPatch
{
    public readonly int MinX, MinZ, Width, Depth;
    public readonly float[] Heights, Mask;
    readonly Vector2 center;
    readonly float radius;
    public ErosionPatch(MapData map, Vector3 point, float radius)
    {
        this.radius = radius;
        float cx = point.X + map.Width / 2f, cz = point.Z + map.Depth / 2f;
        MinX = Math.Max(0, (int)MathF.Floor(cx - radius) - 2); MinZ = Math.Max(0, (int)MathF.Floor(cz - radius) - 2);
        Width = Math.Min(map.Width, (int)MathF.Ceiling(cx + radius) + 2) - MinX + 1;
        Depth = Math.Min(map.Depth, (int)MathF.Ceiling(cz + radius) + 2) - MinZ + 1;
        center = new(cx - MinX, cz - MinZ);
        Heights = new float[Width * Depth]; Mask = new float[Heights.Length];
        for (int z = 0; z < Depth; z++)
        for (int x = 0; x < Width; x++)
        {
            int i = z * Width + x; Heights[i] = map.VertexHeight(x + MinX, z + MinZ);
            float t = Math.Clamp((radius - Vector2.Distance(new(x, z), center)) / Math.Max(1, radius * .25f), 0, 1);
            Mask[i] = t * t * (3 - 2 * t);
        }
    }
    bool Inside(Vector2 point) => point.X >= 0 && point.Y >= 0 && point.X < Width - 1 && point.Y < Depth - 1 && Vector2.Distance(point, center) < radius;
    (float Height, Vector2 Gradient) Sample(Vector2 point)
    {
        int x = Math.Clamp((int)point.X, 0, Width - 2), z = Math.Clamp((int)point.Y, 0, Depth - 2);
        float u = point.X - x, v = point.Y - z;
        float a = Heights[z * Width + x], b = Heights[z * Width + x + 1], c = Heights[(z + 1) * Width + x], d = Heights[(z + 1) * Width + x + 1];
        return ((a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v,
            new((b - a) * (1 - v) + (d - c) * v, (c - a) * (1 - u) + (d - b) * u));
    }
    float Deposit(Vector2 point, float amount)
    {
        if (amount <= 0) return 0;
        int x = Math.Clamp((int)point.X, 0, Width - 2), z = Math.Clamp((int)point.Y, 0, Depth - 2);
        float u = point.X - x, v = point.Y - z;
        (int Index, float Weight)[] vertices = [(z * Width + x, (1 - u) * (1 - v)), (z * Width + x + 1, u * (1 - v)),
            ((z + 1) * Width + x, (1 - u) * v), ((z + 1) * Width + x + 1, u * v)];
        float weight = vertices.Sum(vertex => vertex.Weight * Mask[vertex.Index]);
        if (weight <= .00001f) return 0;
        float deposited = 0;
        foreach (var vertex in vertices)
        {
            float share = Math.Min(MapData.MaxElevation - Heights[vertex.Index], amount * vertex.Weight * Mask[vertex.Index] / weight);
            Heights[vertex.Index] += share; deposited += share;
        }
        return deposited;
    }
    float Erode(Vector2 point, float amount)
    {
        int cx = (int)point.X, cz = (int)point.Y;
        float total = 0;
        for (int z = Math.Max(0, cz - 2); z <= Math.Min(Depth - 1, cz + 2); z++)
        for (int x = Math.Max(0, cx - 2); x <= Math.Min(Width - 1, cx + 2); x++)
            total += Math.Max(0, 2 - Vector2.Distance(point, new(x, z))) * Mask[z * Width + x];
        if (total <= .00001f) return 0;
        float falloff = Math.Clamp((radius - Vector2.Distance(point, center)) / Math.Max(1, radius * .25f), 0, 1);
        amount *= falloff * falloff * (3 - 2 * falloff);
        float removed = 0;
        for (int z = Math.Max(0, cz - 2); z <= Math.Min(Depth - 1, cz + 2); z++)
        for (int x = Math.Max(0, cx - 2); x <= Math.Min(Width - 1, cx + 2); x++)
        {
            int index = z * Width + x;
            float weight = Math.Max(0, 2 - Vector2.Distance(point, new(x, z))) * Mask[index];
            float share = Math.Min(Heights[index] - MapData.MinElevation, amount * weight / total);
            Heights[index] -= share; removed += share;
        }
        return removed;
    }
    public void Hydraulic(int droplets, float strength, int seed)
    {
        var random = new Random(seed);
        for (int drop = 0; drop < droplets; drop++)
        {
            float angle = random.NextSingle() * MathF.Tau, distance = MathF.Sqrt(random.NextSingle()) * radius * .95f;
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            if (!Inside(position)) continue;
            Vector2 direction = Vector2.Zero;
            float speed = 1, water = 1, sediment = 0;
            for (int life = 0; life < 48; life++)
            {
                var previous = Sample(position);
                direction = direction * .08f - previous.Gradient * .92f;
                if (direction.LengthSquared() < .000001f) break;
                direction = Vector2.Normalize(direction);
                var next = position + direction;
                if (!Inside(next)) break;
                float change = Sample(next).Height - previous.Height;
                float capacity = Math.Max(-change, .01f) * speed * water * 4;
                if (change > 0 || sediment > capacity)
                {
                    float amount = change > 0 ? Math.Min(change, sediment) : (sediment - capacity) * .3f;
                    sediment -= Deposit(position, amount);
                }
                else
                    sediment += Erode(position, Math.Min((capacity - sediment) * .25f * strength, Math.Max(0, -change) * .8f));
                speed = MathF.Sqrt(Math.Max(.01f, speed * speed - change * 4));
                water *= .96f; position = next;
            }
            Deposit(position, sediment);
        }
    }
    public void Talus(int iterations, float strength)
    {
        var changes = new float[Heights.Length];
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            Array.Clear(changes);
            for (int z = 0; z < Depth; z++)
            for (int x = 0; x < Width; x++)
            {
                int index = z * Width + x; if (Mask[index] <= 0) continue;
                int lower = -1; float excess = 0;
                for (int nz = Math.Max(0, z - 1); nz <= Math.Min(Depth - 1, z + 1); nz++)
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(Width - 1, x + 1); nx++)
                {
                    int neighbour = nz * Width + nx;
                    if (neighbour == index || Mask[neighbour] <= 0) continue;
                    float difference = Heights[index] - Heights[neighbour] - .6f * Vector2.Distance(new(x, z), new(nx, nz));
                    if (difference > excess) { excess = difference; lower = neighbour; }
                }
                if (lower < 0) continue;
                float moved = excess * .2f * strength * Math.Min(Mask[index], Mask[lower]);
                changes[index] -= moved; changes[lower] += moved;
            }
            for (int i = 0; i < Heights.Length; i++) Heights[i] = Math.Clamp(Heights[i] + changes[i], MapData.MinElevation, MapData.MaxElevation);
        }
    }
}

sealed partial class MapDocument
{
    public int ErodeTerrain(Vector3 center, float radius, float strength, int iterations, ErosionKind kind, int seed)
    {
        if (!Map.Contains(center.X, center.Z) || !MapData.FiniteRange(radius, 1, 64) || !MapData.FiniteRange(strength, .05f, 1) ||
            iterations is < 50 or > 2000 || !Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(radius));
        var patch = new ErosionPatch(Map, center, radius);
        if (kind == ErosionKind.Hydraulic) patch.Hydraulic(iterations, strength, seed);
        else patch.Talus(Math.Clamp(iterations / 20, 1, 100), strength);
        var changed = new List<(int X, int Z, float Height)>();
        for (int z = 0; z < patch.Depth; z++)
        for (int x = 0; x < patch.Width; x++)
        {
            int index = z * patch.Width + x;
            if (patch.Mask[index] <= 0) continue;
            float value = MapData.SnapElevation(patch.Heights[index]);
            if (value != Map.VertexHeight(x + patch.MinX, z + patch.MinZ)) changed.Add((x + patch.MinX, z + patch.MinZ, value));
        }
        if (changed.Count == 0) return 0;
        Checkpoint();
        foreach (var change in changed) ChangeHeight(change.X, change.Z, change.Height);
        return changed.Count;
    }
}
