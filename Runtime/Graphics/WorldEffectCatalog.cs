using Raylib_cs;
using static Raylib_cs.Raylib;

enum WorldEffectKind { Smoke, Steam, HeatHaze, Embers, Fireflies }

sealed record WorldEffectDefinition(string Id, string Name, WorldEffectKind Kind, float DefaultHeight);

// Built-in emitters use ordinary object asset IDs, so selection, map saves,
// prefabs and map fragments retain the existing object workflow.
static class WorldEffectCatalog
{
    public const string SmokeId = "objects/effects/smoke.png";
    public const string SteamId = "objects/effects/steam.png";
    public const string HeatHazeId = "objects/effects/heat_haze.png";
    public const string EmbersId = "objects/effects/embers.png";
    public const string FirefliesId = "objects/effects/fireflies.png";
    public static IReadOnlyList<WorldEffectDefinition> All { get; } =
    [
        new(SmokeId, "Dym", WorldEffectKind.Smoke, 3),
        new(SteamId, "Para", WorldEffectKind.Steam, 2.5f),
        new(HeatHazeId, "Gorące powietrze", WorldEffectKind.HeatHaze, 3),
        new(EmbersId, "Iskry", WorldEffectKind.Embers, 2),
        new(FirefliesId, "Świetliki", WorldEffectKind.Fireflies, 2.5f)
    ];
    static readonly Dictionary<string, WorldEffectDefinition> Definitions = All.ToDictionary(effect => effect.Id);
    public static bool TryGet(string id, out WorldEffectDefinition effect) => Definitions.TryGetValue(id, out effect!);
    public static bool IsEffect(string id) => Definitions.ContainsKey(id);
    public static void Register(WorldAssets assets)
    {
        foreach (var effect in All)
        {
            if (assets.Sprites.ContainsKey(effect.Id)) continue;
            var image = Thumbnail(effect.Kind);
            Texture2D texture;
            try { texture = LoadTextureFromImage(image); }
            finally { UnloadImage(image); }
            if (!IsTextureValid(texture)) throw new InvalidDataException("Cannot create effect thumbnail: " + effect.Name);
            SetTextureFilter(texture, TextureFilter.Bilinear);
            assets.Sprites.Add(effect.Id, new(effect.Id, texture, 128, 128));
        }
    }
    static Image Thumbnail(WorldEffectKind kind)
    {
        const int size = 96;
        var image = GenImageColor(size, size, Color.Blank);
        float Disc(float x, float y, float cx, float cy, float rx, float ry)
        {
            float distance = (x-cx)*(x-cx)/(rx*rx) + (y-cy)*(y-cy)/(ry*ry);
            return MathF.Pow(Math.Max(0, 1-distance), .8f);
        }
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float opacity = 0; var color = new Color(155, 163, 171, 255);
            switch (kind)
            {
                case WorldEffectKind.Smoke:
                case WorldEffectKind.Steam:
                    opacity = Math.Max(Disc(x,y,48,74,14,17), Math.Max(Disc(x,y,40,54,23,23),
                        Math.Max(Disc(x,y,58,36,26,23),Disc(x,y,43,22,22,18)))) * .85f;
                    if (kind == WorldEffectKind.Steam) color = new(224,241,249,255);
                    break;
                case WorldEffectKind.HeatHaze:
                    for (int line = 0; line < 3; line++)
                    {
                        float center = 25 + line*23 + MathF.Sin(y*.14f+line)*5;
                        float band = Math.Max(0,1-Math.Abs(x-center)/4);
                        opacity = Math.Max(opacity,band*MathF.Sin(Math.Clamp(y/96f,0,1)*MathF.PI));
                    }
                    color = new(255,190,116,255);
                    break;
                case WorldEffectKind.Embers:
                case WorldEffectKind.Fireflies:
                    for (int particle = 0; particle < 9; particle++)
                    {
                        float cx = 18 + (particle*29 % 63), cy = 14 + (particle*37 % 68);
                        opacity = Math.Max(opacity,Disc(x,y,cx,cy,6,kind == WorldEffectKind.Embers ? 10 : 6));
                    }
                    color = kind == WorldEffectKind.Embers ? new(255,175,53,255) : new(189,250,99,255);
                    break;
            }
            if (opacity <= .001f) continue;
            color.A = (byte)Math.Clamp(opacity*255,0,255);
            ImageDrawPixel(ref image,x,y,color);
        }
        return image;
    }
}
