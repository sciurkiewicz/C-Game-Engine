using System.Numerics;
using Raylib_cs;

static class WallCacheChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Wall cache check failed: " + message); }
    public static void Run()
    {
        var map = MapData.CreateEmpty(64, 64); var document = new MapDocument(map, true);
        var index = map.GridWalls; ulong emptyHash = index.ContentHash;
        document.PaintWallCells([(7, 7)], new(), false);
        Check(ReferenceEquals(index, map.GridWalls) && index.At(7, 7) != null && index.ContentHash != emptyHash, "painting updates the existing spatial index");
        Check(index.ChunkHeight(0, 0) > 3 && index.ChunkHeight(5, 5) == 0, "chunk bounds use the actual cave wall height");
        Check(index.ChunkVersion(0, 0) > 0 && index.ChunkVersion(1, 0) > 0 && index.ChunkVersion(0, 1) > 0 && index.ChunkVersion(5, 5) == 0,
            "editing a corner invalidates adjacent chunks and preserves distant chunks");
        long version = index.Revision;
        Check(!document.PaintWallCells([(7, 7)], new(), false) && index.Revision == version, "a no-op stroke preserves cache versions");
        document.PaintWallCells([(7, 7)], new(), true);
        Check(index.ContentHash == emptyHash && index.Chunks.Count == 0 && !document.Dirty, "erasing the only cell restores the clean saved grid");
        Check(index.ChunkHeight(0, 0) == 0, "erasing the last wall clears cached height bounds");
        document.PaintWallCells([(2, 2), (8, 8), (15, 15)], new(), false);
        document.PaintWallCells([(8, 8)], new(), true);
        Check(map.WallCells.Count == 2 && index.At(15, 15) != null && index.At(8, 8) == null, "compact removal preserves the moved cell's index");
        document.Undo(); Check(document.Map.WallCells.Count == 3 && document.Map.WallCellAt(8, 8) != null, "undo rebuilds a valid spatial index");
        document.Redo(); Check(document.Map.WallCells.Count == 2 && document.Map.WallCellAt(8, 8) == null, "redo restores indexed removal");
        string file = Path.Combine(Path.GetTempPath(), "wildwood-wall-cache-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            document.Save(file); Check(!document.Dirty, "saved grid is clean");
            var cell = document.Map.WallCells[0];
            document.PaintWallCells([(cell.X, cell.Z)], new() { Height = 4 }, false);
            Check(document.Dirty, "same-size grid edits are detected without serializing the grid");
            document.Undo(); Check(!document.Dirty, "undo to the saved grid clears dirty state");
            document.Map.Player.X = 3; Check(document.Dirty, "non-grid metadata remains tracked");
            document.Map.Player.X = 0; Check(!document.Dirty, "reverting other metadata restores clean state");
            var count = document.Map.WallCells.Count;
            document.Map.WallCells.Clear(); Check(document.Dirty && document.Map.WallCellAt(2, 2) == null, "external list changes invalidate the index");
            document.Load(file); Check(document.Map.WallCells.Count == count && !document.Dirty, "load restores grid and saved-state checks");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        foreach (var projection in new[] { CameraProjection.Perspective, CameraProjection.Orthographic })
        {
            var camera = new Camera3D { Position = new(0, 4, 10), Target = Vector3.Zero, Up = Vector3.UnitY, FovY = projection == CameraProjection.Perspective ? 45 : 12, Projection = projection };
            var frustum = new WallFrustum(); frustum.Configure(camera, 800, 600);
            Check(frustum.Intersects(new(-1, -1, -1), new(1, 3, 1)), "visible wall bounds survive culling in " + projection);
            Check(!frustum.Intersects(new(100, 0, 0), new(101, 3, 1)), "sideways walls are culled in " + projection);
            Check(!frustum.Intersects(new(-1, 4, 20), new(1, 7, 21)), "walls behind the camera are culled in " + projection);
        }
        Console.WriteLine("Wall cache checks passed: local invalidation, in-place painting, compact removal, no-op strokes, saved-state checks, undo/redo and frustum culling.");
    }
}
