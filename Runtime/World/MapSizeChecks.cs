using System.Diagnostics;
using System.Numerics;
using Raylib_cs;

static class MapSizeChecks
{
    public static void Run()
    {
        var timer = Stopwatch.StartNew();
        Check(MapData.MaxSize == 2048, "maximum size is 2048");
        var map = MapData.CreateEmpty(2048, 2048);
        map.Validate();
        Check(map.Tiles.Length == 4_194_304, "full-sized terrain allocation");
        var snapshot = MapSnapshot.Capture(map);
        Check(snapshot.SizeBytes < 4096, "uniform large map has a compact history snapshot");
        var doc = new MapDocument(map, true);
        Check(!doc.Dirty, "large map starts clean");
        doc.Checkpoint(); doc.Paint(new(1023.9f, 0, 1023.9f), 1, "sand");
        Check(doc.Dirty && doc.Map.Tiles[^1] == "sand" && doc.Map.Tiles.Count(t => t == "sand") == 4, "painting at the far corner and dirty state");
        doc.Paint(new(1023.9f, 0, 1023.9f), 1, "grass");
        Check(!doc.Dirty, "restoring the saved terrain clears dirty state");
        doc.Paint(new(1023.9f, 0, 1023.9f), 1, "sand");
        doc.Undo();
        Check(!doc.Dirty && doc.Map.Tiles[^1] == "grass", "undo restores a full-sized map");
        doc.Redo();
        Check(doc.Dirty && doc.Map.Tiles[^1] == "sand", "redo restores large-map terrain changes");
        var hill = new Vector3(1022, 0, -1022);
        doc.Checkpoint(); doc.Sculpt(hill, 3, 2, SculptMode.Raise);
        Check(doc.Map.Heights.Length == 4_198_401 && doc.Map.HeightAt(hill.X, hill.Z) > 1.99f,
            "sculpting allocates every vertex on the maximum-sized map");
        doc.Undo(); Check(doc.Map.HeightAt(hill.X, hill.Z) == 0 && doc.Map.Tiles[^1] == "sand", "large-map undo restores terrain heights independently of materials");
        doc.Redo(); Check(doc.Map.HeightAt(hill.X, hill.Z) > 1.99f, "large-map redo restores terrain heights");
        doc.Checkpoint(); doc.Sculpt(new(-1018, 0, 1018), 3, 2, SculptMode.Lower);
        Check(doc.FillWater(new(-1018, 0, 1018)) == WaterEditResult.Filled && doc.Map.WaterDepthAt(-1018, 1018) > 1.99f &&
            MapSnapshot.Capture(doc.Map).SizeBytes < 8192, "large-map basin filling keeps sparse terrain history compact");
        string file = Path.Combine(Path.GetTempPath(), "wildwood-large-map-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            doc.Map.Player.X = 1022; doc.Map.Player.Z = -1022;
            var npc = doc.PlaceNpc(null, new(1021, 0, -1022)); npc.Name = "Strażnik";
            doc.Save(file);
            Check(!doc.Dirty, "saving the large map clears dirty state");
            var loaded = MapData.Load(file);
            Check(loaded.Width == 2048 && loaded.Depth == 2048 && loaded.Tiles.Length == 4_194_304 &&
                loaded.Tiles[^1] == "sand" && loaded.Tiles[0] == "grass" && loaded.Player.X == 1022 && loaded.Npcs.Single().Id == npc.Id &&
                loaded.HeightAt(hill.X, hill.Z) > 1.99f && loaded.WaterDepthAt(-1018, 1018) > 1.99f,
                "large map save/load preserves sculpted terrain, water, player and NPC");
            var play = new PlaySession(loaded);
            Check(play.Player == loaded.GroundPosition(loaded.Player.X, loaded.Player.Z) && play.NearbyNpc?.Id == npc.Id,
                "runtime starts on elevated terrain near the edge of the large map");
            loaded.Tiles[0] = "water";
            Check(play.Map.Tiles[0] == "grass", "large-map playtest has an independent terrain copy");
            loaded.SetVertexHeight(2046, 2, 10);
            Check(play.Player.Y == play.Map.HeightAt(hill.X, hill.Z), "large-map playtest has independent height data");
        }
        finally { if (File.Exists(file)) File.Delete(file); if (File.Exists(file + ".tmp")) File.Delete(file + ".tmp"); }
        map = doc.Map;
        foreach (var projection in Enum.GetValues<ViewProjection>())
        foreach (float zoom in new[] { -100f, 0f, 100f })
        foreach (var target in new[] { Vector3.Zero, new Vector3(1023, 0, 1023), new Vector3(-1023, 0, -1023) })
        foreach (var size in new[] { (608, 600), (1280, 800), (2560, 1080) })
        {
            var camera = new FollowCamera(target, new() { Projection = projection }); camera.Zoom(zoom); camera.Snap(target);
            var bounds = TerrainVisibility.GetBounds(map, camera.View, size.Item1, size.Item2);
            Check(bounds.MinX >= 0 && bounds.MinZ >= 0 && bounds.MaxX <= map.Width && bounds.MaxZ <= map.Depth &&
                bounds.TileCount > 0 && bounds.TileCount < 50_000, "terrain rendering is bounded by the viewport");
            var forward = Vector3.Normalize(camera.View.Target - camera.View.Position);
            var right = Vector3.Normalize(Vector3.Cross(forward, camera.View.Up));
            var up = Vector3.Cross(right, forward);
            float tangent = MathF.Tan(camera.View.FovY * MathF.PI / 360), aspect = size.Item1 / (float)size.Item2;
            for (int x = -10; x <= 10; x++)
            for (int y = -10; y <= 10; y++)
            {
                bool orthographic = projection == ViewProjection.Isometric;
                var origin = orthographic ? camera.View.Position + (right * (x / 10f * aspect) + up * (y / 10f)) * camera.View.FovY / 2 : camera.View.Position;
                var direction = orthographic ? forward : forward + right * (x / 10f * aspect * tangent) + up * (y / 10f * tangent);
                var terrainHit = TerrainGeometry.Raycast(map, new Ray { Position = origin, Direction = Vector3.Normalize(direction) });
                if (!terrainHit.HasValue || !map.Contains(terrainHit.Value.X, terrainHit.Value.Z)) continue;
                var hit = terrainHit.Value;
                int tx = (int)MathF.Floor(hit.X + map.Width / 2f), tz = (int)MathF.Floor(hit.Z + map.Depth / 2f);
                Check(tx >= bounds.MinX && tx < bounds.MaxX && tz >= bounds.MinZ && tz < bounds.MaxZ,
                    "visible sculpted ground stays inside rendered tile bounds");
            }
        }
        foreach (var sides in new[] { (2049, 48), (48, 2049), (3, 48), (48, 3) })
        {
            try { MapData.CreateEmpty(sides.Item1, sides.Item2); }
            catch (ArgumentOutOfRangeException) { continue; }
            throw new InvalidOperationException("Out-of-range map size accepted.");
        }
        Console.WriteLine($"Large-map checks passed: 2048 x 2048, painting, sculpting, water, undo/redo, save/load, NPC/playtest and viewport rendering ({timer.Elapsed.TotalSeconds:0.00}s).");
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Large-map check failed: " + message);
    }
}
