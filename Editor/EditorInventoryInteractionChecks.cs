using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    void DragItem(InventoryGridView from, InventoryItem item, InventoryGridView to, int x, int y)
    {
        int sx = (int)(from.Bounds.X + (item.X + .5f) * from.Cell), sy = (int)(from.Bounds.Y + (item.Y + .5f) * from.Cell);
        int tx = (int)(to.Bounds.X + (x + .5f) * to.Cell), ty = (int)(to.Bounds.Y + (y + .5f) * to.Cell);
        Input(7, sx, sy); Input(6, 0); TestFrame();
        Input(7, tx, ty); TestFrame();
        Input(7, tx, ty); Input(5, 0); TestFrame();
    }
    void QuickTransfer(InventoryGridView from, InventoryItem item)
    {
        Input(2, (int)KeyboardKey.LeftShift);
        Click((int)(from.Bounds.X + (item.X + .5f) * from.Cell), (int)(from.Bounds.Y + (item.Y + .5f) * from.Cell));
        Input(1, (int)KeyboardKey.LeftShift); TestFrame();
    }
    public void RunInventoryInteractionChecks()
    {
        SetWindowSize(1440, 900);
        document.New(16, 16); play = null; selectedId = null; tool = EditTool.Select;
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        ClickHeader("Chest");
        var screen = GetWorldToScreenEx(Vector3.Zero, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Click((int)screen.X, (int)screen.Y);
        Verify(document.Map.Chests.Count == 1 && Selection is ChestData && tool == EditTool.Chest, "placing a chest keeps the Chest tab");
        Click((int)screen.X, (int)screen.Y);
        Verify(document.Map.Chests.Count == 2 && tool == EditTool.Chest, "next map click places another chest");
        ClickHeader("Undo"); Verify(document.Map.Chests.Count == 1 && tool == EditTool.Chest, "undo repeated chest placement keeps the tab");
        var chest = document.Map.Chests.Single(); string chestId = chest.Id;
        selectedId = chestId;
        Click(GetScreenWidth() - Right + 125, 260);
        Verify(ItemEditorOpen && EditedInventory == chest.Contents, "Chest tab opens the placed chest's contents");
        Press(KeyboardKey.Escape); Verify(!ItemEditorOpen && tool == EditTool.Chest, "closing Items keeps the Chest tab");
        ClickHeader("Select");
        ChangePosition(chest, 1, 0); TestFrame();
        Click(GetScreenWidth() - Right + 155, 437);
        Verify(ItemEditorOpen && EditedInventory == chest.Contents, "Items button opens chest contents");
        var box = ItemEditorBounds;
        Click((int)box.X + 120, (int)box.Y + 464);
        Verify(chest.Contents.Items.Count == 5 && chest.Contents.Items.Select(i => i.DefinitionId).Distinct().Count() == 5, "assign all five placeholders through the editor");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "inventory-editor.png"));
        var sword = chest.Contents.Items.Single(i => i.DefinitionId == "sword"); string swordId = sword.Id;
        int originalX = sword.X;
        DragItem(editorItemViews.Single(), sword, editorItemViews.Single(), 8, 0);
        Verify(sword.X == 8 && sword.Y == 0, "editor drag places the complete footprint");
        var armor = chest.Contents.Items.Single(i => i.DefinitionId == "armor");
        string positioned = document.Map.Serialize();
        DragItem(editorItemViews.Single(), sword, editorItemViews.Single(), armor.X, armor.Y);
        Verify(document.Map.Serialize() == positioned, "overlapping editor drop preserves all items");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Z); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        chest = document.Map.Chests.Single(); sword = chest.Contents.Items.Single(i => i.Id == swordId);
        Verify(sword.X == originalX, "one undo restores the item arrangement");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Y); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        chest = document.Map.Chests.Single(); sword = chest.Contents.Items.Single(i => i.Id == swordId);
        Verify(sword.X == 8, "redo restores the item arrangement");
        var view = editorItemViews.Single();
        Click((int)(view.Bounds.X + (sword.X + .5f) * view.Cell), (int)(view.Bounds.Y + (sword.Y + .5f) * view.Cell));
        Press(KeyboardKey.Delete);
        Verify(chest.Contents.Items.Count == 4, "Delete removes the selected inventory item");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Z); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        chest = document.Map.Chests.Single();
        Verify(chest.Contents.Items.Count == 5, "undo restores the deleted inventory item");
        Press(KeyboardKey.Escape); Verify(!ItemEditorOpen, "Escape closes item editor");
        selectedId = chestId; tool = EditTool.Select;
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.D); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        var copy = document.Map.Chests.Last();
        Verify(document.Map.Chests.Count == 2 && copy.Contents.Items.Count == 5 && !copy.Contents.Items.Select(i => i.Id).Intersect(chest.Contents.Items.Select(i => i.Id)).Any(), "chest duplicate has independent loot");
        Press(KeyboardKey.Delete); Verify(document.Map.Chests.Count == 1, "Delete removes a chest");
        var decoration = document.Place(ChestData.DefaultAsset, new(-3, 0, 0), 1.7f, true);
        selectedId = decoration.Id; TestFrame();
        Click(GetScreenWidth() - Right + 110, 437);
        Verify(ItemEditorOpen && document.Map.Chests.Count == 2 && document.Map.Objects.Count == 0 &&
            document.Map.Chests.Last().Id == decoration.Id && document.Map.Chests.Last().Height == 1.7f, "existing decorative chest gains contents without losing its scene properties");
        Press(KeyboardKey.Escape); Press(KeyboardKey.Delete);
        ClickHeader("Player"); Click(GetScreenWidth() - Right + 110, 319);
        Verify(editingPlayerInventory && EditedInventory == document.Map.Player.Inventory, "player starting inventory editor");
        box = ItemEditorBounds; Click((int)box.X + 150, (int)box.Y + 118);
        Verify(document.Map.Player.Inventory.Items.Count == 1, "individual placeholder assignment");
        Press(KeyboardKey.Escape); ClickHeader("Save");
        string saved = document.Map.Serialize();
        Verify(!document.Dirty && MapData.Load(mapPath).Serialize() == saved, "inventory and chest save/load through the editor");
        document.Map.Save(Path.Combine(ProjectPaths.Root, "Editor", "obj", "inventory-runtime-test.json"));
        Press(KeyboardKey.F5); Press(KeyboardKey.E);
        Verify(play?.InventoryOpen == true && play.OpenedChest?.Id == chestId && inventoryUi.Views.Count == 2, "E opens a nearby chest in playtest");
        var session = play!; var runtimeChest = session.OpenedChest!;
        sword = runtimeChest.Contents.Items.Single(i => i.Id == swordId);
        QuickTransfer(inventoryUi.Views[0], sword);
        Verify(runtimeChest.Contents.Items.Count == 4 && session.Inventory.Items.Count == 2 && session.Inventory.Items.Any(i => i.Id == swordId), "Shift-click moves loot into the player inventory");
        DragItem(inventoryUi.Views[1], sword, inventoryUi.Views[0], 8, 0);
        Verify(runtimeChest.Contents.Items.Count == 5 && session.Inventory.Items.Count == 1 && sword.X == 8, "drag returns loot to the chest");
        var dagger = runtimeChest.Contents.Items.Single(i => i.DefinitionId == "dagger");
        string beforeDrop = session.Map.Serialize();
        DragItem(inventoryUi.Views[0], dagger, inventoryUi.Views[1], 0, 0);
        Verify(session.Map.Serialize() == beforeDrop, "occupied runtime destination cannot lose an item");
        var chestView = inventoryUi.Views[0]; var playerView = inventoryUi.Views[1];
        int sx = (int)(chestView.Bounds.X + (sword.X + .5f) * chestView.Cell);
        int sy = (int)(chestView.Bounds.Y + (sword.Y + 2.5f) * chestView.Cell);
        int tx = (int)(playerView.Bounds.X + 6.5f * playerView.Cell), ty = (int)(playerView.Bounds.Y + 3.5f * playerView.Cell);
        Input(7, sx, sy); Input(6, 0); TestFrame(); Input(7, tx, ty); TestFrame(); Input(7, tx, ty); Input(5, 0); TestFrame();
        Verify(session.Inventory.Items.Contains(sword) && sword.X == 6 && sword.Y == 1, "grabbing a lower item cell preserves the drag offset across grids");
        QuickTransfer(inventoryUi.Views[1], sword);
        beforeDrop = session.Map.Serialize();
        chestView = inventoryUi.Views[0];
        sx = (int)(chestView.Bounds.X + (dagger.X + .5f) * chestView.Cell); sy = (int)(chestView.Bounds.Y + (dagger.Y + .5f) * chestView.Cell);
        Input(7, sx, sy); Input(6, 0); TestFrame(); Input(7, (int)viewport.X + 5, (int)viewport.Y + 5); TestFrame();
        Input(7, (int)viewport.X + 5, (int)viewport.Y + 5); Input(5, 0); TestFrame();
        Verify(session.Map.Serialize() == beforeDrop, "dropping outside both inventories preserves the item");
        var potion = runtimeChest.Contents.Items.Single(i => i.DefinitionId == "potion");
        DragItem(inventoryUi.Views[0], potion, inventoryUi.Views[1], 8, 2);
        Verify(session.Inventory.Items.Contains(potion) && potion.X == 8 && potion.Y == 2, "runtime drag transfers into a chosen free cell");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "inventory-playtest.png"));
        var position = session.Player;
        Input(2, (int)KeyboardKey.D); for (int i = 0; i < 5; i++) TestFrame();
        Input(1, (int)KeyboardKey.D); TestFrame();
        Verify(session.Player == position, "open inventory pauses player movement");
        Press(KeyboardKey.Escape);
        Verify(play == session && !session.InventoryOpen, "Escape closes inventory without exiting playtest");
        Press(KeyboardKey.I);
        Verify(session.InventoryOpen && session.OpenedChest == null && inventoryUi.Views.Count == 1, "I opens standalone player inventory");
        SetWindowSize(1120, 720); TestFrame(); TestFrame();
        Press(KeyboardKey.I); Press(KeyboardKey.E);
        Verify(inventoryUi.Views.Count == 2 && inventoryUi.Views.All(v => v.Bounds.X >= viewport.X && v.Bounds.Y >= viewport.Y &&
            v.Bounds.X + v.Bounds.Width <= viewport.X + viewport.Width && v.Bounds.Y + v.Bounds.Height <= viewport.Y + viewport.Height), "two inventory grids fit the minimum editor viewport");
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "inventory-small.png"));
        Press(KeyboardKey.F5);
        Verify(play == null && document.Map.Serialize() == saved && !document.Dirty, "playtest loot leaves the saved editor map unchanged");
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        Console.WriteLine("Inventory interaction checks passed: chest tool, item assignment, drag/drop, invalid drops, undo/redo, duplication, save/load, Shift-click, E/I/Escape, movement pause, window resize and playtest isolation.");
    }
}
