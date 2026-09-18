using System.Numerics;

static class EditorChecks
{
    public static void Run()
    {
        const string asset = "objects/vegetation/trees/tree1.png";
        var doc = new MapDocument(new MapData(), true);
        Check(!doc.Dirty, "initial clean state");
        var item = doc.Place(asset, new(2, 0, 3), 5, true);
        string itemId = item.Id;
        Check(doc.Dirty && doc.Map.Objects.Count == 1, "place object");
        Check(!WorldRenderer.CanWalk(doc.Map, new(2, 0, 3)), "solid object blocks movement");
        Check(WorldRenderer.CanWalk(doc.Map, Vector3.Zero), "open ground allows movement");
        doc.Checkpoint();
        item.X = 4; item.Height = 3; item.Solid = false;
        doc.Undo();
        Check(doc.Map.Objects.Single().X == 2 && doc.Map.Objects.Single().Solid, "undo inspector changes");
        doc.Redo();
        Check(doc.Map.Objects.Single().X == 4 && doc.Map.Objects.Single().Height == 3, "redo inspector changes");
        doc.Checkpoint();
        doc.Paint(new(-24, 0, -24), 1, "sand");
        Check(doc.Map.Tiles.Count(t => t == "sand") == 4, "brush clips to map bounds");
        doc.Paint(new(0, 0, 0), 1, "stone");
        Check(doc.Map.Tiles.Count(t => t == "stone") == 9, "three by three brush");
        doc.Undo();
        Check(doc.Map.Tiles.All(t => t == "grass"), "one undo restores complete stroke");
        doc.Redo();
        doc.Checkpoint(); doc.Map.Player.X = -3; doc.Map.Player.Z = 5;
        string file = Path.Combine(Path.GetTempPath(), "wildwood-editor-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            doc.Save(file);
            Check(!doc.Dirty, "save clears dirty state");
            var loaded = MapData.Load(file);
            Check(loaded.Serialize() == doc.Map.Serialize(), "round trip preserves all map fields");
            Check(loaded.Objects.Single().Id == itemId && loaded.Player.X == -3 && loaded.Player.Z == 5, "stable IDs and player spawn");
            doc.Checkpoint(); doc.Map.Objects.Clear();
            doc.Load(file);
            Check(doc.Map.Objects.Count == 1 && !doc.CanUndo && !doc.Dirty, "load replaces map and history");
            string before = doc.Map.Serialize();
            File.WriteAllText(file, "{ broken json");
            try { doc.Load(file); throw new Exception("Invalid JSON accepted"); }
            catch (System.Text.Json.JsonException) { }
            Check(doc.Map.Serialize() == before, "failed load preserves current work");
            var invalid = new MapData { Tiles = [] };
            try { MapData.Parse(invalid.Serialize()); throw new Exception("Invalid dimensions accepted"); }
            catch (InvalidDataException) { }
            Check(!ProjectPaths.ValidAssetId("../outside.png") && !ProjectPaths.ValidAssetId("C:/outside.png"), "asset paths stay relative");
            doc.Checkpoint(); doc.Map.Objects.Clear(); doc.Undo();
            doc.Place(asset, Vector3.Zero, 2, false);
            Check(!doc.CanRedo, "editing after undo clears redo history");
        }
        finally { if (File.Exists(file)) File.Delete(file); if (File.Exists(file + ".tmp")) File.Delete(file + ".tmp"); }
        Console.WriteLine("Editor checks passed: placement, collision, painting, spawn, undo/redo, save/load and invalid-map recovery.");
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Editor check failed: " + message);
    }
}
