using System.Numerics;
using System.Text.Json.Nodes;

static class MapTypeChecks
{
    public static void Run()
    {
        var legacy = JsonNode.Parse(MapData.CreateEmpty(8, 8).Serialize())!.AsObject();
        legacy.Remove("Type");
        var outside = MapData.Parse(legacy.ToJsonString());
        Check(outside.Type == MapType.OpenWorld && !outside.AlwaysDark && WorldLighting.Ambient(outside, 12) == Vector3.One,
            "older maps remain outdoor maps with daytime lighting");
        outside.Environment.Lighting = false;
        Check(WorldLighting.Ambient(outside, 0) == Vector3.One, "outdoor lighting can still be disabled");

        string file = Path.Combine(Path.GetTempPath(), "wildwood-map-type-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            foreach (var type in Enum.GetValues<MapType>())
            {
                var map = MapData.CreateEmpty(20, 12, type);
                map.Validate(); map.Save(file);
                var loaded = MapData.Load(file);
                Check(loaded.Type == type && loaded.Width == 20 && loaded.Depth == 12 && loaded.Serialize() == map.Serialize(),
                    "typed rectangular maps survive save/load: " + type);
                Check(MapSnapshot.Capture(map).Restore().Type == type && JsonNode.Parse(map.Serialize())!["Type"]!.GetValue<string>() == type.ToString(),
                    "type is saved as text and survives snapshots: " + type);
                Check(map.Objects.Count == 0 && map.Npcs.Count == 0 && map.Player.Position == Vector3.Zero,
                    "new maps start empty with a valid central spawn");
                if (type == MapType.OpenWorld) continue;
                Check(map.AlwaysDark && map.Environment.TimeOfDay == 0 && !map.Environment.DayNightCycle,
                    "interiors start at a fixed night hour: " + type);
                map.Environment.TimeOfDay = 12; map.Environment.DayNightCycle = true; map.Environment.Lighting = false;
                foreach (float hour in new[] { 0, 6, 12, 18, 24 })
                    Check(WorldLighting.Ambient(map, hour) == WorldLighting.Ambient(0), "interiors stay dark despite daylight overrides: " + type);
                map.Lights.Add(new() { X = 0, Z = 0, Height = 1, Radius = 6, Intensity = 2 });
                var ambient = WorldLighting.Ambient(map, 12);
                Check(WorldLighting.Evaluate(map, Vector3.Zero, ambient, WorldLighting.Nearby(map, Vector3.Zero)).X > ambient.X + .5f,
                    "point lights still illuminate dark interiors: " + type);
                var play = new PlaySession(map);
                play.AdvanceWorld(3600);
                Check(play.Map.Type == type && play.TimeOfDay == 0, "playtest and runtime cannot advance an interior into day: " + type);
                play.SaveProgress(file);
                var progress = JsonNode.Parse(File.ReadAllText(file))!;
                progress["Worlds"]![play.CurrentMapPath]!["TimeOfDay"] = 12;
                File.WriteAllText(file, progress.ToJsonString());
                play.LoadProgress(file);
                Check(play.Map.Type == type && play.TimeOfDay == 0, "loading progress preserves permanent darkness: " + type);
            }
            outside.Environment.Lighting = true; outside.Environment.DayNightCycle = true;
            outside.Environment.TimeOfDay = 6; outside.Environment.DayLengthSeconds = 24;
            var outdoorPlay = new PlaySession(outside); outdoorPlay.AdvanceWorld(1);
            Check(Math.Abs(outdoorPlay.TimeOfDay - 7) < .001f, "outdoor day/night cycle still advances");

            var document = new MapDocument(outside, true);
            document.New(16, 24, MapType.Building);
            Check(document.Map.Type == MapType.Building && document.Map.AlwaysDark && document.Map.Tiles.All(t => t == "terrain/stone_tiles_ink.png"),
                "building creation sets its floor and lighting in one operation");
            document.Undo(); Check(document.Map.Type == MapType.OpenWorld && !document.Dirty, "undo restores the outdoor map");
            document.Redo(); Check(document.Map.Type == MapType.Building && document.Map.Width == 16, "redo restores the typed map");
            string before = document.Map.Serialize();
            try { document.New(8, 8, (MapType)999); throw new Exception("Invalid map type accepted"); }
            catch (ArgumentOutOfRangeException) { }
            Check(document.Map.Serialize() == before, "invalid creation preserves current work");
            var invalid = JsonNode.Parse(before)!; invalid["Type"] = 999;
            try { MapData.Parse(invalid.ToJsonString()); throw new Exception("Invalid saved map type accepted"); }
            catch (InvalidDataException) { }
        }
        finally { if (File.Exists(file)) File.Delete(file); if (File.Exists(file + ".tmp")) File.Delete(file + ".tmp"); }
        Console.WriteLine("Map type checks passed: legacy maps, types, defaults, save/load, undo/redo, permanent darkness, lights and progress.");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Map type check failed: " + message); }
}
