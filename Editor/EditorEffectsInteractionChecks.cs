using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunEffectsInteractionChecks()
    {
        string output = Path.Combine(ProjectPaths.Root, "Editor", "obj");
        Verify(Path.GetFullPath(mapPath).StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "effect checks use an isolated map in Editor/obj");
        Directory.CreateDirectory(output);
        SetWindowSize(1440, 900);
        play = null; document.New(32, 32); selectedId = null; selectedIds.Clear();
        tool = EditTool.Select; objectBrush = false; activePrefab = null;
        assetSearch.Value = ""; assetSearch.Focused = false; scroll = 0; category = 0;
        focus = Vector3.Zero; ApplyRuntimeSettings(); EndEditorGesture(); TestFrame();

        for (int i = 0; i < categories.Length && categories[category] != "effects"; i++) Click(220, 150);
        Verify(categories[category] == "effects", "the category buttons open the Effects palette");

        string[] effectIds = ["embers", "fireflies", "heat_haze", "smoke", "steam"];
        var placedIds = new Dictionary<string, string>();
        Vector2 OnScreen(Vector3 point) => GetWorldToScreenEx(point, camera.View, (int)viewport.Width, (int)viewport.Height)
            + new Vector2(viewport.X, viewport.Y);
        Vector2 ObjectCenter(SceneObject item) => OnScreen(renderer.GetSpriteQuad(camera.View, item.Asset,
            document.Map.GroundPosition(item), item.Height).Center);

        for (int i = 0; i < effectIds.Length; i++)
        {
            string id = "objects/effects/" + effectIds[i] + ".png";
            Verify(assets.Sprites.TryGetValue(id, out var asset) && !asset.DefaultSolid,
                "built-in effect " + effectIds[i] + " has a palette image and no default collision");
            Verify(WorldEffectCatalog.TryGet(id, out var definition), "the effect has an emitter definition");
            var palette = assets.Sprites.Values.Where(s => s.Id.StartsWith("objects/") && s.Category == "effects")
                .OrderBy(s => s.Id).ToArray();
            int index = Array.FindIndex(palette, s => s.Id == id);
            Verify(index >= 0, "the effect is available in the Effects category");
            Click(66 + index % 2 * 116, 270 + index / 2 * 120);
            Verify(tool == EditTool.Place && chosenAsset == id && !placeSolid && placeHeight == definition.DefaultHeight,
                "clicking the effect card selects its emitter and default size");
            var point = OnScreen(new(0, 0, -6 + i * 3));
            Verify(CheckCollisionPointRec(point, viewport), "effect placement stays inside the map viewport");
            Click((int)point.X, (int)point.Y);
            Verify(document.Map.Objects.Count == i + 1 && Selection?.Asset == id && !Selection.Solid &&
                Selection.Height == definition.DefaultHeight && tool == EditTool.Place,
                "a map click places the selected effect as a non-solid scene object");
            placedIds.Add(effectIds[i], Selection!.Id);
        }
        Verify(document.Map.Objects.Count(o => WorldEffectCatalog.IsEffect(o.Asset)) == 5,
            "all five procedural emitters can coexist on the map");

        ClickHeader("Select");
        foreach (string id in placedIds.Values)
        {
            var item = document.Map.Objects.Single(o => o.Id == id);
            var center = ObjectCenter(item); Click((int)center.X, (int)center.Y);
            Verify(selectedId == id, "ray picking selects each effect, including invisible heat distortion");
        }
        string smokeId = placedIds["smoke"];
        var smoke = document.Map.Objects.Single(o => o.Id == smokeId);
        var smokeCenter = ObjectCenter(smoke); Click((int)smokeCenter.X, (int)smokeCenter.Y);
        Vector3 originalPosition = smoke.Position;
        Input(7, (int)smokeCenter.X, (int)smokeCenter.Y); Input(6, 0); TestFrame();
        Input(7, (int)smokeCenter.X + 40, (int)smokeCenter.Y + 18); TestFrame();
        Input(5, 0); TestFrame();
        Verify(smoke.Position != originalPosition, "dragging moves the smoke emitter");
        float originalSize = smoke.Height;
        Click(GetScreenWidth() - Right + 219, 316);
        Verify(Selection?.Id == smokeId && Selection.Height > originalSize, "the size control scales a selected effect");
        float resized = Selection!.Height;
        ClickHeader("Undo");
        Verify(document.Map.Objects.Single(o => o.Id == smokeId).Height == originalSize, "undo restores the effect size");
        ClickHeader("Redo");
        Verify(document.Map.Objects.Single(o => o.Id == smokeId).Height == resized, "redo restores the resized effect");

        selectedId = smokeId; selectedIds.Clear(); TestFrame();
        Click(GetScreenWidth() - Right + 65, 398);
        Verify(document.Map.Objects.Count == 6 && Selection?.Asset == "objects/effects/smoke.png" &&
            Selection.Id != smokeId && !Selection.Solid && Selection.Height == resized,
            "Duplicate creates an independent emitter with the selected size");
        string duplicateId = Selection!.Id;
        Click(GetScreenWidth() - Right + 180, 398);
        Verify(document.Map.Objects.Count == 5 && document.Map.Objects.All(o => o.Id != duplicateId), "Delete removes an emitter");
        ClickHeader("Undo");
        Verify(document.Map.Objects.Count == 6 && document.Map.Objects.Any(o => o.Id == duplicateId), "undo restores the deleted emitter");
        ClickHeader("Redo");
        Verify(document.Map.Objects.Count == 5 && document.Map.Objects.All(o => o.Id != duplicateId), "redo deletes the same emitter");

        ClickHeader("Save"); string saved = document.Map.Serialize();
        Verify(!document.Dirty && MapData.Load(mapPath).Serialize() == saved, "effect objects and their sizes save without a new map schema");
        ClickHeader("Load");
        Verify(pendingAction == null && document.Map.Serialize() == saved, "Load restores all authored emitters");
        TestFrame(); TestFrame(); WorldRenderer.Screenshot(Path.Combine(output, "effects-editor.png"));

        Press(KeyboardKey.F5);
        Verify(play != null && play.Map.Objects.Count == 5 &&
            play.Map.Objects.All(o => WorldEffectCatalog.IsEffect(o.Asset) && !o.Solid), "Play contains all five non-solid effect objects");
        Verify(!ReferenceEquals(play!.Map.Objects[0], document.Map.Objects[0]), "Play owns independent copies of effect objects");
        for (int i = 0; i < 12; i++) TestFrame();
        WorldRenderer.Screenshot(Path.Combine(output, "effects-play.png"));
        play.Map.Objects[0].Height *= .5f;
        Verify(document.Map.Serialize() == saved, "runtime effect changes preserve the authored editor map");
        Press(KeyboardKey.F5);
        Verify(play == null && document.Map.Serialize() == saved && !document.Dirty,
            "returning from Play preserves the saved emitters and map state");
        Console.WriteLine("Effects interaction checks passed: five palette emitters, placement, picking, dragging, size, duplicate/delete, undo/redo, save/load and independent Play.");
    }
}
