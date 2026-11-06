using System.Text.Json;
using System.Text.Json.Serialization;

// A session keeps an immutable profile. Runtime and editor Play load the same editable JSON.
sealed record PlayerDefinition
{
    public string Name { get; init; } = "Bohater";
    public string Description { get; init; } = "";
    public int MaxHealth { get; init; } = 100;
    public int Damage { get; init; } = 10;
    public float WalkSpeed { get; init; } = 3.6f;
    public float RunSpeed { get; init; } = 6;
    public float AttackRange { get; init; } = 1.8f;
    public float AttackCooldown { get; init; } = .4f;
    public float InteractionRange { get; init; } = 2.5f;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static PlayerDefinition Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var definition = JsonSerializer.Deserialize<PlayerDefinition>(stream, JsonOptions)
                ?? throw new InvalidDataException("Empty player definition.");
            definition.Validate(); return definition;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        { throw new InvalidDataException($"Invalid player definition in {path}: {ex.Message}", ex); }
    }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Description == null || Description.Length > 2000)
            throw new InvalidDataException("Name must have 1–80 characters and Description at most 2000 characters.");
        if (MaxHealth is < 1 or > 10000 || Damage is < 1 or > 1000)
            throw new InvalidDataException("MaxHealth must be 1–10000 and Damage 1–1000.");
        if (!MapData.FiniteRange(WalkSpeed, .1f, 100) || !MapData.FiniteRange(RunSpeed, WalkSpeed, 100))
            throw new InvalidDataException("WalkSpeed must be 0.1–100 m/s and RunSpeed at least WalkSpeed, up to 100 m/s.");
        if (!MapData.FiniteRange(AttackRange, .1f, 16) || !MapData.FiniteRange(AttackCooldown, .05f, 10) ||
            !MapData.FiniteRange(InteractionRange, .1f, 32))
            throw new InvalidDataException("AttackRange must be 0.1–16 m, AttackCooldown 0.05–10 s and InteractionRange 0.1–32 m.");
    }
}
