using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

static class ProjectPaths
{
    public static string Root { get; } = FindRoot();
    public static string MapFile => Path.Combine(Root, "Runtime", "Maps", "world.json");
    public static string AssetRoot => Path.Combine(Root, "Editor");
    static string FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "GameEngine.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Run the application inside the game repository.");
    }
    public static bool ValidAssetId(string id) => !string.IsNullOrWhiteSpace(id) &&
        !Path.IsPathRooted(id) && !id.Contains('\\') && !id.Contains(':') &&
        id.Split('/').All(part => part.Length > 0 && part != "." && part != "..");
}

class SceneObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Asset { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Height { get; set; } = 2;
    public bool Solid { get; set; } = true;
    public ColliderData? Collider { get; set; }
    public bool IsDoor { get; set; }
    public bool DoorOpen { get; set; }
    public bool FadeWhenBlocking { get; set; } = true;
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
}

sealed class PlayerStart
{
    public float X { get; set; }
    public float Z { get; set; }
    public string? Asset { get; set; }
    public float Height { get; set; } = 1.6f;
    public InventoryGrid Inventory { get; set; } = new();
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
}

sealed partial class MapData
{
    public const int MinSize = 4, MaxSize = 2048;
    public int Version { get; set; } = 1;
    public int Width { get; set; } = 48;
    public int Depth { get; set; } = 48;
    public string[] Tiles { get; set; } = Enumerable.Repeat("grass", 48 * 48).ToArray();
    public List<SceneObject> Objects { get; set; } = [];
    public List<NpcData> Npcs { get; set; } = [];
    public PlayerStart Player { get; set; } = new();
    public RuntimeSettings Runtime { get; set; } = new();
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    static readonly JsonSerializerOptions MetadataOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { info =>
            {
                if (info.Type == typeof(MapData))
                    foreach (var property in info.Properties)
                        if (property.Name is nameof(Tiles) or nameof(Heights) or nameof(WaterCells)) property.ShouldSerialize = (_, _) => false;
            } }
        }
    };
    public string SerializeMetadata() => JsonSerializer.Serialize(this, MetadataOptions);
    public static MapData ParseMetadata(string json) => JsonSerializer.Deserialize<MapData>(json, JsonOptions) ?? throw new InvalidDataException("Empty map.");
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);
    public static MapData Parse(string json)
    {
        var map = JsonSerializer.Deserialize<MapData>(json, JsonOptions) ?? throw new InvalidDataException("Empty map.");
        map.Validate();
        return map;
    }
    public static MapData Load(string path)
    {
        using var stream = File.OpenRead(path);
        var map = JsonSerializer.Deserialize<MapData>(stream, JsonOptions) ?? throw new InvalidDataException("Empty map.");
        map.Validate(); return map;
    }
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        using (var stream = File.Create(temporary)) JsonSerializer.Serialize(stream, this, JsonOptions);
        File.Move(temporary, path, true);
    }
    public bool Contains(float x, float z) => float.IsFinite(x) && float.IsFinite(z) &&
        x >= -Width / 2f && x < Width / 2f && z >= -Depth / 2f && z < Depth / 2f;
    public Vector3 Clamp(Vector3 p) => GroundPosition(Math.Clamp(p.X, -Width / 2f + .4f, Width / 2f - .4f),
        Math.Clamp(p.Z, -Depth / 2f + .4f, Depth / 2f - .4f));
    public void Validate()
    {
        if (Version != 1 || Width < MinSize || Width > MaxSize || Depth < MinSize || Depth > MaxSize ||
            Tiles == null || Tiles.Length != Width * Depth || Tiles.Distinct().Any(t => !ProjectPaths.ValidAssetId(t)))
            throw new InvalidDataException("Invalid map dimensions, version or terrain.");
        ValidateTerrain();
        ValidateWater();
        if (Runtime == null) throw new InvalidDataException("Missing runtime settings.");
        Runtime.Validate();
        if (Player == null || !Contains(Player.X, Player.Z) || !ValidHeight(Player.Height) ||
            (Player.Asset != null && !ProjectPaths.ValidAssetId(Player.Asset)))
            throw new InvalidDataException("Invalid player start.");
        if (Objects == null || Objects.Count > 10000 || Objects.Any(o => o == null ||
            string.IsNullOrWhiteSpace(o.Id) || !ProjectPaths.ValidAssetId(o.Asset) || !Contains(o.X, o.Z) || !ValidHeight(o.Height)) ||
            Objects.Select(o => o.Id).Distinct().Count() != Objects.Count)
            throw new InvalidDataException("Invalid objects.");
        if (Npcs == null || Npcs.Count > 1000 || Npcs.Any(n => n == null || string.IsNullOrWhiteSpace(n.Id) ||
            string.IsNullOrWhiteSpace(n.Name) || n.Name.Length > 80 || !Contains(n.X, n.Z) || !ValidHeight(n.Height) ||
            (n.Asset != "" && !ProjectPaths.ValidAssetId(n.Asset)) || n.Dialogue == null) ||
            Objects.Select(o => o.Id).Concat(Npcs.Select(n => n.Id)).Distinct().Count() != Objects.Count + Npcs.Count)
            throw new InvalidDataException("Invalid NPCs.");
        foreach (var npc in Npcs) npc.Dialogue.Validate();
        ValidateMonsters();
        ValidateInventory();
        ValidateWorldFeatures();
    }
    static bool ValidHeight(float h) => float.IsFinite(h) && h >= .25f && h <= 12;
    public static MapData CreateEmpty(int width = 48, int depth = 48)
    {
        if (width < MinSize || width > MaxSize || depth < MinSize || depth > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(width), $"Map sides must be {MinSize}-{MaxSize} tiles.");
        return new() { Width = width, Depth = depth, Tiles = Enumerable.Repeat("grass", width * depth).ToArray() };
    }
    public static MapData CreateDemo(IEnumerable<string> assetIds)
    {
        var map = new MapData();
        var ids = assetIds.ToHashSet();
        void Add(string id, float x, float z, float height, bool solid = true)
        {
            if (ids.Contains(id)) map.Objects.Add(new() { Asset = id, X = x, Z = z, Height = height, Solid = solid });
        }
        Add("objects/vegetation/trees/tree1.png", -4, -4, 5);
        Add("objects/vegetation/trees/tree2.png", -5, 4, 4.5f);
        Add("objects/rocks/large/rock_01.png", 1, -5, 2);
        Add("objects/props/camp/campfire.png", 1, 3, 1.3f, false);
        Add("objects/vegetation/bushes/bush_01.png", 4, 5, 1.8f);
        if (ids.Contains(ChestData.DefaultAsset))
        {
            var chest = new ChestData { X = -1, Z = 1 };
            foreach (var item in ItemCatalog.All) chest.Contents.Add(item.Id);
            map.Chests.Add(chest);
        }
        for (int z = 0; z < map.Depth; z++)
            for (int x = 22; x <= 24; x++) map.Tiles[z * map.Width + x] = "dirt";
        return map;
    }
}

