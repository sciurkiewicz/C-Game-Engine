using System.Numerics;
using Raylib_cs;

static class WallChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Wall check failed: " + message); }
    public static void Run()
    {
        var map = MapData.CreateEmpty(24, 24, MapType.Cave);
        var document = new MapDocument(map, true);
        var wall = new WallData { Name = "Północna ściana", Points = [new(-4, 0), new(4, 0)] };
        document.ApplyWall(wall); string id = wall.Id;
        Check(document.Dirty && document.Map.Walls.Count == 1, "walls participate in document changes");
        wall.Points.Clear();
        Check(document.Map.Walls[0].Points.Count == 2, "committing a draft does not retain mutable draft references");
        string applied = document.Map.Serialize();
        document.Undo(); Check(document.Map.Walls.Count == 0 && !document.Dirty, "one undo removes a wall and restores clean state");
        document.Redo(); Check(document.Map.Serialize() == applied, "redo restores material, geometry and stable ID");
        wall = document.Map.Walls[0];
        Check(!WorldRenderer.CanWalk(document.Map, new(0, 0, 0)), "wall blocks stationary overlap");
        Check(!WorldRenderer.CanWalk(document.Map, new(0, 0, 3), new(0, 0, -3)), "a long movement cannot tunnel through a thin wall");
        Check(WorldRenderer.CanWalk(document.Map, new(5, 0, 3), new(5, 0, -3)), "wall endpoints leave a walkable passage");
        Check(WorldRenderer.CanWalk(document.Map, new(0, 0, .8f), new(0, 0, .1f)), "an overlapping spawn can step out of a wall");
        wall.Solid = false;
        Check(WorldRenderer.CanWalk(document.Map, new(0, 0, 3), new(0, 0, -3)), "collision toggle leaves a decorative wall walkable");
        wall.Solid = true;
        var ray = new Ray { Position = new(0, 1, -4), Direction = Vector3.UnitZ };
        Check(WallGeometry.RayDistance(document.Map, wall, ray) is { } hit && Math.Abs(hit - 3.7f) < .01f, "ray picking uses the visible wall face");
        Check(WallGeometry.RayDistance(document.Map, wall, ray with { Position = new(0, 6, -4) }) == null, "picking above a wall misses");
        var mesh = wall.Geometry(document.Map);
        Check(ReferenceEquals(mesh, wall.Geometry(document.Map)) && mesh.Faces.All(f => float.IsFinite(f.Normal.Y)), "wall geometry caches valid surface normals");
        document.Map.SetVertexHeight(12, 12, 1);
        var sloped = wall.Geometry(document.Map);
        Check(!ReferenceEquals(mesh, sloped) && sloped.Max.Y > mesh.Max.Y, "sculpting refreshes wall geometry and keeps it attached to ground");
        foreach (var f in sloped.Faces)
            foreach (var p in new[] { f.A, f.B, f.C, f.D })
                Check(p.Y >= document.Map.HeightAt(p.X, p.Z) - .061f, "the wall base follows terrain at each sampled edge");
        var square = new WallData { Closed = true, Style = WallStyle.Masonry, Points = [new(-3, -3), new(3, -3), new(3, 3), new(-3, 3)] };
        square.Validate(document.Map.Contains); document.ApplyWall(square);
        Check(!WorldRenderer.CanWalk(document.Map, new(0, 0, 5), new(0, 0, 1)), "closed room includes its final segment");
        Check(!WorldRenderer.CanWalk(document.Map, new(-5, 0, 1), new(-1, 0, 1)), "closed room blocks the closing side");
        Check(WorldRenderer.CanWalk(document.Map, new(1, 0, 1), new(1, 0, 2)), "room interior remains walkable");
        var diagonal = new WallData { Points = [new(-3, -3), new(3, 3)] };
        Check(diagonal.Blocks(new(-1, 1), new(1, -1), .25f) && !diagonal.Blocks(new(-3, 2), new(-2, 3), .25f), "diagonal collision follows the wall rather than its bounding box");

        foreach (Action<WallData> corrupt in new Action<WallData>[] {
            w => w.Points = null!, w => w.Points = [new(0, 0)], w => w.Points[0] = null!,
            w => w.Points[0] = new(float.NaN, 0), w => w.Points[0] = new(100, 0), w => w.Points[1] = w.Points[0],
            w => w.Height = float.NaN, w => w.Height = 0, w => w.Thickness = 5, w => w.Style = (WallStyle)100,
            w => w.Name = " ", w => w.Closed = true, w => w.Points.Add(new(-4, 0)) })
        {
            var invalid = ScenePrefab.Clone(wall); corrupt(invalid);
            string before = document.Map.Serialize();
            try { document.ApplyWall(invalid); throw new Exception("Invalid wall accepted."); } catch (InvalidDataException) { }
            Check(document.Map.Serialize() == before, "invalid wall does not alter the document");
        }
        var legacy = MapData.Parse("{\"Width\":4,\"Depth\":4,\"Tiles\":[\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\",\"grass\"]}");
        Check(legacy.Walls.Count == 0, "older maps load without a Walls field");
        string path = Path.Combine(Path.GetTempPath(), "wildwood-walls-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            document.Save(path); var loaded = MapData.Load(path);
            Check(loaded.Serialize() == document.Map.Serialize() && !document.Dirty, "map save/load preserves wall settings and rooms");
            loaded.Player.Z = -2;
            var play = new PlaySession(loaded);
            Check(play.Map.Walls.Count == 2 && !play.TryMovePlayer(new(0, 0, 2)), "the actual gameplay session includes wall collision");
            play.Map.Walls.Clear(); Check(loaded.Walls.Count == 2, "Play edits an independent map copy");
            document.DeleteWall(id); Check(document.Map.Walls.Count == 1, "delete wall");
            document.Undo(); Check(document.Map.Walls.Count == 2, "undo wall deletion");
        }
        finally { if (File.Exists(path)) File.Delete(path); }

        CombatAndNavigation();
        Console.WriteLine("Wall checks passed: geometry, slopes, picking, collision, rooms, undo/redo, compatibility, save/load, gameplay and combat.");
    }
    static void CombatAndNavigation()
    {
        var map = MapData.CreateEmpty(16, 16); map.Player.X = -.6f;
        map.Monsters.Add(new() { X = .6f, MoveSpeed = .25f });
        map.Walls.Add(new() { Thickness = .2f, Points = [new(0, -2), new(0, 2)] });
        var play = new PlaySession(map);
        for (int i = 0; i < 20; i++) play.UpdateCombat(.1f, true);
        Check(play.PlayerHealth == 100 && play.Monsters.Single().Health == 30, "both sides cannot attack through a solid wall");
        play.Map.Walls[0].Solid = false;
        for (int i = 0; i < 5; i++) play.UpdateCombat(.1f, true);
        Check(play.PlayerHealth < 100 && play.Monsters.Single().Health < 30, "a decorative non-solid wall permits combat");
        map.Player.X = 3; map.Monsters[0].X = -3; map.Monsters[0].MoveSpeed = 2.4f; map.Monsters[0].DetectionRange = 12;
        var chase = new PlaySession(map);
        for (int i = 0; i < 160 && chase.PlayerHealth == 100; i++)
        {
            chase.UpdateCombat(.1f);
            var actor = chase.Map.Monsters[0];
            Check(WorldRenderer.CanWalk(chase.Map, chase.Map.GroundPosition(actor), null, actor.Id, WorldRenderer.CollisionRadius(actor)), "monsters never enter wall geometry");
        }
        Check(chase.PlayerHealth < 100, "monsters can navigate around a wall and reach the player");
    }
}
