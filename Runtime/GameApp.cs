using Raylib_cs;
using static Raylib_cs.Raylib;

// Runtime owns the main menu; the editor starts a PlaySession directly for playtesting.
sealed partial class GameApp(WorldAssets assets, string mapPath, bool requireMap, string? narrativeFile = null, GraphicsSettings? graphicsOverride = null) : IDisposable
{
    readonly MainMenu menu = new();
    readonly WorldRenderer renderer = new(assets);
    readonly WorldAudio audio = new();
    readonly DialogueOverlay dialogueUi = new();
    readonly InventoryOverlay inventoryUi = new();
    readonly CombatOverlay combatUi = new();
    readonly WorldVisualEffects visualEffects = new();
    PlaySession? session;
    NarrativePlayer? narrative;
    NarrativeGameTransition? gameTransition;
    bool waitForNarrativeInputRelease;
    bool quit;
    int activeFps = 60;
    string[] launchArguments = [];

    bool StartGame(bool showNarrative = true)
    {
        menu.Message = "";
        try
        {
            var map = File.Exists(mapPath) ? MapData.Load(mapPath) : requireMap
                ? throw new FileNotFoundException("Nie znaleziono mapy.", mapPath) : MapData.CreateDemo(assets.Sprites.Keys);
            var next = new PlaySession(map, mapPath, character: PlayerDefinition.Load(ProjectPaths.PlayerDefinitionFile));
            var graphics = graphicsOverride ?? GraphicsSettings.Load(GraphicsSettings.FilePath);
            if (launchArguments.Contains("--close")) next.Camera.Zoom(100);
            if (launchArguments.Contains("--far")) next.Camera.Zoom(-100);
            next.Camera.Snap(next.Player);
            if (launchArguments.Contains("--dialogue-smoke-test") && next.Map.Npcs.FirstOrDefault() is { } npc) next.Dialogue.Begin(npc);
            if (launchArguments.Contains("--inventory-smoke-test"))
            {
                if (next.Map.Chests.FirstOrDefault() is { } chest)
                {
                    if (!next.OpenChest(chest)) throw new ArgumentException("Inventory smoke test requires the first chest to be near the player spawn.");
                }
                else next.ToggleInventory();
            }
            NarrativePlayer? intro = null;
            if (showNarrative)
            {
                string path = narrativeFile ?? NarrativeSequence.IntroFile;
                var sequence = NarrativeSequence.Load(path);
                if (sequence.Enabled && sequence.Cards.Count > 0) intro = new(sequence, path);
            }
            narrative?.Dispose(); narrative = intro; gameTransition = null; session = next; waitForNarrativeInputRelease = false;
            visualEffects.Configure(graphics,next);
            SetFrameLimit(intro == null ? next.Map.Runtime.TargetFps : 60);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
        { menu.Message = "Nie można rozpocząć gry: " + ex.Message; return false; }
    }
    void ReturnToMenu()
    {
        narrative?.Dispose(); narrative = null; gameTransition = null; waitForNarrativeInputRelease = false;
        audio.Stop(); session = null; menu.Message = ""; SetFrameLimit(60);
    }
    void SetFrameLimit(int fps)
    {
        if (activeFps == fps) return;
        activeFps = fps; SetTargetFPS(fps);
    }
    void Frame(float dt)
    {
        if (narrative != null)
        {
            try
            {
                narrative.Update(dt);
                if (!narrative.Active)
                {
                    gameTransition = narrative.GameTransition;
                    narrative.Dispose(); narrative = null; waitForNarrativeInputRelease = true;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { ReturnToMenu(); menu.Message = "Nie można wyświetlić karty fabularnej: " + ex.Message; }
        }
        else if (gameTransition != null)
        {
            gameTransition.Update(dt);
            if (!gameTransition.Active) { gameTransition = null; SetFrameLimit(session!.Map.Runtime.TargetFps); }
        }
        else if (session == null)
        {
            switch (menu.Update())
            {
                case MenuAction.NewGame: StartGame(); break;
                case MenuAction.Quit: quit = true; break;
            }
        }
        else if (IsKeyPressed(KeyboardKey.Escape) && !session.Dialogue.Active && !session.InventoryOpen) ReturnToMenu();
        else
        {
            bool held = IsKeyDown(KeyboardKey.Space) || IsKeyDown(KeyboardKey.Enter) || IsKeyDown(KeyboardKey.Escape) || IsMouseButtonDown(MouseButton.Left);
            if (!waitForNarrativeInputRelease || !held)
            {
                waitForNarrativeInputRelease = false;
                float simulationDt = Math.Min(dt, .05f);
                session.Update(simulationDt, GetMouseWheelMove());
                visualEffects.Update(session,simulationDt);
                SetFrameLimit(session.Map.Runtime.TargetFps);
                audio.Update(session, simulationDt, menu.Volume);
            }
        }
        if (session != null && narrative == null)
            visualEffects.Render(renderer,session.Map,visualEffects.View(session.Camera.View),session.Player,GetScreenWidth(),GetScreenHeight(),session.TimeOfDay);
        BeginDrawing();
        if (narrative != null) narrative.Draw(new(0, 0, GetScreenWidth(), GetScreenHeight()));
        else if (session == null) menu.Draw();
        else
        {
            var bounds = new Rectangle(0, 0, GetScreenWidth(), GetScreenHeight());
            visualEffects.Draw(bounds);
            combatUi.Draw(session, bounds); dialogueUi.Draw(session, bounds); inventoryUi.Draw(session, bounds);
            if (gameTransition != null) DrawRectangleRec(bounds, new Color(0, 0, 0, (int)MathF.Round(gameTransition.BlackOpacity * 255)));
        }
        EndDrawing();
    }
    public void Run(string[] args)
    {
        launchArguments = args;
        bool inventorySmoke = args.Contains("--inventory-smoke-test"), monsterSmoke = args.Contains("--monster-smoke-test");
        bool smoke = args.Contains("--smoke-test") || args.Contains("--dialogue-smoke-test") || inventorySmoke || monsterSmoke;
        if (smoke && !StartGame(false)) throw new InvalidDataException(menu.Message);
        int frames = 0;
        while (!quit && !WindowShouldClose())
        {
            Frame(GetFrameTime());
            if (smoke && ++frames == 12) WorldRenderer.Screenshot(inventorySmoke || monsterSmoke
                ? Path.Combine(ProjectPaths.Root, "Editor", "obj", monsterSmoke ? "monster-runtime.png" : "inventory-runtime.png")
                : Path.Combine(ProjectPaths.Root, "wildwood-smoke.png"));
            if (smoke && frames >= 15) break;
        }
    }
    public void Dispose() { narrative?.Dispose(); visualEffects.Dispose(); audio.Dispose(); }
}
