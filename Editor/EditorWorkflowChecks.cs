using System.Numerics;
using System.Text.Json;
using Raylib_cs;

static class EditorWorkflowChecks
{
    public static void Run()
    {
        CheckCamera(); CheckStamps(); CheckScatter(); CheckRecovery(); CheckOverview();
        Check(SearchInput.Matches("ŻÓŁW npc", "Strażnik Żółw", "NPC") &&
            !SearchInput.Matches("rock missing", "rock_01.png"), "search matches all words across fields, including Polish text");
        var input = new SearchInput(); input.Append("Łąka\r\n");
        Check(input.Value == "Łąka", "search strips control characters");
        Console.WriteLine("Editor workflow checks passed: free camera, stamps, scatter, recovery retention/isolation and rectangular minimap.");
    }
    static void CheckCamera()
    {
        var camera = new EditorCamera(Vector3.Zero);
        var original = camera.View;
        camera.EnterFree();
        Check(camera.IsFree && Vector3.Distance(camera.View.Position, original.Position) < .001f &&
            Vector3.Dot(Vector3.Normalize(original.Target - original.Position), camera.Forward) > .999f, "free camera enters without a visual jump");
        camera.Move(1, 1, 1, 1, false);
        Check(Math.Abs(Vector3.Distance(camera.View.Position, original.Position) - camera.Speed) < .001f, "diagonal flight keeps a consistent speed");
        camera.Look(new(1000, 100000));
        Check(float.IsFinite(camera.View.Target.X) && Vector3.Cross(camera.Forward, Vector3.UnitY).Length() > .01f, "free camera keeps a valid basis at its pitch limit");
        float orbitDistance = Vector3.Distance(camera.View.Position, Vector3.Zero);
        camera.Orbit(Vector3.Zero, new(90, -120));
        Check(Math.Abs(Vector3.Distance(camera.View.Position, Vector3.Zero) - orbitDistance) < .001f, "orbit preserves distance to selection");
        camera.ChangeSpeed(1000); Check(camera.Speed == 100, "flight speed has an upper bound");
        camera.ChangeSpeed(-1000); Check(camera.Speed == .5f, "flight speed has a lower bound");
        camera.ApplySettings(new() { Projection = ViewProjection.Isometric, InitialDistance = 22 }); camera.Snap(Vector3.Zero);
        Check(!camera.IsFree && camera.View.Projection == CameraProjection.Orthographic && camera.Distance == 22, "return to saved game camera settings");
        foreach (var projection in Enum.GetValues<ViewProjection>())
        {
            camera.Projection = projection;
            var heading = Vector3.Normalize(camera.View.Position - camera.View.Target);
            camera.EnterFree(); camera.Move(1, 1, 1, .5f, true); camera.Look(new(300, -100));
            camera.Orbit(Vector3.Zero, new(75, 50)); camera.Pan(new(20, -15), 600); camera.Zoom(2); camera.Snap(new(3, 0, 4));
            var terrainView = camera.TerrainTextureView;
            Check(Vector3.Distance(Vector3.Normalize(terrainView.Position - terrainView.Target), heading) < .0001f,
                "free look, orbit, flight, pan, zoom and focus preserve ground orientation from " + projection);
        }
        var huge = MapData.CreateEmpty(2048, 2048);
        var horizon = new Camera3D { Position = new(0, 12, 0), Target = new(0, 12, -1), Up = Vector3.UnitY, FovY = 35, Projection = CameraProjection.Perspective };
        var bounds = TerrainVisibility.GetBounds(huge, horizon, 800, 600, 128);
        Check(bounds.TileCount <= 256 * 256 && bounds.TileCount > 0, "horizon views cannot allocate terrain for the entire 2048 map");
    }
    static void CheckStamps()
    {
        foreach (var shape in Enum.GetValues<TerrainStampShape>())
        {
            var doc = new MapDocument(MapData.CreateEmpty(32, 32), true);
            doc.Checkpoint(); doc.StampTerrain(Vector3.Zero, 8, 4, shape, 7);
            Check(doc.Dirty && doc.Map.Heights.Any(h => h != 0) && doc.Map.Heights.All(h => h == MapData.SnapElevation(h)), "stamp creates quantized terrain: " + shape);
            Check(doc.Map.VertexHeight(0, 0) == 0 && doc.Map.VertexHeight(24, 16) == 0, "stamp has no changes outside its radius");
            if (shape == TerrainStampShape.Crater) Check(doc.Map.HeightAt(0, 0) < 0, "crater depresses its centre");
            else Check(doc.Map.HeightAt(0, 0) > 2, "mountain, hill and ridge raise their centre");
            string state = doc.Map.Serialize();
            doc.Undo(); Check(!doc.Dirty && doc.Map.Heights.Length == 0, "stamp undo restores a clean flat map");
            doc.Redo(); Check(doc.Map.Serialize() == state, "stamp redo is exact");
            doc.StampTerrain(new(15, 0, 15), 8, -32, shape, 2);
            doc.Map.Validate();
            Check(MapData.Parse(doc.Map.Serialize()).Heights.SequenceEqual(doc.Map.Heights), "edge stamps survive map serialization");
        }
        var a = new MapDocument(MapData.CreateEmpty(), true);
        a.StampTerrain(Vector3.Zero, 10, 5, TerrainStampShape.Mountain, 2);
        var b = new MapDocument(MapData.CreateEmpty(), true);
        b.StampTerrain(Vector3.Zero, 10, 5, TerrainStampShape.Mountain, 3);
        Check(!a.Map.Heights.SequenceEqual(b.Map.Heights), "successive seeds vary the mountain shape");
        float first = a.Map.HeightAt(0, 0);
        a.StampTerrain(Vector3.Zero, 10, 5, TerrainStampShape.Mountain, 3);
        Check(a.Map.HeightAt(0, 0) > first, "repeated clicks build terrain cumulatively");
    }
    static void CheckScatter()
    {
        var map = MapData.CreateEmpty(32, 20);
        ScatterAsset[] choices = [new("objects/tree.png", 3, true), new("objects/rock.png", 1, false)];
        var placed = ObjectScatter.Place(map, Vector3.Zero, 6, 30, 1, .2f, choices, 4);
        Check(placed.Length == 30 && placed.Select(p => p.Asset).Distinct().Count() == 2, "scatter mixes several assets");
        Check(placed.All(p => map.Contains(p.X, p.Z) && Vector2.Distance(new(p.X, p.Z), Vector2.Zero) <= 6), "scatter stays inside the brush and map");
        Check(placed.All(p => p.Height >= choices.First(a => a.Id == p.Asset).Height * .8f && p.Height <= choices.First(a => a.Id == p.Asset).Height * 1.2f), "scatter bounds size variation");
        for (int i = 0; i < placed.Length; i++)
        for (int j = i + 1; j < placed.Length; j++)
            Check(Vector3.Distance(placed[i].Position, placed[j].Position) >= 1, "scatter enforces minimum distance");
        var copy = MapData.CreateEmpty(32, 20);
        var same = ObjectScatter.Place(copy, Vector3.Zero, 6, 30, 1, .2f, choices, 4);
        Check(placed.Select(p => (p.Position, p.Asset, p.Height)).SequenceEqual(same.Select(p => (p.Position, p.Asset, p.Height))), "scatter is deterministic for a seed");
        ObjectScatter.Place(map, new(15.8f, 0, 9.8f), 4, 12, 1, 0, choices, 8);
        map.Validate();
        var doc = new MapDocument(MapData.CreateEmpty(), true);
        doc.Checkpoint(); ObjectScatter.Place(doc.Map, Vector3.Zero, 6, 10, 1, 0, choices, 1);
        ObjectScatter.Place(doc.Map, new(8, 0, 0), 6, 10, 1, 0, choices, 2);
        doc.Undo(); Check(doc.Map.Objects.Count == 0 && !doc.Dirty, "one undo removes the complete scatter stroke");
    }
    static void CheckRecovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "wildwood-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "map.json");
            var doc = new MapDocument(MapData.CreateEmpty(16, 12), false); doc.Save(path);
            string saved = File.ReadAllText(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
            var store = new EditorRecoveryStore(path, Path.Combine(root, "copies"));
            for (int i = 0; i < 7; i++)
            { doc.Checkpoint(); doc.Map.Player.X = i; store.Save(doc.Map); }
            Check(store.Copies().Length == EditorRecoveryStore.MaximumCopies && File.ReadAllText(path) == saved && doc.Dirty, "autosave rotates five copies without saving the document");
            Check(store.LatestUnsaved() != null && new EditorRecoveryStore(Path.Combine(root, "other.json"), Path.Combine(root, "copies")).Copies().Length == 0,
                "recovery detects newer work and isolates map paths");
            string latest = store.Copies()[0].Path;
            doc.Load(path); doc.Recover(latest);
            Check(doc.Map.Player.X == 6 && doc.Dirty, "recover keeps the map marked as unsaved");
            doc.Undo(); Check(doc.Map.Player.X == 0 && !doc.Dirty, "recovery is undoable");
            string corrupt = Path.Combine(root, "broken.json"); File.WriteAllText(corrupt, "{broken");
            try { doc.Recover(corrupt); throw new Exception("Accepted a corrupt recovery file."); }
            catch (JsonException) { }
            Check(!doc.Dirty && doc.Map.Player.X == 0, "invalid recovery preserves current work");
            string racePath = Path.Combine(root, "manual-save.json"); doc.Map.Save(racePath);
            var slowStore = new EditorRecoveryStore(racePath, Path.Combine(root, "copies"));
            slowStore.Save(doc.Map, DateTime.UtcNow.AddMinutes(-2));
            Check(slowStore.LatestUnsaved() == null, "a delayed background write cannot supersede a newer manual save");
        }
        finally { Directory.Delete(root, true); }
    }
    static void CheckOverview()
    {
        var map = MapData.CreateEmpty(80, 20);
        var rect = new Rectangle(100, 50, 200, 50);
        var point = map.GroundPosition(23, -4);
        Check(Vector3.Distance(TerrainOverview.Unproject(map, TerrainOverview.Project(map, point, rect), rect), point) < .001f, "rectangular minimap coordinates round-trip");
        Check(Vector2.Distance(TerrainOverview.Project(map,new(-40,0,10),rect),new(100,50))<.001f,
            "the default minimap's top-left corner follows the viewport's -Z right axis");
        var available=new Rectangle(30,50,220,220);
        var fitted=TerrainOverview.FitBounds(map,available);
        Check(Math.Abs(fitted.Width/fitted.Height-.25f)<.0001f,
            "a 90 degree overview swaps the display dimensions of a rectangular map");
        foreach(float yaw in new[]{FollowCamera.DefaultYaw,FollowCamera.IsometricYaw})
        {
            var area=TerrainOverview.FitBounds(map,available,yaw);
            var center=TerrainOverview.Project(map,Vector3.Zero,area,yaw);
            var right=new Vector3(MathF.Cos(yaw),0,-MathF.Sin(yaw));
            var down=new Vector3(MathF.Sin(yaw),0,MathF.Cos(yaw));
            var rightPixel=TerrainOverview.Project(map,right*5,area,yaw);
            var downPixel=TerrainOverview.Project(map,down*5,area,yaw);
            Check(rightPixel.X>center.X&&Math.Abs(rightPixel.Y-center.Y)<.001f&&
                downPixel.Y>center.Y&&Math.Abs(downPixel.X-center.X)<.001f,
                "overview screen axes match the camera heading in each fixed projection");
            foreach(var location in new Vector3[]{new(23,0,-4),new(-31,0,8),new(39,0,-9),Vector3.Zero})
                Check(Vector3.Distance(TerrainOverview.Unproject(map,TerrainOverview.Project(map,location,area,yaw),area,yaw),location)<.001f,
                    "click navigation reverses rotated projection for asymmetric world points");
            foreach(float x in new[]{-map.Width*.5f,map.Width*.5f})
            foreach(float z in new[]{-map.Depth*.5f,map.Depth*.5f})
            {
                var corner=TerrainOverview.Project(map,new(x,0,z),area,yaw);
                Check(corner.X>=area.X-.001f&&corner.X<=area.X+area.Width+.001f&&
                    corner.Y>=area.Y-.001f&&corner.Y<=area.Y+area.Height+.001f,
                    "the complete rotated world fits its overview bounds");
            }
        }
        var iso=TerrainOverview.Size(map,FollowCamera.IsometricYaw);
        Check(Math.Abs(iso.X-iso.Y)<.001f,"isometric map bounds contain the entire rotated rectangle without stretching");
        var color = TerrainOverview.Sample(map, .5f, .5f, _ => new(90, 120, 60, 255));
        Check(color.G > color.R && color.A == 255, "overview uses the terrain material color");
        map.WaterCells=Enumerable.Repeat((byte)3,map.Tiles.Length).ToArray(); map.WaterLevel=.25f;
        map.WaterAppearance=map.WaterAppearance with{R=220,G=30,B=40,DeepR=100,DeepG=20,DeepB=30,Clarity=1};
        color=TerrainOverview.Sample(map,.5f,.5f,_=>new(90,120,60,255));
        Check(color.R>180&&color.G<40,"minimap water follows the authored shallow color");
        map.WaterLevel=4;
        color=TerrainOverview.Sample(map,.5f,.5f,_=>new(90,120,60,255));
        Check(color.R==100&&color.G==20&&color.B==30,"deep minimap water follows the authored deep color");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Editor workflow check failed: " + message); }
}
