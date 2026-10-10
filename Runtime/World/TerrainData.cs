using System.Numerics;
using System.Text.Json.Serialization;

enum SculptMode { Raise, Lower, Smooth, Flatten }

sealed partial class MapData
{
    public const float MinElevation = -32, MaxElevation = 64, ElevationStep = .25f;
    float[] heights = [];
    float minimumHeight, maximumHeight;
    public float[] Heights
    {
        get => heights;
        set
        {
            heights = value;
            if (heights != null)
                for (int i = 0; i < heights.Length; i++)
                    if (float.IsFinite(heights[i])) heights[i] = SnapElevation(heights[i]);
            RecalculateHeightBounds();
        }
    }
    public float WaterLevel { get; set; } = 0;
    public float WaveHeight { get; set; } = .08f;
    [JsonIgnore] public int VertexCount => (Width + 1) * (Depth + 1);
    [JsonIgnore] public float MinimumHeight => minimumHeight;
    [JsonIgnore] public float MaximumHeight => maximumHeight;
    void RecalculateHeightBounds()
    {
        minimumHeight = maximumHeight = 0;
        if (heights == null) return;
        foreach (float height in heights) { minimumHeight = Math.Min(minimumHeight, height); maximumHeight = Math.Max(maximumHeight, height); }
    }
    void ValidateTerrain()
    {
        if (Heights == null || (Heights.Length != 0 && Heights.Length != VertexCount) ||
            Heights.Any(h => !float.IsFinite(h) || h < MinElevation || h > MaxElevation) ||
            !float.IsFinite(WaterLevel) || WaterLevel < MinElevation || WaterLevel > MaxElevation ||
            !float.IsFinite(WaveHeight) || WaveHeight < 0 || WaveHeight > .25f)
            throw new InvalidDataException("Invalid terrain heights or water settings.");
    }
    public float VertexHeight(int x, int z) => Heights.Length == 0 ? 0 : Heights[z * (Width + 1) + x];
    public Vector3 Vertex(int x, int z) => new(x - Width / 2f, VertexHeight(x, z), z - Depth / 2f);
    public void SetVertexHeight(int x, int z, float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (Heights.Length == 0) Heights = new float[VertexCount];
        value = SnapElevation(Math.Clamp(value, MinElevation, MaxElevation));
        Heights[z * (Width + 1) + x] = value;
        minimumHeight = Math.Min(minimumHeight, value); maximumHeight = Math.Max(maximumHeight, value);
    }
    public static float SnapElevation(float value) => MathF.Round(value / ElevationStep, MidpointRounding.AwayFromZero) * ElevationStep;
    public float HeightAt(float x, float z)
    {
        float gx = Math.Clamp(x + Width / 2f, 0, Width), gz = Math.Clamp(z + Depth / 2f, 0, Depth);
        int tx = Math.Min((int)gx, Width - 1), tz = Math.Min((int)gz, Depth - 1);
        float u = gx - tx, v = gz - tz;
        float a = VertexHeight(tx, tz), b = VertexHeight(tx, tz + 1), c = VertexHeight(tx + 1, tz + 1), d = VertexHeight(tx + 1, tz);
        return u <= v ? a + (c - b) * u + (b - a) * v : a + (d - a) * u + (c - d) * v;
    }
    public Vector3 GroundPosition(float x, float z) => new(x, HeightAt(x, z), z);
    public Vector3 GroundPosition(SceneObject item) => GroundPosition(item.X, item.Z);
    public float WaterDepthAt(float x, float z)
    {
        if (!Contains(x, z)) return 0;
        int tx = (int)MathF.Floor(x + Width / 2f), tz = (int)MathF.Floor(z + Depth / 2f);
        int triangle = x + Width / 2f - tx <= z + Depth / 2f - tz ? 0 : 1;
        return (WaterMask(tz * Width + tx) & (1 << triangle)) != 0 ? Math.Max(0, WaterLevel - HeightAt(x, z)) : 0;
    }
}

