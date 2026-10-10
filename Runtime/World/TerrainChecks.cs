using System.Numerics;
using Raylib_cs;

static class TerrainChecks
{
    public static MapData CreateDemo()
    {
        var map = MapData.CreateEmpty(32, 32);
        for (int z = 0; z <= map.Depth; z++)
        for (int x = 0; x <= map.Width; x++)
        {
            float wx = x - map.Width / 2f, wz = z - map.Depth / 2f;
            float hill = 4 * MathF.Exp(-((wx - 6) * (wx - 6) + (wz + 4) * (wz + 4)) / 28) +
                1.6f * MathF.Exp(-((wx + 9) * (wx + 9) + (wz - 6) * (wz - 6)) / 18);
            float lake = 2.4f * MathF.Exp(-((wx + 2) * (wx + 2) + wz * wz) / 15);
            map.SetVertexHeight(x, z, hill - lake);
        }
        for (int z = 0; z < map.Depth; z++)
        for (int x = 0; x < map.Width; x++)
        {
            float wx = x - map.Width / 2f + .5f, wz = z - map.Depth / 2f + .5f;
            if (map.HeightAt(wx, wz) < -.25f) map.Tiles[z * map.Width + x] = "sand";
        }
        map.WaterLevel = -.5f;
        new MapDocument(map, false).FillWater(new(-2, 0, 0));
        map.Player.X = 5; map.Player.Z = 5;
        map.Objects.AddRange([
            new SceneObject { Asset = "objects/vegetation/trees/tree1.png", X = 6, Z = -4, Height = 4.5f },
            new SceneObject { Asset = "objects/vegetation/trees/tree2.png", X = -9, Z = 6, Height = 4 },
            new SceneObject { Asset = "objects/rocks/large/rock_01.png", X = 4, Z = 1, Height = 1.6f }
        ]);
        map.Validate(); return map;
    }
    public static void Run()
    {
        var doc = new MapDocument(MapData.CreateEmpty(16, 12), true);
        doc.Checkpoint(); doc.Sculpt(Vector3.Zero, 3, 2, SculptMode.Raise);
        Check(doc.Dirty && Near(doc.Map.HeightAt(0, 0), 2) && doc.Map.HeightAt(4, 0) == 0, "raising with brush falloff");
        Check(doc.Map.HeightAt(1, 0) > doc.Map.HeightAt(2, 0), "soft brush edge");
        doc.Undo(); Check(!doc.Dirty && doc.Map.HeightAt(0, 0) == 0, "undo removes the complete height stroke");
        doc.Redo(); Check(doc.Dirty && Near(doc.Map.HeightAt(0, 0), 2), "redo restores elevation");
        doc.Sculpt(Vector3.Zero, 3, 2, SculptMode.Lower);
        Check(!doc.Dirty && doc.Map.Heights.All(h => Near(h, 0)), "inverse stroke restores the saved flat surface");
        doc.Sculpt(Vector3.Zero, 3, 2, SculptMode.Lower);
        Check(doc.Map.HeightAt(0, 0) < -1.99f, "lowering makes a depression");
        doc.Sculpt(Vector3.Zero, 3, 1, SculptMode.Flatten, 1);
        Check(Near(doc.Map.HeightAt(0, 0), 1), "flatten targets a chosen height");
        doc.Map.SetVertexHeight(8, 6, 5);
        float peak = doc.Map.HeightAt(0, 0);
        doc.Sculpt(Vector3.Zero, 3, 1, SculptMode.Smooth);
        Check(doc.Map.HeightAt(0, 0) < peak, "smoothing removes a sharp peak");
        Check(doc.Map.Heights.All(h => h == MapData.SnapElevation(h)), "all sculpt modes store heights in quarter-metre steps");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var stroke = new MapDocument(MapData.CreateEmpty(8, 8), true);
            stroke.Checkpoint();
            for (int frame = 0; frame < fps; frame++) stroke.Sculpt(Vector3.Zero, 2, .8f / fps, SculptMode.Raise);
            Check(stroke.Map.HeightAt(0, 0) == .75f, "small frame increments accumulate before rounding at " + fps + " FPS");
            stroke.EndSculptStroke(); stroke.Checkpoint(); stroke.Sculpt(Vector3.Zero, 2, 1, SculptMode.Flatten, .38f);
            Check(stroke.Map.HeightAt(0, 0) == .5f, "flatten target rounds to 0.25");
            stroke.Map.SetVertexHeight(0, 0, -.38f);
            Check(stroke.Map.VertexHeight(0, 0) == -.5f, "negative elevations round to 0.25");
        }
        var ramp = MapData.CreateEmpty(8, 8);
        for (int z = 0; z <= ramp.Depth; z++)
        for (int x = 0; x <= ramp.Width; x++) ramp.SetVertexHeight(x, z, (x - 4) * .25f + (z - 4) * .25f);
        for (float x = -3.5f; x < 4; x += .5f)
        for (float z = -3.5f; z < 4; z += .5f)
        {
            Check(Near(ramp.HeightAt(x, z), x * .25f + z * .25f), "triangle interpolation follows the terrain plane");
            var hit = TerrainGeometry.Raycast(ramp, new Ray { Position = new(x, 10, z), Direction = -Vector3.UnitY });
            Check(hit.HasValue && Near(hit.Value.Y, ramp.HeightAt(x, z)), "vertical ray selects elevated terrain");
        }
        var oblique = TerrainGeometry.Raycast(ramp, new Ray { Position = new(10, 8, 3), Direction = Vector3.Normalize(new(-1, -.8f, -.3f)) });
        Check(oblique.HasValue && Near(oblique.Value.Y, ramp.HeightAt(oblique.Value.X, oblique.Value.Z)), "DDA ray enters the map and hits the actual mesh");
        Check(TerrainGeometry.Raycast(ramp, new Ray { Position = new(0, 10, 0), Direction = Vector3.UnitY }) == null, "ray away from terrain misses");
        Check(WorldRenderer.CanWalk(ramp, ramp.GroundPosition(0, 0)), "gentle slope is walkable");
        var steep = MapData.CreateEmpty(8, 8); steep.SetVertexHeight(5, 4, 4); steep.SetVertexHeight(5, 5, 4);
        Check(!WorldRenderer.CanWalk(steep, steep.GroundPosition(.5f, .25f)), "steep slopes stop movement");
        ramp.Player.X = 1; ramp.Player.Z = 2;
        var play = new PlaySession(ramp);
        Check(Near(play.Player.Y, ramp.HeightAt(1, 2)) && Near(play.Camera.View.Target.Y, play.Player.Y + 1.5f), "player and camera follow terrain height");
        var shape = new MapDocument(MapData.CreateEmpty(16, 16), false);
        shape.Sculpt(new(-3, 0, 0), 3, 2, SculptMode.Lower); shape.EndSculptStroke();
        shape.Sculpt(new(3, 0, 0), 2, 2, SculptMode.Lower); shape.Map.WaterLevel = -.5f;
        var waterDoc = new MapDocument(shape.Map, true);
        var bed = (float[])shape.Map.Heights.Clone(); var materials = (string[])shape.Map.Tiles.Clone();
        Check(waterDoc.FillWater(new(-3, 0, 0)) == WaterEditResult.Filled, "one click fills a connected basin");
        Check(waterDoc.Dirty && waterDoc.Map.WaterCells.Count(mask => mask != 0) > 9 && Near(waterDoc.Map.WaterDepthAt(-3, 0), 1.5f), "water fills the basin to the chosen level");
        Check(waterDoc.Map.WaterDepthAt(3, 0) == 0 && waterDoc.Map.Heights.SequenceEqual(bed) && waterDoc.Map.Tiles.SequenceEqual(materials), "fill preserves the terrain and does not cross into a separate basin");
        Check(!WorldRenderer.CanWalk(waterDoc.Map, waterDoc.Map.GroundPosition(-3, 0)), "deep water blocks movement");
        var mesh = WaterGeometry.BuildTile(waterDoc.Map, 5, 8, 0);
        Check(mesh.Count >= 2 && mesh.All(t => float.IsFinite(t.A.Y) && float.IsFinite(t.B.Y) && float.IsFinite(t.C.Y)), "water has finite surface geometry");
        Check(Math.Abs(WaterGeometry.SurfaceHeight(waterDoc.Map, -2.5f, .5f, 0) - WaterGeometry.SurfaceHeight(waterDoc.Map, -2.5f, .5f, 1)) > .005f, "waves animate geometry height");
        Check(mesh.SelectMany(t => new[] { t.A, t.B, t.C }).All(v => Math.Abs(v.Y - waterDoc.Map.WaterLevel) <= waterDoc.Map.WaveHeight), "water has a surface without vertical block walls");
        waterDoc.Undo(); Check(!waterDoc.Dirty && waterDoc.Map.WaterCells.Length == 0 && waterDoc.Map.Heights.SequenceEqual(bed), "one undo removes the fill and keeps the excavated terrain");
        waterDoc.Redo(); Check(waterDoc.Map.WaterDepthAt(-3, 0) > 1.49f, "redo restores the basin fill");
        Check(waterDoc.FillWater(new(-3, 0, 0), true) == WaterEditResult.Drained && waterDoc.Map.WaterDepthAt(-3, 0) == 0 && waterDoc.Map.Heights.SequenceEqual(bed), "draining preserves the bed");
        Check(!waterDoc.Dirty, "filling and draining restores the saved dry terrain");
        waterDoc.Undo();
        int beforeRise = waterDoc.Map.WaterCells.Count(mask => mask != 0);
        waterDoc.SetWaterLevel(-.25f);
        Check(waterDoc.Map.WaterCells.Count(mask => mask != 0) >= beforeRise, "raising the level expands the shore through connected terrain");
        waterDoc.SetWaterLevel(.25f);
        Check(waterDoc.Map.WaterLevel == .25f && waterDoc.Map.WaterCells.All(mask => mask == 3) && waterDoc.Map.Heights.SequenceEqual(bed),
            "raising water past the rim fills connected ground all the way to the map edges");
        var openMap = MapData.CreateEmpty(8, 8); openMap.WaterLevel = 1;
        var open = new MapDocument(openMap, true);
        Check(open.FillWater(Vector3.Zero) == WaterEditResult.Filled && open.Dirty && open.Map.WaterCells.Length == 64 && open.Map.WaterCells.All(mask => mask == 3),
            "filling at the map boundary completes the entire connected area");
        open.Undo(); Check(!open.Dirty && open.Map.WaterCells.Length == 0, "undo restores dry ground after a fill reaching the edges");
        open.Redo(); open.FillWater(Vector3.Zero, true);
        Check(!open.Dirty && open.Map.WaterCells.All(mask => mask == 0), "draining water at the edges completes the entire connected area");
        Check(new MapDocument(MapData.CreateEmpty(4, 4), true).FillWater(Vector3.Zero) == WaterEditResult.NoBasin,
            "dry ground at or above the water level is unchanged");
        var saddle = MapData.CreateEmpty(4, 4); saddle.Heights = Enumerable.Repeat(1f, saddle.VertexCount).ToArray();
        saddle.SetVertexHeight(1, 2, -1); saddle.SetVertexHeight(2, 1, -1); saddle.WaveHeight = 0;
        var saddleDoc = new MapDocument(saddle, true);
        Check(saddleDoc.FillWater(new(-.9f, 0, -.1f)) == WaterEditResult.Filled && saddleDoc.Map.WaterMask(5) == 1 &&
            saddleDoc.Map.WaterDepthAt(-.1f, -.9f) == 0, "fill does not leak between disconnected halves of the same terrain tile");
        var shore = MapData.CreateEmpty(4, 4); shore.Tiles[5] = "water";
        shore.SetVertexHeight(1, 1, -1); shore.SetVertexHeight(1, 2, -1); shore.SetVertexHeight(2, 1, 1); shore.SetVertexHeight(2, 2, 1);
        foreach (var triangle in WaterGeometry.BuildTile(shore, 1, 1, 1))
        foreach (var v in new[] { triangle.A, triangle.B, triangle.C })
            Check(shore.HeightAt(v.X, v.Z) <= shore.WaterLevel + .0001f && v.Y >= shore.HeightAt(v.X, v.Z) - .0001f, "water is clipped to the shoreline");
        var legacyWater = MapData.CreateEmpty(4, 4); legacyWater.Tiles[5] = "water"; legacyWater.WaterLevel = .5f;
        var legacyDoc = new MapDocument(legacyWater, true);
        Check(legacyDoc.Map.WaterDepthAt(-.5f, -.5f) == .5f && MapSnapshot.Capture(legacyWater).Restore().WaterMask(5) == 3,
            "old water tiles remain readable");
        legacyDoc.FillWater(new(-.5f, 0, -.5f), true);
        Check(legacyDoc.Map.WaterDepthAt(-.5f, -.5f) == 0 && legacyDoc.Dirty, "legacy water can be drained");
        legacyDoc.Undo(); Check(!legacyDoc.Dirty && legacyDoc.Map.WaterMask(5) == 3, "undo preserves legacy water representation");
        string path = Path.Combine(Path.GetTempPath(), "wildwood-terrain-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var demo = CreateDemo(); demo.Save(path); var loaded = MapData.Load(path);
            Check(loaded.Serialize() == demo.Serialize(), "heights and water survive map save/load");
            var snapshot = MapSnapshot.Capture(demo).Restore();
            Check(snapshot.Heights.SequenceEqual(demo.Heights) && snapshot.WaterCells.SequenceEqual(demo.WaterCells) && snapshot.WaterLevel == demo.WaterLevel, "history and playtest preserve heights and basin fill");
            demo.Heights[0] = 12;
            Check(snapshot.Heights[0] != 12, "height arrays are independent in snapshots");
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(new MapData().Serialize())!;
            legacy.AsObject().Remove("Heights"); legacy.AsObject().Remove("WaterLevel"); legacy.AsObject().Remove("WaveHeight"); legacy.AsObject().Remove("WaterCells");
            Check(MapData.Parse(legacy.ToJsonString()).HeightAt(0, 0) == 0, "existing maps load as flat terrain");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        foreach (Action<MapData> invalid in new Action<MapData>[] { m => m.Heights = [1], m => m.WaterLevel = float.NaN, m => m.WaveHeight = 2,
            m => { m.Heights = new float[m.VertexCount]; m.Heights[0] = float.NaN; }, m => m.WaterCells = [3],
            m => { m.WaterCells = new byte[m.Tiles.Length]; m.WaterCells[0] = 4; } })
        {
            var map = MapData.CreateEmpty(4, 4); invalid(map);
            try { map.Validate(); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Invalid terrain accepted.");
        }
        Console.WriteLine("Terrain checks passed: 0.25 elevation steps, accumulated strokes, basin fill/drain, water levels/shorelines, undo/redo, ray picking, grounded actors and map compatibility.");
    }
    static bool Near(float a, float b) => Math.Abs(a - b) < .001f;
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Terrain check failed: " + message); }
}
