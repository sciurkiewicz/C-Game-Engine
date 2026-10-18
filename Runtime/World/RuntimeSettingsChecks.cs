using System.Text.Json.Nodes;

static class RuntimeSettingsChecks
{
    public static void Run()
    {
        var legacyJson = JsonNode.Parse(new MapData().Serialize())!.AsObject();
        legacyJson.Remove("Runtime");
        var legacy = MapData.Parse(legacyJson.ToJsonString());
        Check(legacy.Runtime == new RuntimeSettings(), "older maps keep the original projection, zoom, grid and FPS");
        var doc = new MapDocument(legacy, true);
        var settings = new RuntimeSettings { Projection = ViewProjection.Isometric, InitialDistance = 22, ShowGrid = false, TargetFps = 120 };
        doc.Checkpoint(); doc.Map.Runtime = settings with { };
        Check(doc.Dirty, "launch changes mark the map as unsaved");
        Check(MapData.Parse(doc.Map.Serialize()).Runtime == settings, "JSON preserves launch settings");
        doc.Undo(); Check(!doc.Dirty && doc.Map.Runtime == new RuntimeSettings(), "undo restores defaults and saved state");
        doc.Redo(); Check(doc.Map.Runtime == settings, "redo restores all launch options together");
        var play = new PlaySession(doc.Map);
        Check(play.Map.Runtime == settings && play.Camera.Projection == ViewProjection.Isometric && play.Camera.Distance == 22,
            "Play and Runtime start with the saved projection and zoom");
        doc.Map.Runtime.InitialDistance = 40;
        Check(play.Map.Runtime.InitialDistance == 22, "play uses an independent settings copy");
        play.Camera.Zoom(100); play.Camera.Snap(play.Player); play.Camera.Reset();
        Check(play.Camera.TargetDistance == 22, "reset returns to the configured starting zoom");
        play.Restart(); Check(play.Map.Runtime == settings && play.Camera.Projection == ViewProjection.Isometric, "restart keeps launch settings");
        foreach (var invalid in new[]
        {
            settings with { Projection = (ViewProjection)99 }, settings with { InitialDistance = float.NaN },
            settings with { InitialDistance = 14 }, settings with { InitialDistance = 51 }, settings with { TargetFps = -1 },
            settings with { TargetFps = 144 }
        })
        {
            try { invalid.Validate(); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Invalid runtime settings accepted.");
        }
        legacyJson["Runtime"] = null;
        try { MapData.Parse(legacyJson.ToJsonString()); }
        catch (InvalidDataException)
        {
            Console.WriteLine("Runtime settings checks passed: old maps, validation, save/load, undo/redo, play isolation and zoom reset.");
            return;
        }
        throw new InvalidOperationException("Null runtime settings accepted.");
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Runtime settings check failed: " + message);
    }
}
