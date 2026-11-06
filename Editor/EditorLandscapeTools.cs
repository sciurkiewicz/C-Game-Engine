using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

enum GroundMode { Paint, Roads, Biomes }

sealed partial class EditorApp
{
    GroundMode groundMode;
    BiomeKind biomeKind;
    readonly BiomeBrushSettings[] biomeSettings = [new(.08f, 1.8f, .25f), new(.03f, .8f, .25f), new(.06f, 1.25f, .3f)];
    readonly BiomeDefinition?[] biomeDefinitions = new BiomeDefinition?[3];
    int biomeSeed = 17, biomeScatterSeed = 1;
    Vector3 lastBiome;
    bool terrainErosion;
    ErosionKind erosionKind;
    float erosionRadius = 10, erosionStrength = .5f;
    int erosionIterations = 400, erosionSeed = 1;
    BiomeBrushSettings CurrentBiomeSettings => biomeSettings[(int)biomeKind];
    BiomeDefinition CurrentBiome => biomeDefinitions[(int)biomeKind] ??= Biomes.Create(biomeKind, assets.Terrain.Keys, assets.Sprites.Values);
    string GroundMaterial => groundMode == GroundMode.Roads ? roadMaterial : chosenTerrain;
    void SetGroundMode(GroundMode mode)
    {
        EndEditorGesture(); assetSearch.Focused = false; groundMode = mode;
        if (mode != GroundMode.Paint) { selectedId = null; selectedIds.Clear(); }
        status = mode switch
        {
            GroundMode.Roads => "Roads: click control points, drag a point to adjust it, Enter: apply, Backspace: remove point, Esc: cancel.",
            GroundMode.Biomes => "Biomes: hold LMB and move to paint materials and objects. One stroke = one undo.",
            _ => "Hold LMB to paint the selected terrain material."
        };
    }
    bool DrawGroundModes()
    {
        foreach (var mode in Enum.GetValues<GroundMode>())
            if (Button(mode == GroundMode.Paint ? "Paint" : mode.ToString(), 16 + (int)mode * 74, 130, 70, groundMode == mode, play == null)) SetGroundMode(mode);
        if (groundMode != GroundMode.Biomes) return false;
        int y = 176;
        foreach (var kind in Enum.GetValues<BiomeKind>())
        {
            if (Button(kind.ToString(), 16, y, 218, biomeKind == kind, play == null)) { EndEditorGesture(); biomeKind = kind; }
            y += 42;
        }
        Label("TERRAIN MIX", 16, 326, 12, Accent);
        int row = 352;
        foreach (var material in CurrentBiome.Terrain)
        { Label($"{Path.GetFileNameWithoutExtension(material.Id)}  {material.Weight:P0}", 16, row, 13, Text, 220); row += 26; }
        Label($"Object graphics: {CurrentBiome.Objects.Select(o => o.Id).Distinct().Count()}", 16, 450, 13, Accent);
        Label("Uses graphics from your library.", 16, 476, 12, Muted);
        Label("Water, roads and spawn stay clear.", 16, 500, 12, Muted);
        Label("Each biome keeps its own settings.", 16, 536, 12, Muted);
        return true;
    }
    void DrawBiomeInspector(int x)
    {
        var settings = CurrentBiomeSettings;
        Label("Biome: " + biomeKind, x, 147, 16, Accent);
        Stepper("Radius (m)", settings.Radius, x, 184, v => settings.Radius = Math.Clamp(v, 1, 32), 1);
        Stepper("Objects per square metre", settings.Density, x, 244, v => settings.Density = Math.Clamp(v, 0, .3f), .01f);
        Stepper("Minimum spacing (m)", settings.Spacing, x, 304, v => settings.Spacing = Math.Clamp(v, .25f, 16), .25f);
        Stepper("Size variation", settings.Variation, x, 364, v => settings.Variation = Math.Clamp(v, 0, .75f), .05f);
        Label("Hold LMB / one stroke = undo.", x, 430, 12, Muted);
    }
    void PaintBiomeAt(Vector3 point)
    {
        var settings = CurrentBiomeSettings;
        var placed = document.PaintBiome(point, settings.Radius, CurrentBiome, settings.Density, settings.Spacing, settings.Variation,
            biomeSeed + (int)biomeKind * 31, biomeScatterSeed++);
        status = $"{biomeKind}: terrain painted, {placed.Length} objects added. Ctrl+Z undoes the entire stroke.";
    }
    bool UpdateLandscapeTools(Vector3 ground)
    {
        if (tool == EditTool.Ground && groundMode == GroundMode.Roads) { UpdateRoadInput(ground); return true; }
        if (tool == EditTool.Ground && groundMode == GroundMode.Biomes)
        {
            if (!IsMouseButtonDown(MouseButton.Left) || (!gesture && !IsMouseButtonPressed(MouseButton.Left))) return true;
            if (!gesture) { document.Checkpoint(); gesture = true; lastBiome = ground; PaintBiomeAt(ground); }
            else
            {
                float step = Math.Max(.5f, CurrentBiomeSettings.Radius * .4f);
                var delta = new Vector2(ground.X - lastBiome.X, ground.Z - lastBiome.Z);
                int dabs = Math.Min(64, (int)(delta.Length() / step));
                if (dabs > 0)
                {
                    var direction = Vector2.Normalize(delta);
                    for (int i = 0; i < dabs; i++)
                    {
                        lastBiome = document.Map.GroundPosition(lastBiome.X + direction.X * step, lastBiome.Z + direction.Y * step);
                        PaintBiomeAt(lastBiome);
                    }
                }
            }
            return true;
        }
        if (tool != EditTool.Sculpt || !terrainErosion) return false;
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            int changed = document.ErodeTerrain(ground, erosionRadius, erosionStrength, erosionIterations, erosionKind, erosionSeed++);
            status = changed > 0 ? $"{erosionKind} erosion changed {changed} vertices. Ctrl+Z restores the terrain." : "No erosion: try a slope or increase rainfall / strength.";
        }
        return true;
    }
    void DrawErosionPalette()
    {
        if (Button("Back to sculpt brushes", 16, 146, 218, false, play == null)) { EndEditorGesture(); terrainErosion = false; }
        if (Button("Hydraulic erosion", 16, 194, 218, erosionKind == ErosionKind.Hydraulic, play == null)) erosionKind = ErosionKind.Hydraulic;
        if (Button("Talus / loose debris", 16, 236, 218, erosionKind == ErosionKind.Talus, play == null)) erosionKind = ErosionKind.Talus;
        Label("Click once to erode this area.", 16, 300, 13, Muted);
        Label("Water cuts channels and carries", 16, 334, 13, Muted);
        Label("sediment down the slopes.", 16, 356, 13, Muted);
        Label("Talus moves loose soil downhill.", 16, 392, 13, Muted);
        Label("Use after stamping mountains.", 16, 430, 13, Accent);
        Label("Each click is one undo step.", 16, 456, 13, Muted);
    }
    void DrawErosionInspector(int x)
    {
        Label("Erosion: " + erosionKind, x, 147, 16, Accent);
        Stepper("Radius (m)", erosionRadius, x, 184, v => erosionRadius = Math.Clamp(v, 1, 64), 1);
        Stepper("Strength", erosionStrength, x, 244, v => erosionStrength = Math.Clamp(v, .05f, 1), .05f);
        if (erosionKind == ErosionKind.Hydraulic)
            Stepper("Rain droplets", erosionIterations, x, 304, v => erosionIterations = (int)Math.Clamp(v, 50, 2000), 50);
        else
            Stepper("Settling passes", Math.Max(3, erosionIterations / 20), x, 304, v => erosionIterations = (int)Math.Clamp(v, 3, 100) * 20, 1);
        Label("Soil moves downhill into deposits.", x, 386, 12, Muted);
        Label("One click = one undo step.", x, 414, 13, Muted);
    }
}
