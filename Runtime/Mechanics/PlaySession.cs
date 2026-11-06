using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class PlaySession
{
    public MapData Map { get; private set; }
    public Vector3 Player { get; private set; }
    public PlayerDefinition Character { get; }
    public FollowCamera Camera { get; }
    public DialogueSession Dialogue { get; } = new();
    public PlaySession(MapData map, string? sourcePath = null, string? progressPath = null, PlayerDefinition? character = null)
    {
        Character = character ?? new PlayerDefinition();
        Character.Validate();
        entryMapPath = CurrentMapPath = Path.GetFullPath(sourcePath ?? ProjectPaths.MapFile);
        ProgressPath = progressPath ?? GameProgress.SavePath(entryMapPath);
        initialMap = MapSnapshot.Capture(map);
        Map = initialMap.Restore();
        Player = Map.GroundPosition(Map.Player.X, Map.Player.Z);
        Camera = new FollowCamera(Player, Map.Runtime);
        InitializeCombat();
        ResetWorldState();
    }
    public void Update(float dt, float wheel)
    {
        UpdateProgressInput(dt);
        if (!PlayerAlive)
        {
            if (IsKeyPressed(KeyboardKey.Enter)) Restart();
            Camera.Update(Player, dt); return;
        }
        if (Dialogue.Active)
        {
            if (IsKeyPressed(KeyboardKey.Escape)) Dialogue.Close();
            Camera.Update(Player, dt);
            return;
        }
        if (IsKeyPressed(KeyboardKey.I)) ToggleInventory();
        if (InventoryOpen)
        {
            if (IsKeyPressed(KeyboardKey.Escape)) CloseInventory();
            Camera.Update(Player, dt); return;
        }
        if (IsKeyPressed(KeyboardKey.I)) { Camera.Update(Player, dt); return; }
        if (IsKeyPressed(KeyboardKey.E) && InteractionTarget is { } target)
        {
            if (target is NpcData npc) Dialogue.Begin(npc);
            else if (target is ChestData chest) OpenChest(chest);
            return;
        }
        if (IsKeyPressed(KeyboardKey.Equal) || IsKeyPressedRepeat(KeyboardKey.Equal) || IsKeyPressed(KeyboardKey.KpAdd) || IsKeyPressedRepeat(KeyboardKey.KpAdd)) wheel++;
        if (IsKeyPressed(KeyboardKey.Minus) || IsKeyPressedRepeat(KeyboardKey.Minus) || IsKeyPressed(KeyboardKey.KpSubtract) || IsKeyPressedRepeat(KeyboardKey.KpSubtract)) wheel--;
        Camera.Zoom(wheel);
        if (IsKeyPressed(KeyboardKey.R)) Camera.Reset();
        float x = (IsKeyDown(KeyboardKey.D) || IsKeyDown(KeyboardKey.Right) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) || IsKeyDown(KeyboardKey.Left) ? 1 : 0);
        float z = (IsKeyDown(KeyboardKey.S) || IsKeyDown(KeyboardKey.Down) ? 1 : 0) - (IsKeyDown(KeyboardKey.W) || IsKeyDown(KeyboardKey.Up) ? 1 : 0);
        MovePlayer(new(x, z), IsKeyDown(KeyboardKey.LeftShift), dt);
        Camera.Update(Player, dt);
        UpdateCombat(dt, IsKeyDown(KeyboardKey.Space));
        AdvanceWorld(dt);
    }
}
