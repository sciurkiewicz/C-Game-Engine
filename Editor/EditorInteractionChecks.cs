using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    // Raylib 6.0 automation input events exercise the actual input loop and buttons.
    // Event values: https://github.com/raysan5/raylib/blob/6.0/src/rcore.c
    static void Input(uint type, int a = 0, int b = 0)
    {
        var ev = new AutomationEvent { Type = type };
        var data = MemoryMarshal.Cast<AutomationEvent, int>(MemoryMarshal.CreateSpan(ref ev, 1));
        data[2] = a; data[3] = b;
        PlayAutomationEvent(ev);
    }
    void TestFrame()
    {
        ResizeCanvas();
        if (pendingAction == null) Update(1f / 60);
        Render();
    }
    void Click(int x, int y)
    {
        Input(7, x, y); Input(6, 0); TestFrame();
        Input(7, x, y); Input(5, 0); TestFrame();
    }
    void Press(KeyboardKey key)
    {
        Input(2, (int)key); TestFrame(); Input(1, (int)key); TestFrame();
    }
    void ClickHeader(string name)
    {
        if (name == "New") { Click(Left + 70, 23); return; }
        if (Enum.TryParse<EditTool>(name, out var editTool)) { Click(16 + (int)editTool * (ToolbarToolWidth + 2) + ToolbarToolWidth / 2, 60); return; }
        int start = 16 + Enum.GetValues<EditTool>().Length * (ToolbarToolWidth + 2);
        int offset = name switch { "Undo" => 40, "Redo" => 112, "Save" => 199, "Load" => 275, _ => throw new ArgumentException(name) };
        Click(start + offset, 60);
    }
    static void Verify(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Editor interaction check failed: " + message);
    }
    public void RunInteractionChecks()
    {
        SetWindowSize(1440, 900);
        document.New();
        document.Map.Objects.AddRange(MapData.CreateDemo(assets.Sprites.Keys).Objects);
        focus = Vector3.Zero; camera.Snap(focus);
        TestFrame();
        ClickHeader("Save");
        Verify(File.Exists(mapPath) && !document.Dirty, "Save button");
        int count = document.Map.Objects.Count;
        Click(220, 150); // Rocks category.
        Click(65, 235); // First rock.
        Verify(tool == EditTool.Place && chosenAsset.Contains("rocks/"), "asset palette selection");
        Click(960, 625);
        Verify(document.Map.Objects.Count == count + 1 && tool == EditTool.Place, "map click places an object and keeps the Place tab");
        var placed = document.Map.Objects[^1];
        string id = placed.Id;
        ClickHeader("Select");
        var screen = GetWorldToScreenEx(renderer.GetSpriteQuad(camera.View, placed.Asset, placed.Position, placed.Height).Center, camera.View,
            (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        int sx = (int)screen.X, sy = (int)screen.Y;
        Click(sx, sy);
        Verify(selectedId == id && tool == EditTool.Select, "ray picking selects the rendered sprite and keeps the Select tab");
        Vector3 before = placed.Position;
        Input(7, sx, sy); Input(6, 0); TestFrame();
        Input(7, sx - 80, sy + 30); TestFrame();
        Input(5, 0); TestFrame();
        Verify(Vector3.Distance(Selection!.Position, before) > .2f, "drag moves selected object");
        float height = Selection.Height;
        Click(1392, 316);
        Verify(Selection.Height > height, "height inspector");
        Click(1250, 398);
        Verify(document.Map.Objects.Count == count + 2, "duplicate button");
        Click(1360, 398);
        Verify(document.Map.Objects.Count == count + 1, "delete button");
        ClickHeader("Undo");
        Verify(document.Map.Objects.Count == count + 2, "undo deletion");
        ClickHeader("Redo");
        Verify(document.Map.Objects.Count == count + 1, "redo deletion");
        ClickHeader("Ground");
        Click(180, 220);
        string tiles = string.Join(',', document.Map.Tiles);
        Click(830, 650);
        Verify(string.Join(',', document.Map.Tiles) != tiles && tool == EditTool.Ground, "painting keeps the Ground tab");
        ClickHeader("Player");
        Click(690, 620);
        Verify(document.Map.Player.Position != Vector3.Zero && tool == EditTool.Player, "setting the spawn keeps the Player tab");
        Vector3 spawn = document.Map.Player.Position;
        ClickHeader("NPC");
        Click(780, 620);
        Verify(document.Map.Npcs.Count == 1 && Selection is NpcData && tool == EditTool.NPC, "placing an NPC keeps the NPC tab");
        Click(GetScreenWidth() - Right + 125, 260);
        Verify(dialogueNpcId == Selection!.Id, "NPC tab opens the placed actor's dialogue");
        Press(KeyboardKey.Escape); Verify(tool == EditTool.NPC && dialogueNpcId == null, "closing dialogue keeps the NPC tab");
        Click(780, 620);
        Verify(document.Map.Npcs.Count == 2 && tool == EditTool.NPC, "next map click places another NPC");
        ClickHeader("Undo"); Verify(document.Map.Npcs.Count == 1 && tool == EditTool.NPC, "undo repeated NPC placement keeps the tab");
        ClickHeader("Select");
        var npc = document.Map.Npcs.Single();
        string npcId = npc.Id; selectedId = npcId;
        Click(1230, 437); // NPC name.
        Verify(textEdit != null, "NPC name field opens text editor");
        AppendEdit("Strażnik Żółw");
        Press(KeyboardKey.Left); Press(KeyboardKey.Backspace); AppendEdit("ł");
        Verify(textEdit!.Value == "Strażnik Żółw", "cursor navigation and insertion inside Polish text");
        Press(KeyboardKey.Enter);
        Verify(npc.Name == "Strażnik Żółw" && textEdit == null, "NPC name text applies");
        Click(1370, 437); // Dialogue tree.
        Verify(dialogueNpcId == npc.Id, "dialogue button opens graphical tree");
        var greeting = npc.Dialogue.Node(npc.Dialogue.Node(npc.Dialogue.StartId).Next.Single());
        var greetingRect = LayoutTree(npc.Dialogue)[greeting.Id];
        Click((int)greetingRect.X + 50, (int)greetingRect.Y + 30);
        Verify(dialogueNodeId == greeting.Id, "clicking a tree node selects it");
        Click(GetScreenWidth() - 230, 200);
        Verify(textEdit != null, "node text editor");
        AppendEdit("Witaj, podróżniku!\nDokąd zmierzasz?");
        Press(KeyboardKey.Five); // No tool shortcuts while typing.
        Verify(dialogueNpcId != null && tool == EditTool.Select, "text entry captures editor shortcuts");
        Click(GetScreenWidth() / 2 - 220, GetScreenHeight() / 2 + 205);
        Verify(greeting.Text.StartsWith("Witaj, podróżniku!") && textEdit == null, "apply dialogue text");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-tree.png"));
        int nodeCount = npc.Dialogue.Nodes.Count;
        Click(GetScreenWidth() - 200, GetScreenHeight() - 230);
        Verify(npc.Dialogue.Nodes.Count == nodeCount + 1 && DialogueNode?.Kind == DialogueKind.Player, "add a player reply from NPC node");
        Click(GetScreenWidth() - 200, GetScreenHeight() - 188);
        Verify(npc.Dialogue.Nodes.Count == nodeCount, "delete a dialogue branch");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Z); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        npc = document.Map.Npcs.Single();
        Verify(npc.Dialogue.Nodes.Count == nodeCount + 1, "undo restores dialogue branch");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Y); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        npc = document.Map.Npcs.Single();
        Verify(npc.Dialogue.Nodes.Count == nodeCount, "redo removes dialogue branch");
        Press(KeyboardKey.Escape);
        Verify(dialogueNpcId == null, "Escape closes dialogue editor");
        Duplicate();
        Verify(document.Map.Npcs.Count == 2 && System.Text.Json.JsonSerializer.Serialize(document.Map.Npcs[^1].Dialogue) == System.Text.Json.JsonSerializer.Serialize(npc.Dialogue), "duplicating an NPC copies its tree");
        Delete();
        npc = document.Map.Npcs.Single();
        // Put the NPC near the spawn to test conversation input, without depending on camera projection.
        ChangePosition(npc, spawn.X + 1, spawn.Z);
        Click(1360, 60);
        Verify(play != null, "play button");
        Input(2, (int)KeyboardKey.D);
        for (int i = 0; i < 10; i++) TestFrame();
        Input(1, (int)KeyboardKey.D); TestFrame();
        Verify(Vector3.Distance(play!.Player, spawn) > .1f, "playtest movement");
        Press(KeyboardKey.E);
        Verify(play.Dialogue.Active && play.Dialogue.Speaker?.Id == npcId, "E opens a nearby NPC conversation in playtest");
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-playtest.png"));
        var talkingPosition = play.Player;
        Input(2, (int)KeyboardKey.D);
        for (int i = 0; i < 5; i++) TestFrame();
        Input(1, (int)KeyboardKey.D); TestFrame();
        Verify(play.Player == talkingPosition, "dialogue stops player movement");
        Press(KeyboardKey.One);
        Verify(play.Dialogue.Active && play.Dialogue.Current?.Text.StartsWith("To Wildwood") == true, "keyboard reply follows branch");
        Press(KeyboardKey.Escape);
        Verify(play != null && !play.Dialogue.Active, "Escape closes conversation and keeps playtest running");
        Input(2, (int)KeyboardKey.F5); TestFrame(); Input(1, (int)KeyboardKey.F5); TestFrame();
        Verify(play == null && document.Map.Player.Position == spawn, "F5 returns without changing spawn");
        ClickHeader("Save");
        string saved = document.Map.Serialize();
        Verify(MapData.Load(mapPath).Serialize() == saved, "saved map matches edited scene");
        ClickHeader("New");
        Verify(creatingMap && document.Map.Serialize() == saved, "New opens size selection without changing the current map");
        int nx = GetScreenWidth() / 2 - 270, ny = GetScreenHeight() / 2 - 170;
        Click(nx + 70, ny + 226); // 32 x 32 preset.
        for (int i = 0; i < 4; i++) Click(nx + 302, ny + 151); // Depth 28.
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "new-map-size.png"));
        Click(nx + 130, ny + 300);
        Verify(document.Map.Objects.Count == 0 && document.Map.Npcs.Count == 0 && document.Map.Width == 32 && document.Map.Depth == 28, "new map uses selected rectangular size");
        ClickHeader("Load");
        Verify(pendingAction == "load", "unsaved work confirmation");
        Click(GetScreenWidth() / 2 - 230 + 215, GetScreenHeight() / 2 - 90 + 132);
        Verify(document.Map.Serialize() == saved && pendingAction == null, "discard and reload saved map");
        ClickHeader("New");
        Click(nx + 470, ny + 226); // 2048 x 2048 preset.
        Verify(widthInput == "2048" && depthInput == "2048" && ValidDimensions, "maximum-size preset and four-digit fields");
        Click(nx + 130, ny + 300);
        Verify(document.Map.Width == 2048 && document.Map.Depth == 2048 && document.Map.Tiles.Length == 4_194_304, "create maximum-sized map through the UI");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "large-map.png"));
        ClickHeader("Ground");
        Click(830, 650);
        Verify(document.Map.Tiles.Contains(chosenTerrain), "paint on maximum-sized map");
        document.Map.Save(Path.Combine(ProjectPaths.Root, "Editor", "obj", "large-interaction-test.json"));
        Click(1360, 60);
        Verify(play?.Map.Width == 2048 && play.Map.Tiles.Length == 4_194_304, "playtest maximum-sized map");
        Press(KeyboardKey.F5);
        Complete("load");
        RunLaunchOptionsInteractionChecks();
        RunTerrainInteractionChecks();
        TerrainBlendChecks.Run(assets);
        SpriteWaterChecks.Run(assets);
        RunInventoryInteractionChecks();
        RunDialogueLinkInteractionChecks();
        RunDialogueSequenceInteractionChecks();
        RunMonsterInteractionChecks();
        RunWorldFeatureInteractionChecks();
        WorldFeatureGpuChecks.Run(assets);
        SetWindowSize(1120, 720); TestFrame(); TestFrame();
        Verify(canvas.Texture.Width == 608 && canvas.Texture.Height == 600, "resizable viewport");
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        status = "Interaction checks passed: objects, terrain, 3D water, 2048 maps, NPC/dialogues, items/chests, monsters/combat, save/load and playtest.";
        Render();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "editor-smoke.png"));
        Console.WriteLine(status);
    }
}
