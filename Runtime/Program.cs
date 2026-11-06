using Raylib_cs;
using static Raylib_cs.Raylib;

if (args.Contains("--graphics-test"))
{
    GraphicsChecks.Run();
    return;
}

if (args.Contains("--narrative-test"))
{
    NarrativeChecks.Run();
    return;
}

if (args.Contains("--character-test"))
{
    CharacterChecks.Run();
    return;
}

if (args.Contains("--world-test"))
{
    WorldFeatureChecks.Run();
    return;
}

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
InitWindow(1280, 800, "Wildwood");
if (!IsWindowReady())
{
    Console.Error.WriteLine("Cannot open Runtime: no graphical display or OpenGL context is available.");
    Environment.ExitCode = 1; return;
}
SetWindowMinSize(800, 540);
SetExitKey(KeyboardKey.Null);
SetTargetFPS(60);
try
{
    using var assets = new WorldAssets();
    if (args.Contains("--graphics-gpu-test"))
    {
        GraphicsGpuChecks.Run(assets);
        return;
    }
    if (args.Contains("--world-gpu-test"))
    {
        WorldFeatureGpuChecks.Run(assets);
        return;
    }
    if (args.Contains("--terrain-blend-test"))
    {
        TerrainBlendChecks.Run(assets);
        return;
    }
    if (args.Contains("--sprite-water-test"))
    {
        SpriteWaterChecks.Run(assets);
        return;
    }
    int mapArgument = Array.IndexOf(args, "--map");
    if (mapArgument >= 0 && mapArgument + 1 >= args.Length)
        throw new ArgumentException("--map requires a JSON file path.");
    string mapPath = mapArgument >= 0 ? Path.GetFullPath(args[mapArgument + 1]) : ProjectPaths.MapFile;
    if (args.Contains("--menu-interaction-test"))
    {
        using var checks = new GameApp(assets, Path.Combine(ProjectPaths.Root, "Runtime", "obj", "menu-test.json"), true);
        checks.RunMenuInteractionChecks();
        return;
    }
    if (args.Contains("--narrative-interaction-test"))
    {
        string directory = Path.Combine(Path.GetTempPath(), "wildwood-narrative-ui-" + Guid.NewGuid().ToString("N"));
        using var checks = new GameApp(assets, Path.Combine(directory, "map.json"), true, Path.Combine(directory, "intro.json"), new GraphicsSettings { Enabled = false });
        checks.RunNarrativeInteractionChecks();
        return;
    }
    using var game = new GameApp(assets, mapPath, mapArgument >= 0);
    game.Run(args);
}
catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("Cannot open game: " + ex.Message);
    Environment.ExitCode = 1;
}
finally { if (IsWindowReady()) CloseWindow(); }
