using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class PlaySession
{
    public MapData Map { get; private set; }
    public Vector3 Player { get; private set; }
    public FollowCamera Camera { get; }
    public DialogueSession Dialogue { get; } = new();
    public InventoryGrid Inventory => Map.Player.Inventory;
    public bool InventoryOpen { get; private set; }
    public ChestData? OpenedChest { get; private set; }
    public const float TalkDistance = 2.5f;
    public NpcData? NearbyNpc => Map.Npcs.Where(n => Vector3.Distance(Map.GroundPosition(n), Player) <= TalkDistance)
        .OrderBy(n => Vector3.DistanceSquared(Map.GroundPosition(n), Player)).FirstOrDefault();
    public SceneObject? InteractionTarget => Map.Npcs.Cast<SceneObject>().Concat(Map.Chests)
        .Where(item => Vector3.Distance(Map.GroundPosition(item), Player) <= TalkDistance)
        .OrderBy(item => Vector3.DistanceSquared(Map.GroundPosition(item), Player)).FirstOrDefault();
    public bool OpenChest(ChestData chest)
    {
        if (!PlayerAlive || Dialogue.Active || !Map.Chests.Contains(chest) || Vector3.Distance(Map.GroundPosition(chest), Player) > TalkDistance) return false;
        OpenedChest = chest; InventoryOpen = true; return true;
    }
    public void ToggleInventory()
    {
        if (!PlayerAlive || Dialogue.Active) return;
        if (InventoryOpen) CloseInventory();
        else { OpenedChest = null; InventoryOpen = true; }
    }
    public void CloseInventory() { InventoryOpen = false; OpenedChest = null; }
    public PlaySession(MapData map)
    {
        initialMap = MapSnapshot.Capture(map);
        Map = initialMap.Restore();
        Player = Map.GroundPosition(Map.Player.X, Map.Player.Z);
        Camera = new FollowCamera(Player);
        InitializeCombat();
    }
    public void Update(float dt, float wheel)
    {
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
        var move = Camera.Right * x + Camera.Backward * z;
        if (move.LengthSquared() > 0)
        {
            move = Vector3.Normalize(move) * (IsKeyDown(KeyboardKey.LeftShift) ? 6 : 3.6f) * dt;
            var next = Map.Clamp(Player + new Vector3(move.X, 0, 0));
            if (WorldRenderer.CanWalk(Map, next, Player)) Player = next;
            next = Map.Clamp(Player + new Vector3(0, 0, move.Z));
            if (WorldRenderer.CanWalk(Map, next, Player)) Player = next;
        }
        Camera.Update(Player, dt);
        UpdateCombat(dt, IsKeyDown(KeyboardKey.Space));
    }
}
