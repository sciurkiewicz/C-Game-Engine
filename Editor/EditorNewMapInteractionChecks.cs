using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    void ClickNewMapMenu()
    {
        Click(GetScreenWidth() - 36, 23);
        Verify(launchMenuOpen && ModalOpen, "hamburger opens a blocking menu");
        var menu = LaunchMenuBounds; Click((int)menu.X + 130, (int)menu.Y + 25);
        Verify(creatingMap && !launchMenuOpen, "Nowa mapa opens the configuration modal");
    }
    public void RunNewMapInteractionChecks()
    {
        SetWindowSize(1440, 900); document.New(32, 32); tool = EditTool.Select; selectedId = null;
        focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame(); Save();
        string saved = document.Map.Serialize();
        Click(GetScreenWidth() - 36, 23);
        TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "new-map-menu.png"));
        ClickHeader("Monster");
        Verify(!launchMenuOpen && tool == EditTool.Select, "outside click dismisses the menu without activating the toolbar");
        Click(GetScreenWidth() - 36, 23); Click(GetScreenWidth() - 36, 23);
        Verify(!launchMenuOpen, "hamburger toggles its menu closed");
        Click(GetScreenWidth() - 36, 23); Press(KeyboardKey.F5); Press(KeyboardKey.Escape);
        Verify(!launchMenuOpen && play == null, "menu blocks play and Escape dismisses it");

        ClickNewMapMenu(); Press(KeyboardKey.Escape);
        Verify(!creatingMap && document.Map.Serialize() == saved, "cancel preserves the existing map");
        SetWindowSize(1120, 720); TestFrame(); ClickNewMapMenu();
        var box = NewMapBounds;
        Verify(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= GetScreenWidth() && box.Y + box.Height <= GetScreenHeight(),
            "new map modal fits the minimum window");
        widthInput = "0"; depthInput = "2049"; Press(KeyboardKey.Enter);
        Verify(creatingMap && !ValidDimensions && document.Map.Serialize() == saved, "invalid dimensions cannot create a map");
        Press(KeyboardKey.Escape); SetWindowSize(1440, 900); TestFrame();

        foreach (var type in Enum.GetValues<MapType>())
        {
            ClickNewMapMenu(); box = NewMapBounds; int x = (int)box.X, y = (int)box.Y;
            Press(KeyboardKey.F5); Press(KeyboardKey.Nine); ClickHeader("Monster");
            Verify(creatingMap && play == null && tool == EditTool.Select, "configuration blocks background tools and shortcuts");
            Click(x + 100 + (int)type * 194, y + 135);
            Click(x + 70, y + 349);
            for (int i = 0; i < 4; i++) Click(x + 340, y + 265);
            Verify(newMapType == type && widthInput == "32" && depthInput == "28", "configure rectangular map and type: " + type);
            if (type == MapType.Building)
            { TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "new-map-building.png")); }
            Click(x + 140, y + 465);
            Verify(!creatingMap && pendingAction == null && document.Map.Type == type && document.Map.Width == 32 && document.Map.Depth == 28,
                "create selected map type: " + type);
            Verify(document.Map.Objects.Count == 0 && document.Map.Npcs.Count == 0 && document.Map.Player.Position == Vector3.Zero,
                "new map starts empty with a central spawn");
            Verify(document.Map.AlwaysDark == (type != MapType.OpenWorld) && chosenTerrain == document.Map.Tiles[0], "new map applies the lighting rule and floor");
            Save();
            Verify(MapData.Load(mapPath).Type == type, "save persists the type: " + type);
        }

        var asset = assets.Sprites.Values.First(a => a.Id.Contains("rocks/"));
        document.Place(asset.Id, Vector3.Zero, 2, true); string dirty = document.Map.Serialize();
        ClickNewMapMenu(); box = NewMapBounds;
        Click((int)box.X + 294, (int)box.Y + 135); // Building.
        Click((int)box.X + 140, (int)box.Y + 465);
        Verify(pendingAction == "new" && document.Map.Serialize() == dirty, "creating a map asks before replacing unsaved work");
        Click(GetScreenWidth() / 2 + 150, GetScreenHeight() / 2 + 42); // Cancel confirmation.
        Verify(pendingAction == null && document.Map.Serialize() == dirty, "cancel confirmation preserves unsaved work");
        ClickNewMapMenu(); box = NewMapBounds;
        Click((int)box.X + 294, (int)box.Y + 135);
        Click((int)box.X + 140, (int)box.Y + 465);
        Click(GetScreenWidth() / 2, GetScreenHeight() / 2 + 42); // Discard.
        Verify(pendingAction == null && document.Map.Type == MapType.Building && document.Map.Objects.Count == 0, "discard creates the configured building");
        ClickHeader("Undo"); Verify(document.Map.Serialize() == dirty, "one undo restores the complete previous cave");
        ClickHeader("Redo"); Verify(document.Map.Type == MapType.Building && document.Map.Objects.Count == 0, "redo restores the building");

        Click(GetScreenWidth() - 36, 23); var menuBox = LaunchMenuBounds;
        Click((int)menuBox.X + 130, (int)menuBox.Y + 109);
        Verify(worldPage == "Environment", "hamburger keeps world tools available");
        var world = WorldToolsBounds; string environment = document.Map.Serialize();
        Click((int)world.X + 180, (int)world.Y + 155); // Lighting.
        Click((int)world.X + 180, (int)world.Y + 323); // Day/night.
        Click((int)world.X + 258, (int)world.Y + 365); // Hour +.
        Verify(document.Map.Serialize() == environment, "interior lighting, clock and day/night controls stay locked");
        Click((int)world.X + 220, (int)world.Y + 101); Click((int)world.X + 136, (int)world.Y + 489);
        Verify(document.Map.Lights.Count == 1, "point lights can be added inside a building");
        Press(KeyboardKey.Escape); Save(); Complete("load"); Press(KeyboardKey.F5);
        Verify(play?.Map.Type == MapType.Building && play.TimeOfDay == 0, "reloaded building starts play in permanent darkness");
        play!.AdvanceWorld(3600); Verify(play.TimeOfDay == 0, "building remains dark after time passes");
        TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "new-map-building-play.png"));
        Press(KeyboardKey.F5);
        ClickLaunchOptionsMenu(); Verify(launchOptions != null, "launch options remain available"); Press(KeyboardKey.Escape);
        document.New(32, 32); focus = Vector3.Zero; ApplyRuntimeSettings(); Save(); TestFrame();
        Console.WriteLine("New map interaction checks passed: hamburger, modal, types, dimensions, cancel, unsaved confirmation, undo/redo, save/load and dark interiors with lights.");
    }
}
