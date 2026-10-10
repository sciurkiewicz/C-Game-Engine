// Terrain runs keep undo and playtest copies small even on a 2048 x 2048 map.
// The on-disk JSON format remains compatible with existing maps.
sealed class MapSnapshot
{
    readonly string metadata;
    readonly TerrainRun[] terrain;
    readonly HeightRun[] heights;
    readonly WaterRun[] water;
    MapSnapshot(string metadata, TerrainRun[] terrain, HeightRun[] heights, WaterRun[] water)
    { this.metadata = metadata; this.terrain = terrain; this.heights = heights; this.water = water; }
    public readonly record struct TerrainRun(string Material, int Count);
    public readonly record struct HeightRun(float Height, int Count);
    public readonly record struct WaterRun(byte Mask, int Count);
    public long SizeBytes => metadata.Length * 2L + terrain.Length * 16L + heights.Length * 8L + water.Length * 8L;
    public static MapSnapshot Capture(MapData map)
    {
        var runs = new List<TerrainRun>();
        if (map.Tiles.Length > 0)
        {
            string material = map.Tiles[0]; int count = 1;
            for (int i = 1; i < map.Tiles.Length; i++)
            {
                if (map.Tiles[i] == material) count++;
                else { runs.Add(new(material, count)); material = map.Tiles[i]; count = 1; }
            }
            runs.Add(new(material, count));
        }
        var heightRuns = new List<HeightRun>();
        if (map.Heights.Length > 0)
        {
            float height = map.Heights[0]; int count = 1;
            for (int i = 1; i < map.Heights.Length; i++)
            {
                if (map.Heights[i] == height) count++;
                else { heightRuns.Add(new(height, count)); height = map.Heights[i]; count = 1; }
            }
            heightRuns.Add(new(height, count));
            if (heightRuns.Count == 1 && heightRuns[0].Height == 0) heightRuns.Clear();
        }
        var waterRuns = new List<WaterRun>();
        if (map.WaterCells.Length > 0)
        {
            byte mask = map.WaterCells[0]; int count = 1;
            for (int i = 1; i < map.WaterCells.Length; i++)
                if (map.WaterCells[i] == mask) count++;
                else { waterRuns.Add(new(mask, count)); mask = map.WaterCells[i]; count = 1; }
            waterRuns.Add(new(mask, count));
        }
        return new(map.SerializeMetadata(), runs.ToArray(), heightRuns.ToArray(), waterRuns.ToArray());
    }
    public bool Matches(MapSnapshot other) => metadata == other.metadata && terrain.AsSpan().SequenceEqual(other.terrain) &&
        heights.AsSpan().SequenceEqual(other.heights) && water.AsSpan().SequenceEqual(other.water);
    public MapData Restore()
    {
        var map = MapData.ParseMetadata(metadata);
        map.Tiles = new string[map.Width * map.Depth];
        int start = 0;
        foreach (var run in terrain)
        { Array.Fill(map.Tiles, run.Material, start, run.Count); start += run.Count; }
        if (heights.Length > 0)
        {
            var values = new float[map.VertexCount]; start = 0;
            foreach (var run in heights) { Array.Fill(values, run.Height, start, run.Count); start += run.Count; }
            map.Heights = values;
        }
        if (water.Length > 0)
        {
            map.WaterCells = new byte[map.Width * map.Depth]; start = 0;
            foreach (var run in water) { Array.Fill(map.WaterCells, run.Mask, start, run.Count); start += run.Count; }
        }
        map.Validate();
        return map;
    }
}
