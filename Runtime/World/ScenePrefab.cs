using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class ScenePrefab
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Prefab";
    public List<SceneObject> Objects { get; set; } = [];
    public List<NpcData> Npcs { get; set; } = [];
    public List<ChestData> Chests { get; set; } = [];
    public List<MonsterData> Monsters { get; set; } = [];
    [JsonIgnore] public IEnumerable<SceneObject> Entities => Objects.Concat<SceneObject>(Npcs).Concat(Chests).Concat(Monsters);
    public static SceneObject CloneEntity(SceneObject item) => item switch
    {
        ChestData chest => Clone(chest), NpcData npc => Clone(npc), MonsterData monster => Clone(monster), _ => Clone(item)
    };
    public static T Clone<T>(T data) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(data))!;
    void Add(SceneObject item)
    {
        switch (item)
        {
            case ChestData chest: Chests.Add(chest); break;
            case NpcData npc: Npcs.Add(npc); break;
            case MonsterData monster: Monsters.Add(monster); break;
            default: Objects.Add(item); break;
        }
    }
    public static ScenePrefab Capture(string name, IEnumerable<SceneObject> selection)
    {
        var entities = selection.ToArray();
        if (entities.Length == 0) throw new InvalidDataException("Select objects before creating a prefab.");
        var prefab = new ScenePrefab { Name = name.Trim() };
        float x = entities[0].X, z = entities[0].Z;
        foreach (var item in entities)
        {
            var copy = CloneEntity(item); copy.X -= x; copy.Z -= z; prefab.Add(copy);
        }
        prefab.Validate(); return prefab;
    }
    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Objects == null || Npcs == null || Chests == null || Monsters == null)
            throw new InvalidDataException("Invalid prefab name or version.");
        var entries = Entities.ToArray();
        if (entries.Length is < 1 or > 500 || entries.Any(o => o == null || !MapData.FiniteRange(o.X, -2048, 2048) || !MapData.FiniteRange(o.Z, -2048, 2048)))
            throw new InvalidDataException("Invalid prefab entities or offsets.");
        // Validate all actor data and inventory IDs without allocating a full-sized terrain array.
        var test = MapData.CreateEmpty(4, 4);
        var copy = Clone(this);
        foreach (var item in copy.Entities) { item.X = item.Z = 0; }
        test.Objects = copy.Objects; test.Npcs = copy.Npcs; test.Chests = copy.Chests; test.Monsters = copy.Monsters; test.Validate();
    }
    public string Save(string directory)
    {
        Validate(); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); return path;
    }
    public static ScenePrefab Load(string path)
    {
        var prefab = JsonSerializer.Deserialize<ScenePrefab>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty prefab.");
        prefab.Validate(); return prefab;
    }
}

sealed partial class MapDocument
{
    public IReadOnlyList<SceneObject> PlacePrefab(ScenePrefab prefab, Vector3 position)
    {
        prefab.Validate();
        var copy = ScenePrefab.Clone(prefab);
        foreach (var item in copy.Entities)
        {
            item.Id = Guid.NewGuid().ToString("N"); item.X += position.X; item.Z += position.Z;
            if (!Map.Contains(item.X, item.Z)) throw new InvalidDataException("The prefab would extend outside the map.");
            if (item is ChestData chest) foreach (var content in chest.Contents.Items) content.Id = Guid.NewGuid().ToString("N");
        }
        if (Map.Objects.Count + copy.Objects.Count > 10000 || Map.Npcs.Count + copy.Npcs.Count > 1000 ||
            Map.Chests.Count + copy.Chests.Count > 1000 || Map.Monsters.Count + copy.Monsters.Count > 1000)
            throw new InvalidDataException("The prefab would exceed the map's entity limit.");
        Checkpoint(); Map.Objects.AddRange(copy.Objects); Map.Npcs.AddRange(copy.Npcs); Map.Chests.AddRange(copy.Chests); Map.Monsters.AddRange(copy.Monsters);
        return copy.Entities.ToArray();
    }
}