sealed partial class MapDocument(MapData initial, bool saved)
{
    public MapData Map { get; private set; } = initial;
    readonly List<MapSnapshot> undo = [], redo = [];
    string savedState = saved ? initial.SerializeMetadata() : "";
    string[]? savedTiles = saved ? (string[])initial.Tiles.Clone() : null;
    float[] savedHeights = saved ? (float[])initial.Heights.Clone() : [];
    byte[] savedWater = saved ? initial.CopyWaterCells() : [];
    int savedWidth = initial.Width, savedDepth = initial.Depth;
    int changedTiles;
    int changedHeights;
    int changedWater;
    public bool Dirty => savedTiles == null || changedTiles != 0 || changedHeights != 0 || changedWater != 0 || Map.SerializeMetadata() != savedState;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Checkpoint()
    {
        EndSculptStroke();
        var state = MapSnapshot.Capture(Map);
        if (undo.Count == 0 || !undo[^1].Matches(state)) undo.Add(state);
        redo.Clear();
        TrimHistory();
    }
    public void Undo() => Restore(undo, redo);
    public void Redo() => Restore(redo, undo);
    void Restore(List<MapSnapshot> source, List<MapSnapshot> destination)
    {
        if (source.Count == 0) return;
        destination.Add(MapSnapshot.Capture(Map));
        Map = source[^1].Restore();
        source.RemoveAt(source.Count - 1);
        EndSculptStroke(); UpdateChangedTiles(); UpdateChangedHeights(); UpdateChangedWater(); TrimHistory();
    }
    void TrimHistory()
    {
        const long budget = 128L * 1024 * 1024;
        while (undo.Count + redo.Count > 1 && (undo.Count + redo.Count > 80 || undo.Sum(s => s.SizeBytes) + redo.Sum(s => s.SizeBytes) > budget))
        {
            var oldest = undo.Count > 1 || redo.Count == 0 ? undo : redo;
            oldest.RemoveAt(0);
        }
    }
    void UpdateChangedTiles()
    {
        changedTiles = 0;
        if (savedTiles == null) return;
        if (savedTiles.Length != Map.Tiles.Length) { changedTiles = 1; return; }
        for (int i = 0; i < savedTiles.Length; i++) if (savedTiles[i] != Map.Tiles[i]) changedTiles++;
    }
    public void Save(string path)
    {
        Map.Save(path); savedState = Map.SerializeMetadata(); savedTiles = (string[])Map.Tiles.Clone(); changedTiles = 0;
        SaveHeightState();
        savedWater = Map.CopyWaterCells(); changedWater = 0; EndSculptStroke();
    }
    public void Load(string path)
    {
        var loaded = MapData.Load(path);
        Map = loaded; undo.Clear(); redo.Clear(); savedState = Map.SerializeMetadata(); savedTiles = (string[])Map.Tiles.Clone(); changedTiles = 0;
        SaveHeightState();
        savedWater = Map.CopyWaterCells(); changedWater = 0; EndSculptStroke();
    }
    public void New(int width = 48, int depth = 48)
    {
        var map = MapData.CreateEmpty(width, depth);
        Checkpoint(); Map = map; UpdateChangedTiles(); UpdateChangedHeights(); UpdateChangedWater();
    }
    public NpcData PlaceNpc(string? asset, Vector3 position)
    {
        Checkpoint();
        var p = Map.Clamp(position);
        var npc = new NpcData { Asset = asset ?? "", X = p.X, Z = p.Z };
        Map.Npcs.Add(npc);
        return npc;
    }
    public SceneObject Place(string asset, Vector3 position, float height, bool solid)
    {
        Checkpoint();
        var p = Map.Clamp(position);
        var item = new SceneObject { Asset = asset, X = p.X, Z = p.Z, Height = height, Solid = solid };
        Map.Objects.Add(item);
        return item;
    }
    // The caller checkpoints once per brush stroke, not once per cell.
    public void Paint(Vector3 position, int radius, string material)
    {
        int cx = (int)MathF.Floor(position.X + Map.Width / 2f), cz = (int)MathF.Floor(position.Z + Map.Depth / 2f);
        for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if (x >= 0 && z >= 0 && x < Map.Width && z < Map.Depth)
                {
                    int index = z * Map.Width + x;
                    if (savedTiles != null && savedTiles.Length == Map.Tiles.Length)
                        changedTiles += (material != savedTiles[index] ? 1 : 0) - (Map.Tiles[index] != savedTiles[index] ? 1 : 0);
                    Map.Tiles[index] = material;
                }
    }
}
