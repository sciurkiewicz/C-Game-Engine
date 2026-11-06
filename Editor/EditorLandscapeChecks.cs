using System.Numerics;
using Raylib_cs;

static class EditorLandscapeChecks
{
    public static void Run()
    {
        Roads(); BiomePainting(); Erosion();
        Console.WriteLine("Landscape checks passed: editable roads/soft masks, vegetation clearing, biome mixtures/scatter, hydraulic/talus erosion, boundaries, undo/redo, snapshots and map compatibility.");
    }
    static void Roads()
    {
        var map = MapData.CreateEmpty(32, 24);
        map.Objects.Add(new() { Asset = "objects/vegetation/tree.png", X = -2, Z = 0 });
        map.Objects.Add(new() { Asset = "objects/rocks/rock.png", X = 2, Z = 0 });
        map.Objects.Add(new() { Asset = "objects/vegetation/tree.png", X = 0, Z = 8 });
        map.Npcs.Add(new() { X = 0, Z = 0 });
        var document = new MapDocument(map, true);
        var road = new RoadData { Name = "Forest trail", Width = 4, Softness = 1, Points = [new(-10, 0), new(0, 0), new(10, 0)] };
        document.ApplyRoad(road);
        Check(document.Map.Roads.Count == 1 && document.Dirty && map.Objects.Count == 2 && map.Npcs.Count == 1, "road only clears ordinary vegetation in its footprint");
        Check(map.Tiles.All(t => t == "grass"), "road keeps underlying terrain editable");
        var curve = RoadGeometry.Sample(road.Points);
        Check(ReferenceEquals(road.Curve, road.Curve), "unchanged roads reuse sampled curves for minimap and brush queries");
        Check(curve[0] == new Vector2(-10, 0) && curve[^1] == new Vector2(10, 0) && curve.Contains(Vector2.Zero), "curve passes through endpoints and control points");
        Check(RoadGeometry.Coverage(0, 4, 1) == 1 && Math.Abs(RoadGeometry.Coverage(1.5f, 4, 1) - .5f) < .001f && RoadGeometry.Coverage(2, 4, 1) == 0,
            "road has an opaque centre and smooth, bounded shoulders");
        var pixels = RoadMaterialMask.Rasterize(map, new(-16, -12), 128, 96, 4, new Dictionary<string, int> { ["dirt"] = 1 });
        Check(pixels[48 * 128 + 64].A == 255 && pixels[54 * 128 + 64].A is > 0 and < 255 && pixels[64 * 128 + 64].A == 0,
            "coverage raster preserves a soft edge below tile resolution");
        string state = map.Serialize();
        Check(MapSnapshot.Capture(map).Restore().Serialize() == state && MapData.Parse(state).Roads[0].Name == "Forest trail", "road points and settings survive snapshots and JSON");
        var play = new PlaySession(map); play.Map.Roads[0].Width = 8;
        Check(map.Roads[0].Width == 4, "Play edits cannot change editor roads");
        document.Undo(); Check(!document.Dirty && document.Map.Roads.Count == 0 && document.Map.Objects.Count == 3, "one undo restores vegetation and removes the whole road");
        document.Redo(); Check(document.Map.Serialize() == state, "road redo is exact");
        document.DeleteRoad(road.Id); Check(document.Map.Roads.Count == 0, "delete a road");
        document.Undo(); Check(document.Map.Roads.Count == 1, "undo road deletion");
        string legacy = "{\"Width\":4,\"Depth\":4,\"Tiles\":[" + string.Join(',', Enumerable.Repeat("\"grass\"", 16)) + "]}";
        Check(MapData.Parse(legacy).Roads.Count == 0, "legacy maps default to no roads");
        var invalid = new RoadData { Points = [new(0, 0), new(0, 0)] };
        string before = document.Map.Serialize();
        try { document.ApplyRoad(invalid); throw new Exception("Accepted duplicate road points."); } catch (InvalidDataException) { }
        Check(document.Map.Serialize() == before, "invalid road preserves current work");
        var bend = RoadGeometry.Sample([new(-8, -8), new(0, 5), new(8, -5)]);
        Check(bend.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)) && bend.Length > 30, "curves round a bend with finite samples");
    }
    static void BiomePainting()
    {
        string[] terrain = ["grass", "dirt", "sand", "stone", "terrain/forest_floor.png", "terrain/grass_forest.png", "terrain/grass_meadow.png", "terrain/grass_flowers.png", "terrain/gravel.png"];
        SpriteAsset[] sprites = [new("objects/vegetation/trees/tree.png", default, 128, 384), new("objects/vegetation/bushes/bush.png", default, 128, 128),
            new("objects/vegetation/grass/tuft.png", default, 64, 64), new("objects/rocks/rock.png", default, 128, 128)];
        foreach (var kind in Enum.GetValues<BiomeKind>())
        {
            var biome = Biomes.Create(kind, terrain, sprites);
            Check(biome.Terrain.Length == 3 && biome.Terrain.All(t => terrain.Contains(t.Id)) && biome.Objects.Length > 0, "biome resolves available graphics and materials: " + kind);
        }
        var forest = Biomes.Create(BiomeKind.Forest, terrain, sprites);
        var map = MapData.CreateEmpty(48, 40); map.Player.X = -20; map.Player.Z = -16;
        map.Roads.Add(new() { Width = 3, Points = [new(-12, 0), new(12, 0)] });
        var document = new MapDocument(map, true);
        document.Checkpoint();
        var first = document.PaintBiome(Vector3.Zero, 10, forest, .1f, 1.5f, .25f, 17, 1);
        document.PaintBiome(new(5, 0, 0), 10, forest, .1f, 1.5f, .25f, 17, 2);
        Check(first.Length > 0 && map.Tiles.Distinct().Count() > 1 && map.Heights.Length == 0, "biome paints a mixture and places graphics without changing elevation");
        Check(map.Objects.All(o => Math.Abs(o.Z) >= 1.7f || Math.Abs(o.X) > 12), "biome leaves road footprints free of new vegetation");
        Check(map.Tiles[0] == "grass" && map.Tiles[^1] == "grass", "biome does not affect distant cells");
        string state = map.Serialize();
        document.Undo(); Check(!document.Dirty && document.Map.Objects.Count == 0 && document.Map.Tiles.All(t => t == "grass"), "one undo restores a complete biome stroke");
        document.Redo(); Check(document.Map.Serialize() == state, "biome redo keeps the same random layout");
        document.Map.Validate();
        var water = MapData.CreateEmpty(16, 16); water.WaterLevel = 2; water.WaterCells = Enumerable.Repeat((byte)3, 256).ToArray();
        var wet = new MapDocument(water, true); wet.PaintBiome(Vector3.Zero, 5, forest, .2f, 1, .2f, 1, 1);
        Check(!wet.Dirty && water.Objects.Count == 0, "biome skips deep water");
        var edge = new MapDocument(MapData.CreateEmpty(16, 12), true);
        edge.Checkpoint(); edge.PaintBiome(new(7.8f, 0, 5.8f), 8, forest, .2f, 1, .2f, 2, 3); edge.Map.Validate();
        Check(edge.Map.Objects.All(o => Vector2.Distance(new(o.X, o.Z), new(edge.Map.Player.X, edge.Map.Player.Z)) >= 1.5f), "biome leaves clearance around the player start");
    }
    static void Erosion()
    {
        foreach (var kind in Enum.GetValues<ErosionKind>())
        {
            var map = MapData.CreateEmpty(48, 40); var document = new MapDocument(map, true);
            document.StampTerrain(Vector3.Zero, 12, 8, TerrainStampShape.Mountain, 7);
            string file = Path.Combine(Path.GetTempPath(), "wildwood-erosion-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                document.Save(file); string before = map.Serialize();
                int changed = document.ErodeTerrain(Vector3.Zero, 10, .7f, 600, kind, 4);
                Check(changed > 0 && document.Dirty && map.Heights.All(h => float.IsFinite(h) && h >= MapData.MinElevation && h <= MapData.MaxElevation && h == MapData.SnapElevation(h)), "erosion creates valid quantized changes: " + kind);
                var original = MapData.Parse(before);
                for (int z = 0; z <= map.Depth; z++)
                for (int x = 0; x <= map.Width; x++)
                    if (Vector2.Distance(new(x - map.Width / 2f, z - map.Depth / 2f), Vector2.Zero) >= 10)
                        Check(map.VertexHeight(x, z) == original.VertexHeight(x, z), "erosion stays within its brush");
                var delta = map.Heights.Zip(original.Heights).Select(pair => pair.First - pair.Second).ToArray();
                Check(delta.Any(d => d < 0) && delta.Any(d => d > 0), "erosion both removes and deposits soil: " + kind);
                string after = map.Serialize(); document.Undo(); Check(document.Map.Serialize() == before && !document.Dirty, "erosion is one undo step");
                document.Redo(); Check(document.Map.Serialize() == after, "erosion redo is exact");
                var duplicate = new MapDocument(MapData.Parse(before), true);
                duplicate.ErodeTerrain(Vector3.Zero, 10, .7f, 600, kind, 4);
                Check(duplicate.Map.Serialize() == after, "erosion is repeatable for a seed");
                var patch = new ErosionPatch(original, Vector3.Zero, 10); double volume = patch.Heights.Sum(h => (double)h);
                if (kind == ErosionKind.Hydraulic) patch.Hydraulic(600, .7f, 4); else patch.Talus(30, .7f);
                Check(Math.Abs(patch.Heights.Sum(h => (double)h) - volume) < .01, "simulation conserves soil before height quantization");
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
        var flat = new MapDocument(MapData.CreateEmpty(16, 12), true);
        Check(flat.ErodeTerrain(Vector3.Zero, 6, .5f, 200, ErosionKind.Hydraulic, 1) == 0 && !flat.Dirty && !flat.CanUndo, "flat terrain creates no empty erosion history");
        flat.StampTerrain(new(6, 0, 4), 5, 8, TerrainStampShape.Mountain, 1);
        flat.ErodeTerrain(new(7.5f, 0, 5.5f), 8, .7f, 200, ErosionKind.Hydraulic, 1); flat.Map.Validate();
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Landscape check failed: " + message); }
}
