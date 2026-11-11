using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool wallGridMode = true, gridWallErase, gridWallRectangle, gridWallPainting;
    int gridWallRadius;
    float gridWallRoughness = .75f;
    (int X, int Z)? gridWallLast, gridWallStart;
    GridWallCell GridWallSettings() => new() { Style = wallDraft.Style, Height = Math.Clamp(wallDraft.Height, .5f, 12),
        Roughness = gridWallRoughness, Solid = wallDraft.Solid, FadeWhenBlocking = wallDraft.FadeWhenBlocking };
    void EndGridWallStroke()
    { gridWallPainting = false; gridWallStart = gridWallLast = null; }
    void SetWallGridMode(bool enabled)
    {
        EndEditorGesture(); ResetWallDraft(); wallPicking = false; wallGridMode = enabled;
        if (enabled) wallDraft.Height = Math.Clamp(wallDraft.Height, .5f, 12);
        status = enabled ? "Lochy: maluj ściany LMB po kratkach. Shift+LMB wymazuje przejścia. Puste kratki są korytarzami." : "Ściany przez punkty: klikaj, Enter zapisuje.";
    }
    bool ApplyGridWallCells(IEnumerable<(int X, int Z)> points, bool erase, bool checkpoint)
    {
        try { return document.PaintWallCells(points, GridWallSettings(), erase, checkpoint); }
        catch (InvalidDataException ex) { status = ex.Message; return false; }
    }
    bool UpdateGridWallTool()
    {
        if (!IsMouseButtonDown(MouseButton.Left))
        {
            if (gridWallRectangle && gridWallStart is { } first && gridWallLast is { } last)
                ApplyGridWallCells(GridWallGeometry.Rectangle(first, last), false, true);
            EndGridWallStroke(); gesture = false; return true;
        }
        if (!InViewport || MouseTerrainPoint is not { } cursor || !document.Map.Contains(cursor.X, cursor.Z)) return true;
        var cell = GridWallGeometry.Cell(document.Map, cursor);
        if (IsMouseButtonPressed(MouseButton.Left))
        { gridWallPainting = false; gridWallStart = gridWallLast = cell; }
        if (gridWallStart == null) return true;
        gesture = true;
        if (!gridWallRectangle && (!gridWallPainting || gridWallLast != cell))
        {
            bool erase = gridWallErase || IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
            if (ApplyGridWallCells(GridWallGeometry.Stroke(gridWallLast ?? cell, cell, gridWallRadius), erase, !gridWallPainting))
            {
                gridWallPainting = true;
                status = erase ? "Wykute przejście. Ctrl+Z cofa całe pociągnięcie." : "Ściany lochu namalowane. Shift+LMB wykuwa korytarz; Ctrl+Z cofa pociągnięcie.";
            }
        }
        gridWallLast = cell;
        return true;
    }
    void DrawGridWallPalette()
    {
        if (Button("Maluj ściany", 16, 146, 218, !gridWallErase && !gridWallRectangle, play == null)) { EndEditorGesture(); gridWallErase = gridWallRectangle = false; }
        if (Button("Wymazuj / kuj korytarz", 16, 188, 218, gridWallErase, play == null)) { EndEditorGesture(); gridWallErase = true; gridWallRectangle = false; }
        Label("MATERIAŁ", 16, 242, 12, Muted);
        foreach (var style in Enum.GetValues<WallStyle>())
            if (Button(WallStyleName(style), 16, 266 + (int)style * 38, 218, wallDraft.Style == style, play == null)) wallDraft.Style = style;
        if (Button("Obrys pokoju: przeciągnij", 16, 388, 218, gridWallRectangle, play == null)) { EndEditorGesture(); gridWallRectangle = true; gridWallErase = false; }
        Label("SIATKA LOCHU / 1 m", 16, 448, 14, Accent);
        Label("LMB + ruch: ciągła ściana", 16, 482, 13, Text);
        Label("Shift + LMB: wykuj przejście", 16, 510, 12, Text);
        Label("Puste kratki tworzą korytarze.", 16, 550, 12, Muted);
        Label("Skała ma nierówne boki i grzbiet.", 16, 578, 12, Muted);
        Label("Mur i drewno pozostają równe.", 16, 606, 12, Muted);
        Label("Punkty: dotychczasowe ściany.", 16, 646, 12, Accent);
    }
    void DrawGridWallInspector(int x)
    {
        Label(gridWallRectangle ? "Obrys pokoju" : gridWallErase ? "Wykuwanie korytarza" : "Ściany lochu", x, 147, 16, Accent);
        Stepper("Wysokość (m)", wallDraft.Height, x, 184, v => wallDraft.Height = Math.Clamp(v, .5f, 12), .25f);
        Stepper("Nierówność skały", gridWallRoughness, x, 244, v => gridWallRoughness = Math.Clamp(v, 0, 1), .05f);
        Stepper("Pędzel (kratek)", gridWallRadius * 2 + 1, x, 304, v => gridWallRadius = Math.Clamp(((int)v - 1) / 2, 0, 3), 2);
        if (Button("Kolizja: " + (wallDraft.Solid ? "WŁ." : "WYŁ."), x, 364, 218, wallDraft.Solid)) wallDraft.Solid = !wallDraft.Solid;
        if (Button("Odsłanianie gracza: " + (wallDraft.FadeWhenBlocking ? "WŁ." : "WYŁ."), x, 406, 218, wallDraft.FadeWhenBlocking)) wallDraft.FadeWhenBlocking = !wallDraft.FadeWhenBlocking;
        Label($"Ściany: {document.Map.WallCells.Count} kratek", x, 468, 14, Text);
        Label("Każda kratka to pełny blok ściany.", x, 502, 12, Muted);
        Label("Zostaw puste kratki na przejścia.", x, 530, 12, Muted);
        Label("Mur: regularne lochy / labirynt.", x, 566, 12, Accent);
        Label("Skała: naturalne podziemia.", x, 592, 12, Accent);
        Label("Ctrl+Z: cofnij całe pociągnięcie", x, 630, 12, Muted);
        Label("M: minimapa / G: siatka", x, 656, 12, Muted);
    }
    void DrawGridWallPreview(Camera3D view)
    {
        if (!InViewport || ModalOpen || MouseTerrainPoint is not { } cursor || !document.Map.Contains(cursor.X, cursor.Z)) return;
        var cell = GridWallGeometry.Cell(document.Map, cursor);
        var points = gridWallRectangle && gridWallStart is { } start
            ? GridWallGeometry.Rectangle(start, cell)
            : GridWallGeometry.Stroke(cell, cell, gridWallRadius);
        bool erase = gridWallErase || IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        var color = erase ? new Color(255, 145, 104, 255) : Accent;
        var settings = GridWallSettings();
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthTest(); Rlgl.DisableDepthMask();
        foreach (var p in points.Where(p => p.X >= 0 && p.X < document.Map.Width && p.Z >= 0 && p.Z < document.Map.Depth).Take(2048))
        {
            var tile = settings.Copy(p.X, p.Z);
            renderer.DrawWallOutline(document.Map, tile.AsWall(document.Map), color);
        }
        Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); Rlgl.EnableDepthTest();
    }
}
