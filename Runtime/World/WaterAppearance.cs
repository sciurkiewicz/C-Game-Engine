using System.Numerics;
using System.Text.Json.Serialization;

sealed record WaterAppearance
{
    public int R { get; init; } = 65;
    public int G { get; init; } = 150;
    public int B { get; init; } = 151;
    public int DeepR { get; init; } = 12;
    public int DeepG { get; init; } = 62;
    public int DeepB { get; init; } = 80;
    // Optical depth scale in metres; larger values retain more of the bed's detail.
    public float Clarity { get; init; } = 4.5f;
    public float ReflectionStrength { get; init; } = .65f;
    public float RefractionStrength { get; init; } = .15f;
    public float FoamStrength { get; init; } = .12f;
    [JsonIgnore] public Vector3 ShallowColor => new(R / 255f, G / 255f, B / 255f);
    [JsonIgnore] public Vector3 DeepColor => new(DeepR / 255f, DeepG / 255f, DeepB / 255f);
    public void Validate()
    {
        if (R is < 0 or > 255 || G is < 0 or > 255 || B is < 0 or > 255 ||
            DeepR is < 0 or > 255 || DeepG is < 0 or > 255 || DeepB is < 0 or > 255 ||
            !MapData.FiniteRange(Clarity, .1f, 64) || !MapData.FiniteRange(ReflectionStrength, 0, 1) ||
            !MapData.FiniteRange(RefractionStrength, 0, 1) || !MapData.FiniteRange(FoamStrength, 0, 1))
            throw new InvalidDataException("Invalid water color, clarity, reflection, refraction or foam settings.");
    }
}
