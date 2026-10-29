using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    float ChestHeight => assets.Sprites.TryGetValue(ChestData.DefaultAsset, out var sprite) ? sprite.DefaultHeight : 1.4f;
    string? chestInventoryId;
    bool editingPlayerInventory;
    readonly InventoryInteraction itemEditorInteraction = new();
    IReadOnlyList<InventoryGridView> editorItemViews = [];
    string itemEditorMessage = "";
    bool ItemEditorOpen => editingPlayerInventory || chestInventoryId != null;
    InventoryGrid? EditedInventory => editingPlayerInventory ? document.Map.Player.Inventory :
        document.Map.Chests.FirstOrDefault(c => c.Id == chestInventoryId)?.Contents;
    Rectangle ItemEditorBounds => new((GetScreenWidth() - Math.Min(900, GetScreenWidth() - 48)) / 2,
        (GetScreenHeight() - Math.Min(600, GetScreenHeight() - 48)) / 2, Math.Min(900, GetScreenWidth() - 48), Math.Min(600, GetScreenHeight() - 48));
    void OpenItemEditor(ChestData? chest)
    {
        editingPlayerInventory = chest == null; chestInventoryId = chest?.Id;
        itemEditorInteraction.Reset(); itemEditorMessage = "";
    }
    void CloseItemEditor()
    {
        editingPlayerInventory = false; chestInventoryId = null;
        editorItemViews = []; itemEditorInteraction.Reset();
    }
    void UpdateItemEditor()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { CloseItemEditor(); return; }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && (IsKeyPressed(KeyboardKey.Z) || IsKeyPressed(KeyboardKey.Y)))
        {
            itemEditorInteraction.Reset(); itemEditorMessage = "";
            if (IsKeyPressed(KeyboardKey.Z)) Undo(); else Redo();
        }
        if (EditedInventory == null) { CloseItemEditor(); return; }
        if (IsKeyPressed(KeyboardKey.Delete)) RemoveInventoryItem();
    }
    void RemoveInventoryItem()
    {
        if (EditedInventory is not { } grid || grid.Items.FirstOrDefault(i => i.Id == itemEditorInteraction.SelectedId) is not { } item) return;
        document.Checkpoint(); grid.Items.Remove(item); itemEditorInteraction.Reset(); itemEditorMessage = "Przedmiot usunięty.";
    }
    void AddInventoryItem(ItemDefinition definition)
    {
        if (EditedInventory is not { } grid) return;
        if (grid.FindSpace(definition.Id) == null) { itemEditorMessage = "Brak miejsca na ten przedmiot."; return; }
        document.Checkpoint(); grid.Add(definition.Id); itemEditorInteraction.Reset(); itemEditorMessage = "Dodano: " + definition.Name;
    }
    void AddInventorySamples()
    {
        if (EditedInventory is not { } grid || !ItemCatalog.All.Any(definition => grid.FindSpace(definition.Id) != null))
        { itemEditorMessage = "Brak wolnych pól."; return; }
        document.Checkpoint(); int added = ItemCatalog.All.Count(definition => grid.Add(definition.Id) != null);
        itemEditorInteraction.Reset(); itemEditorMessage = $"Dodano {added} / 5 przedmiotów.";
    }
    void DrawItemEditor()
    {
        if (EditedInventory is not { } grid) { CloseItemEditor(); return; }
        var box = ItemEditorBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 170));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label(editingPlayerInventory ? "STARTOWY EKWIPUNEK" : "PRZEDMIOTY SKRZYNI", x + 24, y + 20, 22, Accent);
        Label("Kliknij +, aby dodać. Przeciągaj na wolne pola. Delete usuwa zaznaczony item.", x + 24, y + 58, 14, Muted);
        int row = y + 100;
        foreach (var definition in ItemCatalog.All)
        {
            InventoryInteraction.DrawItem(definition, new(x + 24, row, definition.Width * 18, definition.Height * 18));
            if (Button("+ " + definition.Name, x + 78, row + 4, 158, false, true, true)) AddInventoryItem(definition);
            Label($"{definition.Width} x {definition.Height} pól", x + 84, row + 41, 12, Muted); row += 68;
        }
        if (Button("+ 5 placeholderów", x + 24, row + 8, 212, false, true, true)) AddInventorySamples();
        int cell = Math.Min(48, (int)(box.Width - 300) / grid.Width);
        cell = Math.Min(cell, (int)(box.Height - 230) / grid.Height);
        string title = editingPlayerInventory ? "Bohater" : document.Map.Chests.First(c => c.Id == chestInventoryId).Name;
        editorItemViews = [new(grid, new(x + 276, y + 112, grid.Width * cell, grid.Height * cell), title)];
        itemEditorInteraction.Draw(editorItemViews, document.Checkpoint, false);
        Label(itemEditorInteraction.Description(editorItemViews), x + 276, y + (int)box.Height - 111, 15, Text);
        Label(itemEditorInteraction.Message.Length > 0 ? itemEditorInteraction.Message : itemEditorMessage,
            x + 276, y + (int)box.Height - 83, 14, Accent);
        if (Button("Usuń item", x + 276, y + (int)box.Height - 48, 132, false,
            grid.Items.Any(i => i.Id == itemEditorInteraction.SelectedId), true)) RemoveInventoryItem();
        if (Button("Cofnij", x + 422, y + (int)box.Height - 48, 92, false, document.CanUndo, true))
        { itemEditorInteraction.Reset(); Undo(); }
        if (Button("Zamknij", x + (int)box.Width - 156, y + (int)box.Height - 48, 132, true, true, true)) CloseItemEditor();
    }
}
