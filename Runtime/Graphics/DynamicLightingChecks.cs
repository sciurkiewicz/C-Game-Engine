using System.Numerics;

static class DynamicLightingChecks
{
    public static void Run()
    {
        var map = MapData.CreateEmpty(16, 16);
        map.Objects.Add(new() { Id = "fire-a", Asset = "objects/props/camp/ognisko.png", X = -1 });
        map.Objects.Add(new() { Id = "fire-b", Asset = "objects/torch.png", X = 1 });
        map.Lights.Add(new() { Id = "steady", Intensity = 1.3f });
        string authored = map.Serialize();
        var settings = new GraphicsSettings();
        var frame = new WorldVisualFrame(settings, 1, new FollowCamera(Vector3.Zero).View, 12, true);
        float Intensity(WorldVisualFrame? mode, string id = "fire-a") =>
            WorldLighting.Nearby(map, Vector3.Zero, mode).Single(light => light.Id == id).Intensity;
        float first = Intensity(frame), second = Intensity(frame with { Time = 1.2f });
        Check(Math.Abs(first - second) > .001f, "fire illumination changes with animation time");
        Check(first == Intensity(frame), "the same frame produces the same illumination for every render pass");
        Check(Math.Abs(first - Intensity(frame, "fire-b")) > .001f, "different fires have independent stable phases");
        Check(Intensity(frame, "steady") == 1.3f, "ordinary point lights retain their authored brightness");
        Check(Intensity(frame with { Outdoors = false, Hour = 0 }) == first, "fire flicker works indoors and at night");
        Check(Intensity(null) == .85f, "the editor retains steady light without a visual-effects frame");
        foreach (var disabled in new[] { settings with { Enabled = false }, settings with { DynamicLights = false },
            settings with { LightFlickerStrength = 0 }, settings with { LightFlickerSpeed = 0 } })
            Check(Intensity(frame with { Settings = disabled }) == .85f, "disabled animation restores the base intensity");
        for (int sample = 0; sample < 200; sample++)
        {
            float intensity = Intensity(frame with { Time = sample * .137f });
            Check(float.IsFinite(intensity) && intensity >= .85f * (1 - settings.LightFlickerStrength) - .00001f &&
                intensity <= .85f * (1 + settings.LightFlickerStrength) + .00001f, "flicker stays within its configured brightness range");
        }
        Check(map.Serialize() == authored, "animated illumination never rewrites authored map objects or lights");
        map.Environment.Lighting = false;
        Check(WorldLighting.Nearby(map, Vector3.Zero, frame).Length == 0, "the map lighting switch is respected");
        map.Type = MapType.Cave;
        Check(WorldLighting.Nearby(map, Vector3.Zero, frame).Length == 3, "interior lights remain available with permanently dark ambient light");
        Console.WriteLine("Dynamic lighting checks passed: animated fire, stable independent phases, steady authored points, controls, interior/night behavior and map isolation.");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Dynamic lighting check failed: " + message); }
}
