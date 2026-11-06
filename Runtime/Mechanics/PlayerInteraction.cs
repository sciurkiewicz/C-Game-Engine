using System.Numerics;

sealed partial class PlaySession
{
    public InventoryGrid Inventory => Map.Player.Inventory;
    public bool InventoryOpen { get; private set; }
    public ChestData? OpenedChest { get; private set; }
    public NpcData? NearbyNpc => Map.Npcs.Where(n => Vector3.Distance(Map.GroundPosition(n), Player) <= Character.InteractionRange)
        .OrderBy(n => Vector3.DistanceSquared(Map.GroundPosition(n), Player)).FirstOrDefault();
    public SceneObject? InteractionTarget => Map.Npcs.Cast<SceneObject>().Concat(Map.Chests)
        .Where(item => Vector3.Distance(Map.GroundPosition(item), Player) <= Character.InteractionRange)
        .OrderBy(item => Vector3.DistanceSquared(Map.GroundPosition(item), Player)).FirstOrDefault();
    public bool OpenChest(ChestData chest)
    {
        if (!PlayerAlive || Dialogue.Active || !Map.Chests.Contains(chest) || Vector3.Distance(Map.GroundPosition(chest), Player) > Character.InteractionRange) return false;
        chest.Opened = true; OpenedChest = chest; InventoryOpen = true; return true;
    }
    public void ToggleInventory()
    {
        if (!PlayerAlive || Dialogue.Active) return;
        if (InventoryOpen) CloseInventory();
        else { OpenedChest = null; InventoryOpen = true; }
    }
    public void CloseInventory() { InventoryOpen = false; OpenedChest = null; }
}
