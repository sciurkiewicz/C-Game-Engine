using System.Text.Json.Nodes;

static class WaterAppearanceChecks
{
    public static void Run()
    {
        var defaults = new WaterAppearance(); defaults.Validate();
        var map = MapData.CreateEmpty(12, 8);
        var legacy = JsonNode.Parse(map.Serialize())!.AsObject(); legacy.Remove("WaterAppearance");
        Check(MapData.Parse(legacy.ToJsonString()).WaterAppearance == defaults, "older maps receive the default water material");
        var material = defaults with { R = 23, G = 188, B = 159, DeepR = 8, DeepG = 41, DeepB = 69,
            Clarity = 5.5f, ReflectionStrength = .8f, RefractionStrength = .2f, FoamStrength = .4f };
        var document = new MapDocument(map, true);
        document.Checkpoint(); document.Map.WaterAppearance = material;
        Check(document.Dirty, "changing water appearance marks the map as unsaved");
        Check(MapData.Parse(document.Map.Serialize()).WaterAppearance == material, "JSON preserves all water material parameters");
        Check(MapSnapshot.Capture(document.Map).Restore().WaterAppearance == material, "undo snapshots preserve the water material");
        document.Undo(); Check(!document.Dirty && document.Map.WaterAppearance == defaults, "undo restores the material and saved state");
        document.Redo(); Check(document.Map.WaterAppearance == material, "redo restores the complete material");
        var play = new PlaySession(document.Map);
        Check(play.Map.WaterAppearance == material, "Play starts with authored water appearance");
        document.Map.WaterAppearance = defaults;
        Check(play.Map.WaterAppearance == material, "Play owns an independent water appearance setting");
        foreach (var invalid in new[] { defaults with { R = -1 }, defaults with { G = 256 }, defaults with { DeepB = 300 },
            defaults with { Clarity = 0 }, defaults with { Clarity = float.NaN }, defaults with { ReflectionStrength = 2 },
            defaults with { RefractionStrength = float.PositiveInfinity }, defaults with { FoamStrength = -1 } })
            ExpectInvalid(invalid.Validate);
        legacy["WaterAppearance"] = null; ExpectInvalid(() => MapData.Parse(legacy.ToJsonString()));
        Console.WriteLine("Water appearance checks passed: legacy defaults, color/optics validation, JSON/snapshots, undo/redo and Play isolation.");
    }
    static void ExpectInvalid(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new InvalidOperationException("Invalid water appearance accepted."); }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Water appearance check failed: " + message); }
}
