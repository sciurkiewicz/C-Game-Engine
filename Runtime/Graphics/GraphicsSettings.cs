using System.Text.Json;
using System.Text.Json.Serialization;

sealed record GraphicsSettings
{
    public bool Enabled { get; init; } = true;
    public bool Fog { get; init; } = true;
    public float FogStrength { get; init; } = .4f;
    public float FogStart { get; init; } = 12;
    public float FogEnd { get; init; } = 60;
    public float GroundFog { get; init; } = .08f;
    public bool Wind { get; init; } = true;
    public float WindStrength { get; init; } = .13f;
    public float WindSpeed { get; init; } = 1.2f;
    public bool Bloom { get; init; } = true;
    public float BloomStrength { get; init; } = .28f;
    public float BloomThreshold { get; init; } = .72f;
    public bool SunRays { get; init; } = true;
    public float SunRaysStrength { get; init; } = .14f;
    public bool Outlines { get; init; } = true;
    public float OutlineStrength { get; init; } = .65f;
    public float OutlineWidth { get; init; } = 1.2f;
    public bool HeatDistortion { get; init; } = true;
    public float HeatStrength { get; init; } = .004f;
    public bool Rain { get; init; } = true;
    public float RainIntensity { get; init; } = .3f;
    public float Wetness { get; init; } = .3f;
    public bool Damage { get; init; } = true;
    public float DamageStrength { get; init; } = .6f;
    public float DamageDuration { get; init; } = .4f;
    public float CameraShake { get; init; } = .09f;
    public static string FilePath => Path.Combine(ProjectPaths.Root, "Runtime", "Graphics", "effects.json");
    static readonly JsonSerializerOptions Options = new()
    { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    public static GraphicsSettings Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var settings = JsonSerializer.Deserialize<GraphicsSettings>(stream, Options) ?? throw new InvalidDataException("Empty graphics settings.");
            settings.Validate(); return settings;
        }
        catch (JsonException ex) { throw new InvalidDataException("Invalid graphics settings in " + path + ": " + ex.Message, ex); }
    }
    public void Validate()
    {
        if (!MapData.FiniteRange(FogStrength, 0, 2) || !MapData.FiniteRange(FogStart, 0, 256) ||
            !MapData.FiniteRange(FogEnd, FogStart + 1, 512) || !MapData.FiniteRange(GroundFog, 0, 1) ||
            !MapData.FiniteRange(WindStrength, 0, 1) || !MapData.FiniteRange(WindSpeed, 0, 10) ||
            !MapData.FiniteRange(BloomStrength, 0, 2) || !MapData.FiniteRange(BloomThreshold, .1f, 1) ||
            !MapData.FiniteRange(SunRaysStrength, 0, 1) || !MapData.FiniteRange(OutlineStrength, 0, 1) ||
            !MapData.FiniteRange(OutlineWidth, .25f, 4) || !MapData.FiniteRange(HeatStrength, 0, .03f) ||
            !MapData.FiniteRange(RainIntensity, 0, 1) || !MapData.FiniteRange(Wetness, 0, 1) ||
            !MapData.FiniteRange(DamageStrength, 0, 1) || !MapData.FiniteRange(DamageDuration, .1f, 3) || !MapData.FiniteRange(CameraShake, 0, .5f))
            throw new InvalidDataException("Invalid fog, wind, bloom, rays, outline, heat, rain or damage intensity in graphics settings.");
    }
}
