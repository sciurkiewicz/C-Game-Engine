using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunLandscapeInteractionChecks()
    {
        SetWindowSize(1440, 900); EndEditorGesture(); ResetRoadDraft();
        document.New(48, 40); tool = EditTool.Select; groundMode = GroundMode.Paint;
        terrainStamp = terrainErosion = objectBrush = false; selectedId = null; selectedIds.Clear();
        preferences.ShowMinimap = false; preferences.AutosaveSeconds = 0;
        assetSearch.Value = ""; assetSearch.Focused = false; sceneSearch.Value = ""; sceneFilter = "All";
        document.Map.Player.X = document.Map.Player.Z = -16; document.Map.Environment.Audio = false;
        var vegetation = assets.Sprites.Values.First(s => s.Id.StartsWith("objects/vegetation/"));
        var rock = assets.Sprites.Values.First(s => s.Id.Contains("/rocks/"));
        document.Place(vegetation.Id, new(0, 0, 3), 1, true);
        document.Place(rock.Id, new(0, 0, 3), 1, true);
        document.PlaceNpc(null, new(0, 0, 3));
        focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame(); Save();
        int ix = GetScreenWidth() - Right + 18;
        Vector2 Screen(float x, float z) => GetWorldToScreenEx(document.Map.GroundPosition(x, z), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        void GroundClick(float x, float z) { var p = Screen(x, z); Click((int)p.X, (int)p.Y); }
        ClickHeader("Ground"); Click(125, 145);
        Verify(groundMode == GroundMode.Roads, "Ground opens road mode");
        roadWidth = 3; roadSoftness = .75f; roadMaterial = "dirt"; roadClearVegetation = true;
        Click(ix + 201, 214); Click(ix + 201, 274);
        Verify(roadWidth == 3.5f && roadSoftness == 1, "road width and soft-edge controls");
        string beforeRoad = document.Map.Serialize();
        GroundClick(-8, -4); GroundClick(0, 3); GroundClick(8, -4);
        Verify(roadPoints.Count == 3 && document.Map.Serialize() == beforeRoad, "road points preview without changing the document");
        var handle = Screen(0, 3); var moved = Screen(0, 4);
        Input(7, (int)handle.X, (int)handle.Y); Input(6, 0); TestFrame();
        Input(7, (int)moved.X, (int)moved.Y); TestFrame(); Input(5, 0); TestFrame();
        Verify(Math.Abs(roadPoints[1].Z - 4) < .1f, "drag a road control point");
        Press(KeyboardKey.Backspace); Verify(roadPoints.Count == 2, "Backspace removes the last road point");
        GroundClick(8, -4);
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "landscape-road-preview.png"));
        Press(KeyboardKey.Enter);
        Verify(document.Map.Roads.Count == 1 && roadPoints.Count == 0 && document.Map.Objects.Count == 1 && document.Map.Npcs.Count == 1,
            "Enter applies a road and only clears vegetation");
        string roadId = document.Map.Roads[0].Id;
        string withRoad = document.Map.Serialize();
        ClickHeader("Undo"); Verify(document.Map.Serialize() == beforeRoad, "one undo restores road creation and cleared vegetation");
        ClickHeader("Redo"); Verify(document.Map.Serialize() == withRoad, "redo restores the same road");
        Click(ix + 50, 403); Verify(roadListOpen, "saved road list");
        Press(KeyboardKey.Six); Verify(tool == EditTool.Ground && roadListOpen, "road list captures tool shortcuts");
        var list = RoadListBounds;
        Click((int)list.X + 100, (int)list.Y + 120);
        Verify(!roadListOpen && editingRoadId == roadId && roadPoints.Count == 3, "open saved control points for editing");
        Click(ix + 201, 214); Press(KeyboardKey.Enter);
        Verify(document.Map.Roads.Count == 1 && document.Map.Roads[0].Width == 4 && document.Map.Roads[0].Id == roadId, "editing replaces the road while preserving its ID");
        Click(ix + 50, 403); Click((int)list.X + 636, (int)list.Y + 120);
        Verify(textEdit != null, "rename a road"); AppendEdit("Leśna ścieżka"); Press(KeyboardKey.Enter); Press(KeyboardKey.Escape);
        OpenSceneSearch(); sceneSearch.Append("ścieżka"); Press(KeyboardKey.Enter);
        Verify(tool == EditTool.Ground && groundMode == GroundMode.Roads && editingRoadId == roadId, "scene search selects an editable road");
        Press(KeyboardKey.Escape); Verify(roadPoints.Count == 0 && document.Map.Roads.Count == 1, "Escape cancels the draft while preserving the applied road");
        preferences.ShowMinimap = true; TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "landscape-road.png"));
        Save(); Verify(MapData.Load(mapPath).Roads[0].Name == "Leśna ścieżka", "roads save to the map file");
        string beforePlay = document.Map.Serialize(); Press(KeyboardKey.F5);
        Verify(play != null && play.Map.Roads.Count == 1 && play.Map.Roads[0].Id == roadId, "Play includes the road");
        Press(KeyboardKey.F5); Verify(document.Map.Serialize() == beforePlay, "road preview preserves the editor map");
        EditRoad(document.Map.Roads[0]); Click(ix + 165, 403);
        Verify(document.Map.Roads.Count == 0, "delete road button");
        ClickHeader("Undo"); Verify(document.Map.Roads.Count == 1, "undo road deletion");
        ResetRoadDraft(); Click(200, 145);
        Verify(groundMode == GroundMode.Biomes, "Ground opens biome mode");
        biomeKind = BiomeKind.Forest; CurrentBiomeSettings.Density = .08f; CurrentBiomeSettings.Radius = 6;
        Click(ix + 201, 274); float forestDensity = CurrentBiomeSettings.Density;
        Click(100, 233); Verify(biomeKind == BiomeKind.Meadow && CurrentBiomeSettings.Density != forestDensity, "biomes have separate density settings");
        Click(100, 191); Verify(biomeKind == BiomeKind.Forest && CurrentBiomeSettings.Density == forestDensity, "forest settings survive switching presets");
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        string beforeBiome = document.Map.Serialize();
        var spot = Screen(-6, 8);
        Input(7, (int)spot.X, (int)spot.Y); Input(6, 0); TestFrame();
        int added = document.Map.Objects.Count; string dab = document.Map.Serialize();
        Verify(added > 1 && dab != beforeBiome, "biome paints materials and adds available graphics");
        for (int i = 0; i < 8; i++) { Input(7, (int)spot.X, (int)spot.Y); TestFrame(); }
        Verify(document.Map.Serialize() == dab, "stationary biome brushes do not add objects each frame");
        var second = Screen(-9, 8); Input(7, (int)second.X, (int)second.Y); TestFrame(); Input(5, 0); TestFrame();
        string stroke = document.Map.Serialize();
        ClickHeader("Undo"); Verify(document.Map.Serialize() == beforeBiome, "one undo removes the full biome stroke");
        ClickHeader("Redo"); Verify(document.Map.Serialize() == stroke, "biome redo restores the same objects and materials");
        focus = document.Map.GroundPosition(-6, 8); camera.Snap(focus); TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "landscape-biome.png"));
        document.Checkpoint(); document.StampTerrain(new(7, 0, 6), 9, 8, TerrainStampShape.Mountain, 7);
        focus = document.Map.GroundPosition(7, 6); camera.Snap(focus); TestFrame();
        ClickHeader("Sculpt"); Click(100, 473);
        Verify(terrainErosion, "Sculpt opens erosion mode");
        erosionKind = ErosionKind.Hydraulic; erosionRadius = 10; erosionIterations = 600; erosionStrength = .7f;
        string beforeErosion = document.Map.Serialize(); spot = Screen(7, 6);
        Input(7, (int)spot.X, (int)spot.Y); Input(6, 0); TestFrame();
        string erosion = document.Map.Serialize(); Verify(erosion != beforeErosion, "click erodes the mountain");
        for (int i = 0; i < 8; i++) { Input(7, (int)spot.X, (int)spot.Y); TestFrame(); }
        Verify(document.Map.Serialize() == erosion, "held erosion clicks do not repeat each frame");
        Input(5, 0); TestFrame();
        ClickHeader("Undo"); Verify(document.Map.Serialize() == beforeErosion, "one undo restores the pre-erosion mountain");
        ClickHeader("Redo"); Verify(document.Map.Serialize() == erosion, "erosion redo restores its deposits and channels");
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "landscape-erosion.png"));
        Click(100, 251); Verify(erosionKind == ErosionKind.Talus, "talus erosion mode");
        beforeErosion = document.Map.Serialize(); GroundClick(7, 6);
        Verify(document.Map.Serialize() != beforeErosion, "talus moves loose soil downhill");
        SetWindowSize(1120, 720); TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "landscape-small.png"));
        Save(); Verify(MapData.Load(mapPath).Serialize() == document.Map.Serialize(), "all landscape changes survive save/load");
        SetWindowSize(1440, 900); Complete("load"); tool = EditTool.Select; groundMode = GroundMode.Paint;
        terrainErosion = terrainStamp = false; preferences.ShowMinimap = false;
        assetSearch.Value = ""; sceneSearch.Value = ""; sceneSearch.Focused = false; TestFrame();
        Console.WriteLine("Landscape interaction checks passed: road tabs/points/dragging/softness/list/names/search/delete, vegetation clearing, Play, biome strokes/settings, hydraulic/talus erosion, undo/redo, minimap, save/load and small window.");
    }
}
