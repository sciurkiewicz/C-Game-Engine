using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool terrainStamp, objectBrush, brushSettingsOpen;
    TerrainStampShape stampShape = TerrainStampShape.Mountain;
    float stampRadius = 8, stampHeight = 4, stampRoughness = .3f;
    int stampSeed = 1, scatterSeed = 1;
    float scatterRadius = 3, scatterSpacing = 1.2f, scatterVariation = .2f;
    int scatterCount = 6;
    readonly HashSet<string> scatterAssets = [];
    Vector3 lastScatter;
    Rectangle BrushSettingsBounds => new(GetScreenWidth() / 2 - 300, GetScreenHeight() / 2 - 250, 600, 500);
    void DrawSculptPalette()
    {
        if (terrainErosion) { DrawErosionPalette(); return; }
        if (!terrainStamp)
        {
            int sy = 146;
            foreach (var mode in Enum.GetValues<SculptMode>())
            {
                if (Button(mode.ToString(), 16, sy, 218, sculptMode == mode, play == null)) sculptMode = mode;
                sy += 42;
            }
            Label("Hold LMB to sculpt the surface.", 16, 328, 13, Muted);
            Label("Shift swaps Raise / Lower.", 16, 350, 13, Muted);
            Label("Elevation step: 0.25 m.", 16, 382, 13, Accent);
            if (Button("Terrain stamps...", 16, 416, 218, false, play == null)) { EndEditorGesture(); terrainStamp = true; }
            if (Button("Terrain erosion...", 16, 458, 218, false, play == null)) { EndEditorGesture(); terrainStamp = false; terrainErosion = true; }
            return;
        }
        if (Button("Back to sculpt brushes", 16, 146, 218, false, play == null)) { EndEditorGesture(); terrainStamp = false; }
        int y = 194;
        foreach (var shape in Enum.GetValues<TerrainStampShape>())
        {
            if (Button(shape.ToString(), 16, y, 218, shape == stampShape, play == null)) stampShape = shape;
            y += 42;
        }
        Label("Click once to stamp a landform.", 16, 380, 13, Muted);
        Label("Repeat to build mountain ranges.", 16, 402, 13, Muted);
        Label("Shift: invert height / dig.", 16, 434, 13, Accent);
        Label("Each click varies the shape.", 16, 458, 13, Muted);
    }
    bool UpdateObjectBrush(Vector3 ground)
    {
        if (tool != EditTool.Place || !objectBrush || activePrefab != null || chosenAsset.Length == 0) return false;
        if (!IsMouseButtonDown(MouseButton.Left) || (!gesture && !IsMouseButtonPressed(MouseButton.Left))) return true;
        if (!gesture)
        { document.Checkpoint(); gesture = true; lastScatter = ground; ScatterAt(ground); }
        else
        {
            float step = Math.Max(.5f, scatterRadius * .45f);
            var delta = new Vector2(ground.X - lastScatter.X, ground.Z - lastScatter.Z);
            int dabs = Math.Min(64, (int)(delta.Length() / step));
            if (dabs > 0)
            {
                var direction = Vector2.Normalize(delta);
                for (int i = 0; i < dabs; i++)
                {
                    lastScatter = document.Map.GroundPosition(lastScatter.X + direction.X * step, lastScatter.Z + direction.Y * step);
                    ScatterAt(lastScatter);
                }
            }
        }
        return true;
    }
    void ScatterAt(Vector3 point)
    {
        var ids = scatterAssets.Count > 0 ? scatterAssets : new HashSet<string> { chosenAsset };
        float relativeScale = assets.Sprites.TryGetValue(chosenAsset, out var primary) ? placeHeight / primary.DefaultHeight : 1;
        var choices = ids.Where(id => assets.Sprites.ContainsKey(id)).Order().Select(id =>
            new ScatterAsset(id, Math.Clamp(assets.Sprites[id].DefaultHeight * relativeScale, SceneObject.MinHeight, SceneObject.MaxHeight), placeSolid)).ToArray();
        if (choices.Length == 0) return;
        var placed = ObjectScatter.Place(document.Map, point, scatterRadius, scatterCount, scatterSpacing, scatterVariation, choices, scatterSeed++);
        if (placed.Length > 0)
        { selectedIds.Clear(); selectedId = placed[^1].Id; status = $"Object brush: {placed.Length} placed. Ctrl+Z undoes the whole stroke."; }
        else status = document.Map.Objects.Count >= 10000 ? "Object limit reached (10000)." : "Object brush: no space; reduce the minimum spacing.";
    }
    void ChoosePaletteAsset(SpriteAsset asset)
    {
        if (objectBrush && asset.Id != ChestData.DefaultAsset && (IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl)))
        {
            if (scatterAssets.Count == 0 && chosenAsset.Length > 0) scatterAssets.Add(chosenAsset);
            if (!scatterAssets.Add(asset.Id)) scatterAssets.Remove(asset.Id);
            status = $"Object brush uses {Math.Max(1, scatterAssets.Count)} graphics. Ctrl+click to mix more.";
            return;
        }
        Choose(asset);
        scatterAssets.Clear();
    }
    void OpenBrushSettings()
    { EndEditorGesture(); assetSearch.Focused = false; brushSettingsOpen = true; }
    void DrawBrushSettings()
    {
        var box = BrushSettingsBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("OBJECT BRUSH", x + 24, y + 22, 23, Accent);
        Label("Hold LMB and move to scatter objects. One stroke = one undo.", x + 24, y + 62, 14, Muted);
        void Number(string title, float value, int row, float step, float min, float max, Action<float> apply)
        {
            Label(title, x + 24, y + row + 9, 14, Text);
            if (Button("-", x + 316, y + row, 40, false, value > min, true)) apply(Math.Clamp(value - step, min, max));
            Label(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), x + 394, y + row + 9, 14, Accent);
            if (Button("+", x + 536, y + row, 40, false, value < max, true)) apply(Math.Clamp(value + step, min, max));
        }
        Number("Radius (m)", scatterRadius, 112, .5f, .5f, 32, v => scatterRadius = v);
        Number("Objects per dab", scatterCount, 166, 1, 1, 64, v => scatterCount = (int)v);
        Number("Minimum spacing (m)", scatterSpacing, 220, .25f, .25f, 16, v => scatterSpacing = v);
        Number("Size variation", scatterVariation, 274, .05f, 0, .75f, v => scatterVariation = v);
        Label("Ctrl+click graphics in the library to mix several assets.", x + 24, y + 340, 14, Text);
        Label("Deep water and steep terrain are skipped automatically.", x + 24, y + 368, 13, Muted);
        Label("Spacing applies to existing objects and the new stroke.", x + 24, y + 392, 13, Muted);
        if (Button("Done / Esc", x + 376, y + 446, 200, true, true, true)) brushSettingsOpen = false;
    }
}
