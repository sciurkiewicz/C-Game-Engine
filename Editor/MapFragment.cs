using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

readonly record struct MapRegion(int X, int Z, int Width, int Depth)
{
    public bool Fits(MapData map) => X >= 0 && Z >= 0 && Width >= 1 && Depth >= 1 && X <= map.Width - Width && Z <= map.Depth - Depth;
    public FragmentBounds WorldBounds(MapData map) => new(X - map.Width / 2f, Z - map.Depth / 2f, X + Width - map.Width / 2f, Z + Depth - map.Depth / 2f);
    public static MapRegion FromDrag(MapData map, Vector3 a, Vector3 b)
    {
        int minX = Math.Clamp((int)MathF.Floor(Math.Min(a.X, b.X) + map.Width / 2f), 0, map.Width - 1);
        int minZ = Math.Clamp((int)MathF.Floor(Math.Min(a.Z, b.Z) + map.Depth / 2f), 0, map.Depth - 1);
        int maxX = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, b.X) + map.Width / 2f), minX + 1, map.Width);
        int maxZ = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Z, b.Z) + map.Depth / 2f), minZ + 1, map.Depth);
        return new(minX, minZ, maxX - minX, maxZ - minZ);
    }
}

sealed class FragmentTrigger
{
    public TriggerData Data { get; set; } = new();
    public bool LocalDestination { get; set; }
}

