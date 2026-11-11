using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

enum FragmentToolMode { Off, Selecting, Ready, Pasting }

sealed partial class EditorApp
{
    FragmentToolMode fragmentMode;
    MapFragment? fragmentClipboard;
    MapRegion? fragmentSource;
    MapRegion fragmentDestination;
    Vector3? fragmentDragStart;
    Vector3 fragmentDragEnd;
    readonly FragmentPasteOptions fragmentOptions = new();
    bool FragmentToolActive => play == null && tool == EditTool.Select && fragmentMode != FragmentToolMode.Off;
    void StopFragmentTool(bool clearSource = false)
    { fragmentMode = FragmentToolMode.Off; fragmentDragStart = null; if (clearSource) fragmentSource = null; }
    void BeginFragmentSelection()
    {
        EndEditorGesture(); tool = EditTool.Select; activePrefab = null; assetSearch.Focused = false;
        selectedId = null; selectedIds.Clear(); fragmentMode = FragmentToolMode.Selecting; fragmentSource = null;
        drawingTrigger = false; drawingColliderId = null; triggerStart = triggerEnd = null; colliderPoints.Clear();
        status = "Map fragments: drag an area on the terrain. Ctrl+V pastes the copied area; Esc closes the tool.";
    }
    void CopyFragmentSelection()
    {
        if (fragmentSource is not { } region || !region.Fits(document.Map)) { status = "Select a map area first (Ctrl+Shift+C)."; return; }
        try
        {
            fragmentClipboard = MapFragment.Capture(document.Map, region); fragmentMode = FragmentToolMode.Ready;
            status = $"Copied {region.Width} x {region.Depth} tiles, {fragmentClipboard.Actors.Entities.Count()} actors, {fragmentClipboard.Roads.Count} roads, {fragmentClipboard.Walls.Count} walls. Ctrl+V: paste.";
        }
        catch (InvalidDataException ex) { status = "Cannot copy area: " + ex.Message; }
    }
    void BeginFragmentPaste(MapFragment? fragment = null)
    {
        if (fragment != null) fragmentClipboard = fragment;
        if (fragmentClipboard == null) { status = "Copy a map area first, or load a saved fragment."; return; }
        EndEditorGesture(); tool = EditTool.Select; activePrefab = null; assetSearch.Focused = false;
        fragmentMode = FragmentToolMode.Pasting; fragmentLibraryOpen = false;
        drawingTrigger = false; drawingColliderId = null; triggerStart = triggerEnd = null; colliderPoints.Clear();
        SetFragmentDestination(InViewport ? MouseTerrainPoint ?? focus : focus);
        status = "Move the fragment preview and click to paste. Repeated clicks place independent copies; Esc leaves paste mode.";
    }
    void SetFragmentDestination(Vector3 point)
    {
        if (fragmentClipboard == null) return;
        fragmentDestination = new((int)MathF.Floor(point.X + document.Map.Width / 2f - fragmentClipboard.Width / 2f),
            (int)MathF.Floor(point.Z + document.Map.Depth / 2f - fragmentClipboard.Depth / 2f), fragmentClipboard.Width, fragmentClipboard.Depth);
    }
    bool FragmentShortcuts(bool ctrl)
    {
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        if (ctrl && shift && IsKeyPressed(KeyboardKey.C)) { BeginFragmentSelection(); return true; }
        if (ctrl && IsKeyPressed(KeyboardKey.C) && FragmentToolActive) { CopyFragmentSelection(); return true; }
        if (ctrl && IsKeyPressed(KeyboardKey.V)) { BeginFragmentPaste(); return true; }
        if (FragmentToolActive && IsKeyPressed(KeyboardKey.Escape))
        {
            if (fragmentMode == FragmentToolMode.Pasting) fragmentMode = FragmentToolMode.Ready;
            else StopFragmentTool();
            fragmentDragStart = null; return true;
        }
        return false;
    }
    bool UpdateFragmentTool()
    {
        if (fragmentMode == FragmentToolMode.Off) return false;
        if (tool != EditTool.Select) { StopFragmentTool(); return false; }
        if (fragmentSource is { } selected && !selected.Fits(document.Map)) fragmentSource = null;
        var ground = InViewport ? MouseTerrainPoint : null;
        if (fragmentMode == FragmentToolMode.Pasting)
        {
            if (ground is { } cursor) SetFragmentDestination(cursor);
            if (ground != null && IsMouseButtonPressed(MouseButton.Left)) PasteFragmentAtPreview();
            return true;
        }
        if (ground is { } point && IsMouseButtonPressed(MouseButton.Left))
        { fragmentMode = FragmentToolMode.Selecting; fragmentDragStart = point; fragmentDragEnd = point; }
        if (fragmentDragStart is { } start)
        {
            if (ground is { } end && IsMouseButtonDown(MouseButton.Left)) fragmentDragEnd = end;
            fragmentSource = MapRegion.FromDrag(document.Map, start, fragmentDragEnd);
            if (!IsMouseButtonDown(MouseButton.Left)) { fragmentDragStart = null; CopyFragmentSelection(); }
        }
        return true;
    }
    void PasteFragmentAtPreview()
    {
        if (fragmentClipboard == null) return;
        try
        {
            var result = document.PasteFragment(fragmentClipboard, fragmentDestination, fragmentOptions);
            selectedIds.Clear(); foreach (var entity in result.Actors) selectedIds.Add(entity.Id); selectedId = result.Actors.FirstOrDefault()?.Id;
            status = !result.Changed ? "The destination already matches the fragment." :
                $"Pasted {fragmentClipboard.Width} x {fragmentClipboard.Depth} tiles. Ctrl+Z undoes the whole paste.";
            if (result.DisabledTriggers > 0) status += $" Disabled triggers with missing external targets: {result.DisabledTriggers}; choose their targets in World tools.";
        }
        catch (InvalidDataException ex) { status = "Cannot paste: " + ex.Message; }
    }
    bool DrawFragmentPalette()
    {
        if (!FragmentToolActive) return false;
        Label("MAP FRAGMENTS", 16, 108, 15, Accent);
        if (Button("Select area / Ctrl+Shift+C", 16, 146, 218, fragmentMode == FragmentToolMode.Selecting)) BeginFragmentSelection();
        if (Button("Copy / Ctrl+C", 16, 188, 218, false, fragmentSource != null)) CopyFragmentSelection();
        if (Button("Paste / Ctrl+V", 16, 230, 218, fragmentMode == FragmentToolMode.Pasting, fragmentClipboard != null)) BeginFragmentPaste();
        if (Button("Save fragment...", 16, 272, 218, false, fragmentClipboard != null)) SaveFragmentToLibrary();
        if (Button("Fragment library...", 16, 314, 218)) OpenFragmentLibrary();
        if (Button("Paste options...", 16, 356, 218, false, fragmentClipboard != null)) { EndEditorGesture(); fragmentOptionsOpen = true; }
        if (Button("Close tool / Esc", 16, 398, 218)) StopFragmentTool();
        Label("Drag on terrain to copy an area.", 16, 460, 12, Muted);
        Label("Selection follows whole tiles.", 16, 484, 12, Muted);
        Label("Click the preview to paste.", 16, 520, 12, Muted);
        Label("Repeat to place several copies.", 16, 544, 12, Muted);
        return true;
    }
    void DrawFragmentInspector(int x)
    {
        Label(fragmentMode == FragmentToolMode.Pasting ? "PASTE PREVIEW" : "AREA SELECTION", x, 147, 16, Accent);
        if (fragmentSource is { } region) Label($"Selected: {region.Width} x {region.Depth} tiles", x, 180, 13, Text);
        else Label("Drag a rectangle on the terrain.", x, 180, 13, Muted);
        if (fragmentClipboard is not { } fragment) return;
        Label(fragment.Name, x, 218, 15, Text, 220);
        Label($"Clipboard: {fragment.Width} x {fragment.Depth} tiles", x, 248, 13, Accent);
        Label($"Actors: {fragment.Actors.Entities.Count()}  /  Roads: {fragment.Roads.Count}", x, 276, 13, Text);
        Label($"Lights: {fragment.Lights.Count}  /  Sounds: {fragment.Sounds.Count}", x, 304, 13, Text);
        Label($"Triggers: {fragment.Triggers.Count} / Walls: {fragment.Walls.Count} + {fragment.WallCells.Count} cells", x, 332, 12, Text, 220);
        if (fragmentMode == FragmentToolMode.Pasting)
        {
            string? issue = MapDocument.FragmentPasteIssue(document.Map, fragment, fragmentDestination, fragmentOptions);
            Label(issue ?? "Ready: click the map to paste.", x, 374, 12, issue == null ? Accent : new(255, 154, 117, 255), 220);
            Label($"Height shift: {fragmentOptions.ElevationShift(fragment, document.Map):0.##} m", x, 404, 12, Muted);
        }
        else Label("Ctrl+V previews a copy at the cursor.", x, 384, 12, Muted);
    }
    static void FragmentOutline(MapData map, FragmentBounds bounds, Color color)
    {
        var corners = new Vector2[] { new(bounds.MinX, bounds.MinZ), new(bounds.MaxX, bounds.MinZ), new(bounds.MaxX, bounds.MaxZ), new(bounds.MinX, bounds.MaxZ) };
        for (int side = 0; side < 4; side++)
        {
            var a = corners[side]; var b = corners[(side + 1) % 4]; int steps = Math.Clamp((int)MathF.Ceiling(Vector2.Distance(a, b)), 1, 256);
            for (int i = 0; i < steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps); var q = Vector2.Lerp(a, b, (i + 1) / (float)steps);
                DrawLine3D(map.GroundPosition(p.X, p.Y) + Vector3.UnitY * .12f, map.GroundPosition(q.X, q.Y) + Vector3.UnitY * .12f, color);
            }
        }
    }
    void DrawFragmentPreview(Camera3D view)
    {
        if (!FragmentToolActive) return;
        if (fragmentSource is { } source && source.Fits(document.Map)) FragmentOutline(document.Map, source.WorldBounds(document.Map), Accent);
        if (fragmentMode != FragmentToolMode.Pasting || fragmentClipboard is not { } fragment) return;
        var bounds = fragmentDestination.WorldBounds(document.Map);
        bool valid = MapDocument.FragmentPasteIssue(document.Map, fragment, fragmentDestination, fragmentOptions) == null;
        var color = valid ? Accent : new Color(255, 116, 104, 255);
        float shift = fragmentOptions.ElevationShift(fragment, document.Map);
        Vector3 Point(float x, float z) => new(bounds.MinX + x,
            (fragmentOptions.Terrain ? fragment.HeightAt(x, z) + shift : document.Map.HeightAt(bounds.MinX + x, bounds.MinZ + z)) + .06f, bounds.MinZ + z);
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthTest(); Rlgl.DisableDepthMask();
        try
        {
            int step = Math.Max(1, (int)MathF.Ceiling(Math.Max(fragment.Width, fragment.Depth) / 32f));
            for (int z = 0; z < fragment.Depth; z += step)
            for (int x = 0; x < fragment.Width; x += step)
            {
                int nx = Math.Min(fragment.Width, x + step), nz = Math.Min(fragment.Depth, z + step);
                var a = Point(x, z); var b = Point(x, nz); var c = Point(nx, nz); var d = Point(nx, z);
                if (fragmentOptions.Terrain)
                {
                    var fill = new Color(color.R, color.G, color.B, (byte)22);
                    DrawTriangle3D(a, b, c, fill); DrawTriangle3D(a, c, d, fill);
                    DrawLine3D(a, b, new(color.R, color.G, color.B, (byte)85)); DrawLine3D(a, d, new(color.R, color.G, color.B, (byte)85));
                    if (fragment.WaterMask(z * fragment.Width + x) != 0 && fragment.HeightAt(x + .5f, z + .5f) + shift < document.Map.WaterLevel)
                    {
                        a.Y = b.Y = c.Y = d.Y = document.Map.WaterLevel + .08f;
                        DrawTriangle3D(a, b, c, new(80, 183, 255, 55)); DrawTriangle3D(a, c, d, new(80, 183, 255, 55));
                    }
                }
            }
            FragmentOutline(document.Map, bounds, color);
            if (fragmentOptions.Actors)
                foreach (var actor in fragment.Actors.Entities)
                    renderer.DrawSprite(view, actor.Asset, Point(actor.X, actor.Z), actor.Height,
                        actor is NpcData ? new(244, 192, 96, 255) : actor is ChestData ? new(190, 140, 70, 255) : actor is MonsterData ? new(222, 75, 86, 255) : color, 105);
            if (fragmentOptions.Roads)
                foreach (var road in fragment.Roads)
                {
                    var curve = road.Curve;
                    for (int i = 1; i < curve.Length; i++) DrawLine3D(Point(curve[i - 1].X, curve[i - 1].Y), Point(curve[i].X, curve[i].Y), color);
                }
            if (fragmentOptions.Walls)
            {
                foreach (var wall in fragment.Walls)
                    for (int i = 0; i < wall.Points.Count - (wall.Closed ? 0 : 1); i++)
                    {
                        var a = wall.Points[i]; var b = wall.Points[(i + 1) % wall.Points.Count];
                        var pa = Point(a.X, a.Z); var pb = Point(b.X, b.Z);
                        DrawLine3D(pa, pb, color); DrawLine3D(pa + Vector3.UnitY * wall.Height, pb + Vector3.UnitY * wall.Height, color);
                        DrawLine3D(pa, pa + Vector3.UnitY * wall.Height, color);
                    }
                foreach (var cell in fragment.WallCells)
                {
                    var p = Point(cell.X + .5f, cell.Z + .5f) + Vector3.UnitY * (cell.Height / 2);
                    DrawCubeWires(p, 1, cell.Height, 1, color);
                }
            }
            if (fragmentOptions.WorldFeatures)
            {
                foreach (var light in fragment.Lights) DrawSphereWires(Point(light.X, light.Z) + Vector3.UnitY * light.Height, .2f, 4, 8, color);
                foreach (var sound in fragment.Sounds) DrawSphereWires(Point(sound.X, sound.Z), .2f, 4, 8, color);
                foreach (var trigger in fragment.Triggers) DrawCubeWires(Point(trigger.Data.X, trigger.Data.Z), trigger.Data.Width, .15f, trigger.Data.Depth, color);
            }
        }
        finally { Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); Rlgl.EnableDepthTest(); }
    }
}
