using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunGridWallInteractionChecks()
    {
        SetWindowSize(1440, 900); EndEditorGesture(); StopFragmentTool(true); ResetWallDraft();
        document.New(20, 20, MapType.Cave); tool = EditTool.Select; selectedId = null; selectedIds.Clear(); activePrefab = null;
        wallDraft = new(); wallGridMode = true; gridWallErase = gridWallRectangle = false; gridWallRadius = 0; gridWallRoughness = .75f;
        preferences.ShowMinimap = false; preferences.AutosaveSeconds = 0; assetSearch.Focused = false; grid = snap = true;
        document.Map.Player.X = -6; document.Map.Player.Z = 4; document.Map.Environment.Audio = false;
        document.Map.Lights.Add(new() { Height = 5, Radius = 24, Intensity = 2.5f, R = 255, G = 220, B = 175 });
        document.ApplyWall(new() { Points = [new(-8, 6), new(-3, 6)] });
        focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame(); Save();
        Vector2 Screen(int x, int z) => GetWorldToScreenEx(document.Map.GroundPosition(x + .5f - document.Map.Width / 2f, z + .5f - document.Map.Depth / 2f), camera.View,
            (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        void Drag((int X, int Z) a, (int X, int Z) b)
        {
            var start = Screen(a.X, a.Z); var end = Screen(b.X, b.Z);
            Input(7, (int)start.X, (int)start.Y); Input(6, 0); TestFrame();
            Input(7, (int)end.X, (int)end.Y); TestFrame(); Input(5, 0); TestFrame();
        }
        ClickHeader("Walls"); Verify(wallGridMode, "Walls opens the dungeon grid mode");
        string before = document.Map.Serialize(); Drag((3, 6), (12, 6));
        Verify(document.Map.WallCells.Count == 10 && document.Map.Walls.Count == 1, "drag paints a continuous grid wall and preserves old point walls");
        string firstStroke = document.Map.Serialize();
        ClickHeader("Undo"); Verify(document.Map.Serialize() == before, "one undo removes the whole grid brush stroke");
        ClickHeader("Redo"); Verify(document.Map.Serialize() == firstStroke, "redo restores the same wall cells and cave shape");
        Drag((7, 3), (7, 12)); Verify(document.Map.WallCells.Count == 19, "crossing strokes join without duplicate cells");
        Input(2, (int)KeyboardKey.LeftShift); Drag((7, 6), (7, 6)); Input(1, (int)KeyboardKey.LeftShift); TestFrame();
        Verify(document.Map.WallCellAt(7, 6) == null && document.Map.WallCells.Count == 18, "Shift carves a passage through a wall");
        Click(100, 403); Verify(gridWallRectangle, "room outline mode activates");
        Drag((2, 12), (6, 16));
        Verify(document.Map.WallCellAt(2, 12) != null && document.Map.WallCellAt(6, 16) != null && document.Map.WallCellAt(4, 14) == null,
            "dragging a room paints its perimeter and leaves the floor clear");
        Click(100, 161); Click(100, 319); int ix = GetScreenWidth() - Right + 18;
        Click(ix + 201, 214); Drag((3, 6), (3, 6));
        Verify(document.Map.WallCellAt(3, 6) is { Style: WallStyle.Masonry, Height: 3.25f }, "painting over a grid cell updates material and height");
        Click(100, 203); Drag((3, 6), (3, 6)); Verify(document.Map.WallCellAt(3, 6) == null, "eraser button removes a grid cell");
        ClickHeader("Undo"); Verify(document.Map.WallCellAt(3, 6) != null, "undo restores an erased grid cell");
        Click(115, 114); Verify(!wallGridMode && document.Map.WallCells.Count > 0, "Points mode retains all dungeon cells");
        wallDraft.Height = 24;
        Click(195, 114); Verify(wallGridMode && document.Map.Walls.Count == 1, "Grid mode retains existing point walls");
        Verify(wallDraft.Height == 12, "switching to grid mode displays its actual maximum brush height");
        preferences.ShowMinimap = true; TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dungeon-editor-perspective.png"));
        Press(KeyboardKey.V); TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dungeon-editor-isometric.png"));
        Save(); Verify(MapData.Load(mapPath).WallCells.Count == document.Map.WallCells.Count, "grid walls save to the authored map");
        string authored = document.Map.Serialize(); Press(KeyboardKey.F5);
        Verify(play != null && play.Map.WallCells.Count > 0 && !play.TryMovePlayer(new(-6, 0, -6)), "Play loads the dungeon and blocks movement through grid walls");
        play!.Map.WallCells.Clear(); Press(KeyboardKey.F5);
        Verify(document.Map.Serialize() == authored, "Play cannot alter the editor dungeon");
        Complete("load"); Verify(document.Map.WallCells.Count > 0, "load restores the dungeon grid");
        SetWindowSize(1120, 720); TestFrame(); TestFrame(); ClickHeader("Walls");
        Verify(wallGridMode, "grid controls remain reachable at the minimum window size");
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        GridWallGpuChecks.Run(assets);
        Console.WriteLine("Dungeon interaction checks passed: grid tab, continuous paint, cave/masonry, carve/erase, room outlines, existing walls, undo/redo, save/load, minimap, cameras and Runtime.");
    }
}
