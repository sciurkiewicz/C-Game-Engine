using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

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

sealed class SceneObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Asset { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Height { get; set; } = 2;
    public bool Solid { get; set; } = true;
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
}

sealed class PlayerStart
{
    public float X { get; set; }
    public float Z { get; set; }
    public string? Asset { get; set; }
    public float Height { get; set; } = 1.6f;
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
}

sealed class MapData
{
    public int Version { get; set; } = 1;
    public int Width { get; set; } = 48;
    public int Depth { get; set; } = 48;
    public string[] Tiles { get; set; } = Enumerable.Repeat("grass", 48 * 48).ToArray();
    public List<SceneObject> Objects { get; set; } = [];
    public PlayerStart Player { get; set; } = new();
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);
    public static MapData Parse(string json)
    {
        var map = JsonSerializer.Deserialize<MapData>(json, JsonOptions) ?? throw new InvalidDataException("Empty map.");
        map.Validate();
        return map;
    }
    public static MapData Load(string path) => Parse(File.ReadAllText(path));
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, Serialize());
        File.Move(temporary, path, true);
    }
    public bool Contains(float x, float z) => float.IsFinite(x) && float.IsFinite(z) &&
        x >= -Width / 2f && x < Width / 2f && z >= -Depth / 2f && z < Depth / 2f;
    public Vector3 Clamp(Vector3 p) => new(Math.Clamp(p.X, -Width / 2f + .4f, Width / 2f - .4f), 0,
        Math.Clamp(p.Z, -Depth / 2f + .4f, Depth / 2f - .4f));
    public void Validate()
    {
        if (Version != 1 || Width < 4 || Width > 128 || Depth < 4 || Depth > 128 ||
            Tiles == null || Tiles.Length != Width * Depth || Tiles.Any(t => !ProjectPaths.ValidAssetId(t)))
            throw new InvalidDataException("Invalid map dimensions, version or terrain.");
        if (Player == null || !Contains(Player.X, Player.Z) || !ValidHeight(Player.Height) ||
            (Player.Asset != null && !ProjectPaths.ValidAssetId(Player.Asset)))
            throw new InvalidDataException("Invalid player start.");
        if (Objects == null || Objects.Count > 10000 || Objects.Any(o => o == null ||
            string.IsNullOrWhiteSpace(o.Id) || !ProjectPaths.ValidAssetId(o.Asset) || !Contains(o.X, o.Z) || !ValidHeight(o.Height)) ||
            Objects.Select(o => o.Id).Distinct().Count() != Objects.Count)
            throw new InvalidDataException("Invalid objects.");
    }
    static bool ValidHeight(float h) => float.IsFinite(h) && h >= .25f && h <= 12;
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
        for (int z = 0; z < map.Depth; z++)
            for (int x = 22; x <= 24; x++) map.Tiles[z * map.Width + x] = "dirt";
        return map;
    }
}

sealed class MapDocument(MapData initial, bool saved)
{
    public MapData Map { get; private set; } = initial;
    readonly List<string> undo = [], redo = [];
    string savedState = saved ? initial.Serialize() : "";
    public bool Dirty => Map.Serialize() != savedState;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Checkpoint()
    {
        string state = Map.Serialize();
        if (undo.Count == 0 || undo[^1] != state) undo.Add(state);
        if (undo.Count > 80) undo.RemoveAt(0);
        redo.Clear();
    }
    public void Undo() => Restore(undo, redo);
    public void Redo() => Restore(redo, undo);
    void Restore(List<string> source, List<string> destination)
    {
        if (source.Count == 0) return;
        destination.Add(Map.Serialize());
        Map = MapData.Parse(source[^1]);
        source.RemoveAt(source.Count - 1);
    }
    public void Save(string path) { Map.Save(path); savedState = Map.Serialize(); }
    public void Load(string path)
    {
        var loaded = MapData.Load(path);
        Map = loaded; undo.Clear(); redo.Clear(); savedState = Map.Serialize();
    }
    public void New() { Checkpoint(); Map = new MapData(); }
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
                    Map.Tiles[z * Map.Width + x] = material;
    }
}
