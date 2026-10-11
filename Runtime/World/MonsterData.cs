using System.Numerics;

sealed class MonsterData : SceneObject
{
    public string Name { get; set; } = "Monster";
    public int MaxHealth { get; set; } = 30;
    public int Damage { get; set; } = 10;
    public float MoveSpeed { get; set; } = 2.4f;
    public float DetectionRange { get; set; } = 6;
    public float AttackRange { get; set; } = 1;
    public float AttackCooldown { get; set; } = 1;
    public MonsterData() { Height = 1.6f; }
}

sealed partial class MapData
{
    public List<MonsterData> Monsters { get; set; } = [];
    void ValidateMonsters()
    {
        if (Monsters == null || Monsters.Count > 1000 || Monsters.Any(m => m == null ||
            string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 80 ||
            !Contains(m.X, m.Z) || !ValidHeight(m.Height) || (m.Asset != "" && !ProjectPaths.ValidAssetId(m.Asset)) ||
            m.MaxHealth is < 1 or > 10000 || m.Damage is < 1 or > 1000 ||
            !InRange(m.MoveSpeed, .25f, 10) || !InRange(m.DetectionRange, 1, 64) ||
            !InRange(m.AttackRange, .5f, 5) || !InRange(m.AttackCooldown, .2f, 10)))
            throw new InvalidDataException("Invalid monsters or combat settings.");
    }
    static bool InRange(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
}

sealed partial class MapDocument
{
    public MonsterData PlaceMonster(string? asset, Vector3 position)
    {
        Checkpoint(); var p = Map.Clamp(position);
        var monster = new MonsterData { Asset = asset ?? "", X = p.X, Z = p.Z };
        Map.Monsters.Add(monster); return monster;
    }
    public MonsterData DuplicateMonster(MonsterData source)
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<MonsterData>(System.Text.Json.JsonSerializer.Serialize(source))!;
        Checkpoint(); copy.Id = Guid.NewGuid().ToString("N");
        var p = Map.Clamp(source.Position + new Vector3(1, 0, 1)); copy.X = p.X; copy.Z = p.Z;
        Map.Monsters.Add(copy); return copy;
    }
}
