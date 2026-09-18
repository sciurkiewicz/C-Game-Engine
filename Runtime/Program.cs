using Raylib_cs;
using static Raylib_cs.Raylib;

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
SetTargetFPS(60);
try
{
    using var assets = new WorldAssets();
    int mapArgument = Array.IndexOf(args, "--map");
    if (mapArgument >= 0 && mapArgument + 1 >= args.Length)
        throw new ArgumentException("--map requires a JSON file path.");
    string mapPath = mapArgument >= 0 ? Path.GetFullPath(args[mapArgument + 1]) : ProjectPaths.MapFile;
    var map = File.Exists(mapPath) ? MapData.Load(mapPath) : mapArgument >= 0
        ? throw new FileNotFoundException("Map not found.", mapPath) : MapData.CreateDemo(assets.Sprites.Keys);
    var session = new PlaySession(map);
    var renderer = new WorldRenderer(assets);
    if (args.Contains("--close")) session.Camera.Zoom(100);
    if (args.Contains("--far")) session.Camera.Zoom(-100);
    session.Camera.Snap(session.Player);
    int frames = 0;
    bool smoke = args.Contains("--smoke-test");
    while (!WindowShouldClose())
    {
        session.Update(Math.Min(GetFrameTime(), .05f), GetMouseWheelMove());
        BeginDrawing();
        ClearBackground(new Color(38, 47, 54, 255));
        BeginMode3D(session.Camera.View);
        renderer.Draw(session.Map, session.Camera.View, true, session.Player);
        EndMode3D();
        EndDrawing();
        if (smoke && ++frames == 12) WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "wildwood-smoke.png"));
        if (smoke && frames >= 15) break;
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("Cannot open game: " + ex.Message);
    Environment.ExitCode = 1;
}
finally { CloseWindow(); }