sealed class MapFragment
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Map fragment";
    public int Width { get; set; }
    public int Depth { get; set; }
    public string[] Tiles { get; set; } = [];
    public float[] Heights { get; set; } = [];
    public byte[] WaterCells { get; set; } = [];
    public float WaterLevel { get; set; }
    public ScenePrefab Actors { get; set; } = new();
    public List<RoadData> Roads { get; set; } = [];
    public List<WallData> Walls { get; set; } = [];
    public List<GridWallCell> WallCells { get; set; } = [];
    public List<PointLightData> Lights { get; set; } = [];
    public List<SoundEmitterData> Sounds { get; set; } = [];
    public List<FragmentTrigger> Triggers { get; set; } = [];
    [JsonIgnore] public float MinimumHeight { get; private set; }
    [JsonIgnore] public float MaximumHeight { get; private set; }
    [JsonIgnore] public bool HasWater { get; private set; }
    public bool Contains(float x, float z) => float.IsFinite(x) && float.IsFinite(z) && x >= 0 && z >= 0 && x < Width && z < Depth;
    public float VertexHeight(int x, int z) => Heights.Length == 0 ? 0 : Heights[z * (Width + 1) + x];
    public byte WaterMask(int cell) => WaterCells.Length > 0 ? WaterCells[cell] : Tiles[cell] == "water" ? (byte)3 : (byte)0;
    public float HeightAt(float x, float z)
    {
        float px = Math.Clamp(x, 0, Width), pz = Math.Clamp(z, 0, Depth);
        int tx = Math.Min((int)px, Width - 1), tz = Math.Min((int)pz, Depth - 1); float u = px - tx, v = pz - tz;
        float a = VertexHeight(tx, tz), b = VertexHeight(tx, tz + 1), c = VertexHeight(tx + 1, tz + 1), d = VertexHeight(tx + 1, tz);
        return u <= v ? a + (c - b) * u + (b - a) * v : a + (d - a) * u + (c - d) * v;
    }
    public static MapFragment Capture(MapData map, MapRegion region)
    {
        if (!region.Fits(map)) throw new InvalidDataException("Select a rectangular area inside the map.");
        var bounds = region.WorldBounds(map);
        var fragment = new MapFragment { Width = region.Width, Depth = region.Depth, WaterLevel = map.WaterLevel, Tiles = new string[region.Width * region.Depth] };
        var water = new byte[fragment.Tiles.Length];
        for (int z = 0; z < region.Depth; z++)
        for (int x = 0; x < region.Width; x++)
        {
            int source = (region.Z + z) * map.Width + region.X + x, destination = z * region.Width + x;
            fragment.Tiles[destination] = map.Tiles[source]; water[destination] = map.WaterMask(source);
        }
        if (water.Any(value => value != 0) || fragment.Tiles.Contains("water")) fragment.WaterCells = water;
        if (map.Heights.Length > 0)
        {
            fragment.Heights = new float[(region.Width + 1) * (region.Depth + 1)];
            for (int z = 0; z <= region.Depth; z++)
            for (int x = 0; x <= region.Width; x++) fragment.Heights[z * (region.Width + 1) + x] = map.VertexHeight(region.X + x, region.Z + z);
        }
        foreach (var item in map.SceneEntities.Where(e => bounds.Contains(e.X, e.Z)))
        {
            var copy = ScenePrefab.CloneEntity(item); copy.X -= bounds.MinX; copy.Z -= bounds.MinZ;
            if (copy is NpcData npc) fragment.Actors.Npcs.Add(npc);
            else if (copy is ChestData chest) fragment.Actors.Chests.Add(chest);
            else if (copy is MonsterData monster) fragment.Actors.Monsters.Add(monster);
            else fragment.Actors.Objects.Add(copy);
        }
        fragment.Roads = FragmentRoads.Capture(map.Roads, bounds);
        fragment.Walls = FragmentWalls.Capture(map.Walls, bounds);
        fragment.WallCells = map.WallCells.Where(c => c.X >= region.X && c.Z >= region.Z && c.X < region.X + region.Width && c.Z < region.Z + region.Depth)
            .Select(c => c.Copy(c.X - region.X, c.Z - region.Z)).ToList();
        foreach (var light in map.Lights.Where(l => bounds.Contains(l.X, l.Z)))
        { var copy = ScenePrefab.Clone(light); copy.X -= bounds.MinX; copy.Z -= bounds.MinZ; fragment.Lights.Add(copy); }
        foreach (var sound in map.Sounds.Where(s => bounds.Contains(s.X, s.Z)))
        { var copy = ScenePrefab.Clone(sound); copy.X -= bounds.MinX; copy.Z -= bounds.MinZ; fragment.Sounds.Add(copy); }
        foreach (var trigger in map.Triggers.Where(t => bounds.Contains(t.X, t.Z)))
        {
            var copy = ScenePrefab.Clone(trigger); copy.X -= bounds.MinX; copy.Z -= bounds.MinZ;
            bool local = trigger.Action is TriggerAction.Teleport or TriggerAction.SpawnMonster && bounds.Contains(trigger.DestinationX, trigger.DestinationZ);
            if (local) { copy.DestinationX -= bounds.MinX; copy.DestinationZ -= bounds.MinZ; }
            fragment.Triggers.Add(new() { Data = copy, LocalDestination = local });
        }
        fragment.Validate(); return fragment;
    }
    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Width is < 1 or > MapData.MaxSize || Depth is < 1 or > MapData.MaxSize ||
            Tiles == null || Tiles.Length != Width * Depth || Tiles.Any(t => !ProjectPaths.ValidAssetId(t)) || Heights == null ||
            (Heights.Length != 0 && Heights.Length != (Width + 1) * (Depth + 1)) ||
            Heights.Any(h => !MapData.FiniteRange(h, MapData.MinElevation, MapData.MaxElevation) || h != MapData.SnapElevation(h)) ||
            WaterCells == null || (WaterCells.Length != 0 && WaterCells.Length != Tiles.Length) || WaterCells.Any(mask => mask > 3) ||
            !MapData.FiniteRange(WaterLevel, MapData.MinElevation, MapData.MaxElevation) || Actors == null || Actors.Objects == null || Actors.Npcs == null || Actors.Chests == null || Actors.Monsters == null ||
            Actors.Version != 1 || string.IsNullOrWhiteSpace(Actors.Name) || Actors.Name.Length > 80 ||
            Actors.Objects.Count > 10000 || Actors.Npcs.Count > 1000 || Actors.Chests.Count > 1000 || Actors.Monsters.Count > 1000 ||
            Actors.Entities.Any(e => e == null || !Contains(e.X, e.Z)) || Roads == null || Roads.Count > 128 || Roads.Any(r => r == null) ||
            Roads.Select(r => r.Id).Distinct().Count() != Roads.Count || Lights == null || Lights.Count > 256 || Lights.Any(l => l == null || !Contains(l.X, l.Z)) ||
            Sounds == null || Sounds.Count > 256 || Sounds.Any(s => s == null || !Contains(s.X, s.Z)) || Triggers == null || Triggers.Count > 1000 ||
            Triggers.Any(t => t?.Data == null || !Contains(t.Data.X, t.Data.Z) || !float.IsFinite(t.Data.DestinationX) || !float.IsFinite(t.Data.DestinationZ) ||
                (t.LocalDestination && (t.Data.Action is not (TriggerAction.Teleport or TriggerAction.SpawnMonster) || !Contains(t.Data.DestinationX, t.Data.DestinationZ)))))
            throw new InvalidDataException("Invalid map fragment dimensions, terrain or contents.");
        foreach (var road in Roads) road.Validate(Contains);
        if (Walls == null || Walls.Count > WallData.Limit || Walls.Any(w => w == null) || Walls.Select(w => w.Id).Distinct().Count() != Walls.Count)
            throw new InvalidDataException("Invalid fragment walls.");
        foreach (var wall in Walls) wall.Validate(Contains);
        if (WallCells == null || WallCells.Count > GridWallCell.Limit || WallCells.Any(c => c == null) || WallCells.Select(c => (c.X, c.Z)).Distinct().Count() != WallCells.Count)
            throw new InvalidDataException("Invalid fragment dungeon walls.");
        foreach (var cell in WallCells) cell.Validate(Width, Depth);
        // Validate nested actor, inventory, dialogue and world-feature data in a tiny map.
        var test = MapData.CreateEmpty(4, 4); var actors = ScenePrefab.Clone(Actors);
        test.Objects = actors.Objects; test.Npcs = actors.Npcs; test.Chests = actors.Chests; test.Monsters = actors.Monsters;
        foreach (var entity in test.SceneEntities) entity.X = entity.Z = 0;
        test.Lights = ScenePrefab.Clone(Lights); foreach (var light in test.Lights) light.X = light.Z = 0;
        test.Sounds = ScenePrefab.Clone(Sounds); foreach (var sound in test.Sounds) sound.X = sound.Z = 0;
        test.Triggers = Triggers.Select(t => ScenePrefab.Clone(t.Data)).ToList();
        foreach (var trigger in test.Triggers) trigger.X = trigger.Z = trigger.DestinationX = trigger.DestinationZ = 0;
        test.Validate();
        MinimumHeight = Heights.Length == 0 ? 0 : Heights.Min(); MaximumHeight = Heights.Length == 0 ? 0 : Heights.Max();
        HasWater = WaterCells.Length > 0 ? WaterCells.Any(mask => mask != 0) : Tiles.Contains("water");
    }
    public string Save(string directory)
    {
        Validate(); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        using (var stream = File.Create(path + ".tmp")) JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        File.Move(path + ".tmp", path); return path;
    }
    public static MapFragment Load(string path)
    {
        using var stream = File.OpenRead(path);
        var fragment = JsonSerializer.Deserialize<MapFragment>(stream) ?? throw new InvalidDataException("Empty map fragment.");
        fragment.Validate(); return fragment;
    }
}
