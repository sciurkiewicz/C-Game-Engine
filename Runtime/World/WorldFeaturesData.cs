using System.Numerics;
using System.Text.Json.Serialization;

sealed class WorldEnvironment
{
    public bool Lighting { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public bool FadeOccluders { get; set; } = true;
    public float OccluderOpacity { get; set; } = .25f;
    public bool DayNightCycle { get; set; }
    public float TimeOfDay { get; set; } = 12;
    public float DayLengthSeconds { get; set; } = 300;
    public bool Audio { get; set; } = true;
    public float MasterVolume { get; set; } = .65f;
    public void Validate()
    {
        if (!MapData.FiniteRange(OccluderOpacity, .05f, .8f) || !MapData.FiniteRange(TimeOfDay, 0, 24) ||
            !MapData.FiniteRange(DayLengthSeconds, 10, 86400) || !MapData.FiniteRange(MasterVolume, 0, 1))
            throw new InvalidDataException("Invalid environment settings.");
    }
}

sealed class PointLightData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Light";
    public float X { get; set; }
    public float Z { get; set; }
    public float Height { get; set; } = 2;
    public float Radius { get; set; } = 6;
    public float Intensity { get; set; } = 1;
    public byte R { get; set; } = 255;
    public byte G { get; set; } = 190;
    public byte B { get; set; } = 100;
    public bool Enabled { get; set; } = true;
}

sealed class SoundEmitterData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Ambient sound";
    public string Clip { get; set; } = "fire";
    public float X { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 12;
    public float Volume { get; set; } = .7f;
    public bool Enabled { get; set; } = true;
}

[JsonConverter(typeof(JsonStringEnumConverter<TriggerAction>))]
enum TriggerAction { Message, Dialogue, ToggleDoor, SpawnMonster, Teleport, ChangeMap }

sealed class TriggerData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Trigger";
    public float X { get; set; }
    public float Z { get; set; }
    public float Width { get; set; } = 2;
    public float Depth { get; set; } = 2;
    public bool Enabled { get; set; } = true;
    public bool Once { get; set; } = true;
    public TriggerAction Action { get; set; }
    public string Text { get; set; } = "You entered a trigger area.";
    public string TargetId { get; set; } = "";
    public string MapFile { get; set; } = "world.json";
    public float DestinationX { get; set; }
    public float DestinationZ { get; set; }
    public MonsterData MonsterTemplate { get; set; } = new();
    public bool Contains(Vector3 point) => Math.Abs(point.X - X) <= Width / 2 && Math.Abs(point.Z - Z) <= Depth / 2;
}

sealed partial class MapData
{
    public WorldEnvironment Environment { get; set; } = new();
    public List<PointLightData> Lights { get; set; } = [];
    public List<SoundEmitterData> Sounds { get; set; } = [];
    public List<TriggerData> Triggers { get; set; } = [];
    public static bool FiniteRange(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
    void ValidateWorldFeatures()
    {
        if (Environment == null) throw new InvalidDataException("Missing environment settings.");
        Environment.Validate();
        foreach (var entity in SceneEntities) entity.Collider?.Validate();
        if (Lights == null || Lights.Count > 256 || Lights.Any(l => l == null || string.IsNullOrWhiteSpace(l.Id) ||
            string.IsNullOrWhiteSpace(l.Name) || l.Name.Length > 80 || !Contains(l.X, l.Z) ||
            !FiniteRange(l.Height, 0, 64) || !FiniteRange(l.Radius, .25f, 128) || !FiniteRange(l.Intensity, 0, 8)) ||
            Lights.Select(l => l.Id).Distinct().Count() != Lights.Count)
            throw new InvalidDataException("Invalid point lights.");
        if (Sounds == null || Sounds.Count > 256 || Sounds.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id) ||
            string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 80 || !Contains(s.X, s.Z) || !ProjectPaths.ValidAssetId(s.Clip) ||
            !FiniteRange(s.Radius, .5f, 128) || !FiniteRange(s.Volume, 0, 1)) || Sounds.Select(s => s.Id).Distinct().Count() != Sounds.Count)
            throw new InvalidDataException("Invalid sound emitters.");
        if (Triggers == null || Triggers.Count > 1000 || Triggers.Any(t => t == null || string.IsNullOrWhiteSpace(t.Id) ||
            string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 80 || !Contains(t.X, t.Z) || !FiniteRange(t.Width, .5f, 256) ||
            !FiniteRange(t.Depth, .5f, 256) || !Enum.IsDefined(t.Action) || t.Text == null || t.Text.Length > 2000 ||
            t.TargetId == null || t.TargetId.Length > 128 || !ProjectPaths.ValidAssetId(t.MapFile) || !t.MapFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            !float.IsFinite(t.DestinationX) || !float.IsFinite(t.DestinationZ) ||
            (t.Action is TriggerAction.Teleport or TriggerAction.SpawnMonster && !Contains(t.DestinationX, t.DestinationZ)) || t.MonsterTemplate == null) ||
            Triggers.Select(t => t.Id).Distinct().Count() != Triggers.Count)
            throw new InvalidDataException("Invalid trigger areas.");
        foreach (var trigger in Triggers)
        {
            var m = trigger.MonsterTemplate;
            if (m.Asset != "" && !ProjectPaths.ValidAssetId(m.Asset) || !ValidHeight(m.Height) || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 80 ||
                m.MaxHealth is < 1 or > 10000 || m.Damage is < 1 or > 1000 || !FiniteRange(m.MoveSpeed, .25f, 10) ||
                !FiniteRange(m.DetectionRange, 1, 64) || !FiniteRange(m.AttackRange, .5f, 5) || !FiniteRange(m.AttackCooldown, .2f, 10))
                throw new InvalidDataException("Invalid trigger monster template.");
            m.Collider?.Validate();
        }
    }
}
