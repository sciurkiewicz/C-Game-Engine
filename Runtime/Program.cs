using Raylib_cs;
using static Raylib_cs.Raylib;

if (args.Contains("--monster-test"))
{
    MonsterChecks.Run();
    return;
}
if (args.Contains("--inventory-test"))
{
    InventoryChecks.Run();
    return;
}
if (args.Contains("--terrain-test"))
{
    TerrainChecks.Run();
    return;
}
if (args.Contains("--map-test"))
{
    MapSizeChecks.Run();
    return;
}
if (args.Contains("--dialogue-test"))
{
    DialogueChecks.Run();
    return;
}
if (args.Contains("--camera-test"))
{
    CameraChecks.Run();
    return;
}
if (args.Contains("--billboard-test"))
{
    BillboardChecks.Run();
    return;
}

SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
InitWindow(1280, 800, "Wildwood | Runtime");
SetWindowMinSize(800, 540);
SetExitKey(KeyboardKey.Null);
SetTargetFPS(60);
try
{
    using var assets = new WorldAssets();
    if (args.Contains("--sprite-water-test"))
    {
        SpriteWaterChecks.Run(assets);
        return;
    }
    int mapArgument = Array.IndexOf(args, "--map");
    if (mapArgument >= 0 && mapArgument + 1 >= args.Length)
        throw new ArgumentException("--map requires a JSON file path.");
    string mapPath = mapArgument >= 0 ? Path.GetFullPath(args[mapArgument + 1]) : ProjectPaths.MapFile;
    var map = File.Exists(mapPath) ? MapData.Load(mapPath) : mapArgument >= 0
        ? throw new FileNotFoundException("Map not found.", mapPath) : MapData.CreateDemo(assets.Sprites.Keys);
    var session = new PlaySession(map);
    var renderer = new WorldRenderer(assets);
    var dialogueUi = new DialogueOverlay();
    var inventoryUi = new InventoryOverlay();
    var combatUi = new CombatOverlay();
    if (args.Contains("--close")) session.Camera.Zoom(100);
    if (args.Contains("--far")) session.Camera.Zoom(-100);
    session.Camera.Snap(session.Player);
    int frames = 0;
    bool inventorySmoke = args.Contains("--inventory-smoke-test");
    bool monsterSmoke = args.Contains("--monster-smoke-test");
    bool smoke = args.Contains("--smoke-test") || args.Contains("--dialogue-smoke-test") || inventorySmoke || monsterSmoke;
    if (args.Contains("--dialogue-smoke-test") && session.Map.Npcs.FirstOrDefault() is { } smokeNpc)
        session.Dialogue.Begin(smokeNpc);
    if (inventorySmoke)
    {
        if (session.Map.Chests.FirstOrDefault() is { } chest)
        {
            if (!session.OpenChest(chest)) throw new ArgumentException("Inventory smoke test requires the first chest to be near the player spawn.");
        }
        else session.ToggleInventory();
    }
    while (!WindowShouldClose())
    {
        if (IsKeyPressed(KeyboardKey.Escape) && !session.Dialogue.Active && !session.InventoryOpen) break;
        session.Update(Math.Min(GetFrameTime(), .05f), GetMouseWheelMove());
        BeginDrawing();
        ClearBackground(new Color(38, 47, 54, 255));
        BeginMode3D(session.Camera.View);
        renderer.Draw(session.Map, session.Camera.View, true, session.Player);
        EndMode3D();
        combatUi.Draw(session, new(0, 0, GetScreenWidth(), GetScreenHeight()));
        dialogueUi.Draw(session, new(0, 0, GetScreenWidth(), GetScreenHeight()));
        inventoryUi.Draw(session, new(0, 0, GetScreenWidth(), GetScreenHeight()));
        EndDrawing();
        if (smoke && ++frames == 12) WorldRenderer.Screenshot(inventorySmoke || monsterSmoke
            ? Path.Combine(ProjectPaths.Root, "Editor", "obj", monsterSmoke ? "monster-runtime.png" : "inventory-runtime.png")
            : Path.Combine(ProjectPaths.Root, "wildwood-smoke.png"));
        if (smoke && frames >= 15) break;
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("Cannot open game: " + ex.Message);
    Environment.ExitCode = 1;
}
finally { CloseWindow(); }