sealed partial class MapDocument
{
    readonly Dictionary<int, float> sculptValues = [];
    public void EndSculptStroke() => sculptValues.Clear();
    void SaveHeightState()
    {
        savedHeights = (float[])Map.Heights.Clone(); savedWidth = Map.Width; savedDepth = Map.Depth; changedHeights = 0;
    }
    void UpdateChangedHeights()
    {
        changedHeights = 0;
        if (Map.Width != savedWidth || Map.Depth != savedDepth) return;
        for (int i = 0; i < Map.VertexCount; i++)
            if ((Map.Heights.Length == 0 ? 0 : Map.Heights[i]) != (savedHeights.Length == 0 ? 0 : savedHeights[i])) changedHeights++;
    }
    void ChangeHeight(int x, int z, float value)
    {
        value = MapData.SnapElevation(Math.Clamp(value, MapData.MinElevation, MapData.MaxElevation));
        float previous = Map.VertexHeight(x, z);
        if (value == previous) return;
        if (savedTiles != null && Map.Width == savedWidth && Map.Depth == savedDepth)
        {
            float original = savedHeights.Length == 0 ? 0 : savedHeights[z * (Map.Width + 1) + x];
            changedHeights += (value != original ? 1 : 0) - (previous != original ? 1 : 0);
        }
        Map.SetVertexHeight(x, z, value);
    }
    // One checkpoint per stroke is made by the editor, including held-button sculpting.
    public void Sculpt(Vector3 point, float radius, float amount, SculptMode mode, float target = 0)
    {
        if (!float.IsFinite(radius) || radius <= 0 || !float.IsFinite(amount) || amount < 0 || !float.IsFinite(target))
            throw new ArgumentOutOfRangeException(nameof(radius));
        float cx = point.X + Map.Width / 2f, cz = point.Z + Map.Depth / 2f;
        var changes = new List<(int X, int Z, float Height)>();
        for (int z = Math.Max(0, (int)MathF.Floor(cz - radius)); z <= Math.Min(Map.Depth, (int)MathF.Ceiling(cz + radius)); z++)
        for (int x = Math.Max(0, (int)MathF.Floor(cx - radius)); x <= Math.Min(Map.Width, (int)MathF.Ceiling(cx + radius)); x++)
        {
            float distance = Vector2.Distance(new(x, z), new(cx, cz));
            if (distance >= radius) continue;
            float falloff = 1 - distance / radius; falloff = falloff * falloff * (3 - 2 * falloff);
            int index = z * (Map.Width + 1) + x;
            float height = Map.VertexHeight(x, z), next;
            // Preserve changes smaller than 0.25 between frames; only the stored mesh is snapped.
            if (sculptValues.TryGetValue(index, out float accumulated) && MapData.SnapElevation(accumulated) == height) height = accumulated;
            if (mode is SculptMode.Raise or SculptMode.Lower) next = height + (mode == SculptMode.Raise ? 1 : -1) * amount * falloff;
            else
            {
                float destination = target;
                if (mode == SculptMode.Smooth)
                {
                    float sum = 0; int count = 0;
                    for (int nz = Math.Max(0, z - 1); nz <= Math.Min(Map.Depth, z + 1); nz++)
                    for (int nx = Math.Max(0, x - 1); nx <= Math.Min(Map.Width, x + 1); nx++)
                    { sum += Map.VertexHeight(nx, nz); count++; }
                    destination = sum / count;
                }
                next = height + (destination - height) * Math.Clamp(amount * falloff, 0, 1);
            }
            next = Math.Clamp(next, MapData.MinElevation, MapData.MaxElevation);
            sculptValues[index] = next;
            changes.Add((x, z, next));
        }
        foreach (var change in changes) ChangeHeight(change.X, change.Z, change.Height);
    }
}
