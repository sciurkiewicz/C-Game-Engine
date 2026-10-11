using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

readonly record struct InventoryGridView(InventoryGrid Grid, Rectangle Bounds, string Title)
{
    public float Cell => Bounds.Width / Grid.Width;
    public Rectangle ItemBounds(InventoryItem item)
    {
        var definition = ItemCatalog.Find(item.DefinitionId)!;
        return new(Bounds.X + item.X * Cell, Bounds.Y + item.Y * Cell, definition.Width * Cell, definition.Height * Cell);
    }
    public InventoryItem? ItemAt(Vector2 point)
    {
        if (!CheckCollisionPointRec(point, Bounds)) return null;
        int x = (int)((point.X - Bounds.X) / Cell), y = (int)((point.Y - Bounds.Y) / Cell);
        return Grid.Items.FirstOrDefault(item =>
        {
            var definition = ItemCatalog.Find(item.DefinitionId)!;
            return x >= item.X && x < item.X + definition.Width && y >= item.Y && y < item.Y + definition.Height;
        });
    }
}

sealed class InventoryInteraction
{
    InventoryGrid? source;
    InventoryItem? held;
    int grabX, grabY;
    public string? SelectedId { get; private set; }
    public string Message { get; private set; } = "";
    public void Reset() { source = null; held = null; SelectedId = null; Message = ""; }
    public void Draw(IReadOnlyList<InventoryGridView> views, Action? beforeChange = null, bool quickTransfer = true)
    {
        foreach (var view in views)
        {
            string title = view.Title;
            if (UiText.Measure(title, 18) > view.Bounds.Width)
            {
                while (title.Length > 0 && UiText.Measure(title + "...", 18) > view.Bounds.Width) title = title[..^1];
                title += "...";
            }
            UiText.Draw(title, (int)view.Bounds.X, (int)view.Bounds.Y - 28, 18, new(234, 200, 128, 255));
            DrawRectangleRec(view.Bounds, new(13, 20, 28, 255));
            for (int x = 0; x <= view.Grid.Width; x++)
                DrawLine((int)(view.Bounds.X + x * view.Cell), (int)view.Bounds.Y, (int)(view.Bounds.X + x * view.Cell), (int)(view.Bounds.Y + view.Bounds.Height), new(54, 65, 77, 255));
            for (int y = 0; y <= view.Grid.Height; y++)
                DrawLine((int)view.Bounds.X, (int)(view.Bounds.Y + y * view.Cell), (int)(view.Bounds.X + view.Bounds.Width), (int)(view.Bounds.Y + y * view.Cell), new(54, 65, 77, 255));
            foreach (var item in view.Grid.Items)
            {
                DrawItem(ItemCatalog.Find(item.DefinitionId)!, view.ItemBounds(item), held?.Id == item.Id ? (byte)65 : (byte)255);
                if (SelectedId == item.Id) DrawRectangleLinesEx(view.ItemBounds(item), 2, Color.White);
            }
        }
        var mouse = GetMousePosition();
        var hoverIndex = -1;
        for (int i = 0; i < views.Count; i++) if (CheckCollisionPointRec(mouse, views[i].Bounds)) hoverIndex = i;
        if (held == null && IsMouseButtonPressed(MouseButton.Left) && hoverIndex >= 0 && views[hoverIndex].ItemAt(mouse) is { } clicked)
        {
            var view = views[hoverIndex]; SelectedId = clicked.Id; Message = "";
            bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
            if (shift && quickTransfer && views.Count == 2)
            {
                var target = views[1 - hoverIndex].Grid;
                if (target.FindSpace(clicked.DefinitionId) is { } space)
                {
                    beforeChange?.Invoke(); InventoryGrid.Transfer(view.Grid, target, clicked.Id, space.X, space.Y);
                }
                else Message = "Brak miejsca na ten przedmiot.";
            }
            else
            {
                held = clicked; source = view.Grid;
                grabX = (int)((mouse.X - view.Bounds.X) / view.Cell) - clicked.X;
                grabY = (int)((mouse.Y - view.Bounds.Y) / view.Cell) - clicked.Y;
            }
        }
        if (held != null)
        {
            if (hoverIndex >= 0)
            {
                var target = views[hoverIndex];
                int x = (int)MathF.Floor((mouse.X - target.Bounds.X) / target.Cell) - grabX;
                int y = (int)MathF.Floor((mouse.Y - target.Bounds.Y) / target.Cell) - grabY;
                bool valid = target.Grid.CanPlace(held.DefinitionId, x, y, ReferenceEquals(source, target.Grid) ? held.Id : null);
                var definition = ItemCatalog.Find(held.DefinitionId)!;
                DrawRectangleLinesEx(new(target.Bounds.X + x * target.Cell, target.Bounds.Y + y * target.Cell,
                    definition.Width * target.Cell, definition.Height * target.Cell), 3, valid ? new(104, 217, 175, 255) : new(245, 95, 95, 255));
                if (IsMouseButtonReleased(MouseButton.Left))
                {
                    if (valid && (!ReferenceEquals(source, target.Grid) || held.X != x || held.Y != y))
                    {
                        beforeChange?.Invoke(); InventoryGrid.Transfer(source!, target.Grid, held.Id, x, y);
                    }
                    else if (!valid) Message = "Przedmiot nie mieści się w tym miejscu.";
                    held = null; source = null;
                }
            }
            else if (IsMouseButtonReleased(MouseButton.Left)) { held = null; source = null; }
            if (held != null)
            {
                var definition = ItemCatalog.Find(held.DefinitionId)!;
                float cell = hoverIndex >= 0 ? views[hoverIndex].Cell : views[0].Cell;
                DrawItem(definition, new(mouse.X - (grabX + .5f) * cell, mouse.Y - (grabY + .5f) * cell,
                    definition.Width * cell, definition.Height * cell), 210);
            }
        }
    }
    public string Description(IReadOnlyList<InventoryGridView> views)
    {
        var item = views.Select(view => view.ItemAt(GetMousePosition())).FirstOrDefault(i => i != null) ??
            views.SelectMany(view => view.Grid.Items).FirstOrDefault(i => i.Id == SelectedId);
        var definition = item == null ? null : ItemCatalog.Find(item.DefinitionId);
        return definition == null ? "Przeciągnij przedmiot na wolne pola." : $"{definition.Name}  /  {definition.Width} x {definition.Height} pól";
    }
    public static void DrawItem(ItemDefinition item, Rectangle rect, byte alpha = 255)
    {
        var color = item.Color; color.A = alpha;
        DrawRectangleRec(new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), new(color.R / 2, color.G / 2, color.B / 2, alpha));
        DrawRectangleLinesEx(new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), 2, color);
        var center = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 4);
        float w = Math.Min(rect.Width * .55f, 46), h = Math.Min(rect.Height * .55f, 80);
        var ink = new Color(color.R, color.G, color.B, alpha);
        if (item.Id == "potion")
        {
            DrawCircleV(center + new Vector2(0, 3), Math.Min(w, h) * .4f, ink);
            DrawRectangle((int)(center.X - w * .15f), (int)(center.Y - h * .45f), (int)(w * .3f), (int)(h * .35f), ink);
        }
        else if (item.Id is "dagger" or "sword")
        {
            DrawLineEx(center - new Vector2(0, h / 2), center + new Vector2(0, h / 2), Math.Max(3, w * .15f), ink);
            DrawLineEx(center + new Vector2(-w / 2, h * .22f), center + new Vector2(w / 2, h * .22f), 3, ink);
        }
        else if (item.Id == "armor")
        {
            DrawRectangleRec(new(center.X - w * .3f, center.Y - h * .4f, w * .6f, h * .8f), ink);
            DrawLineEx(center + new Vector2(-w / 2, -h * .35f), center + new Vector2(w / 2, -h * .35f), Math.Max(6, h * .2f), ink);
        }
        else DrawTriangle(center + new Vector2(-w / 2, -h / 2), center + new Vector2(0, h / 2), center + new Vector2(w / 2, -h / 2), ink);
        if (rect.Height >= 42) UiText.Draw($"{item.Width}x{item.Height}", (int)(rect.X + rect.Width / 2) - 11, (int)(rect.Y + rect.Height - 16), 11, new(235, 241, 245, (int)alpha));
    }
}

sealed class InventoryOverlay
{
    readonly InventoryInteraction interaction = new();
    PlaySession? owner;
    public IReadOnlyList<InventoryGridView> Views { get; private set; } = [];
    public void Draw(PlaySession play, Rectangle bounds)
    {
        if (!ReferenceEquals(owner, play)) { owner = play; interaction.Reset(); }
        if (!play.InventoryOpen)
        {
            interaction.Reset(); Views = [];
            if (!play.PlayerAlive || play.Dialogue.Active) return;
            DrawRectangle((int)(bounds.X + bounds.Width - 170), (int)bounds.Y + 12, 158, 28, new(20, 28, 38, 235));
            UiText.Draw("I  Ekwipunek", (int)(bounds.X + bounds.Width - 154), (int)bounds.Y + 18, 15, new(230, 238, 242, 255));
            if (play.InteractionTarget is ChestData chest)
            {
                string hint = $"E  Otwórz: {chest.Name}";
                int width = Math.Min((int)bounds.Width - 24, UiText.Measure(hint, 18) + 32);
                int x = (int)(bounds.X + (bounds.Width - width) / 2), y = (int)(bounds.Y + bounds.Height - 58);
                DrawRectangle(x, y, width, 38, new(20, 28, 38, 248));
                UiText.Draw(hint, x + 16, y + 10, 18, new(234, 200, 128, 255));
            }
            return;
        }
        bool chestOpen = play.OpenedChest != null;
        int columns = play.Inventory.Width + (chestOpen ? play.OpenedChest!.Contents.Width : 0);
        int cell = Math.Min(48, (int)(bounds.Width - (chestOpen ? 96 : 72)) / columns);
        int rows = Math.Max(play.Inventory.Height, chestOpen ? play.OpenedChest!.Contents.Height : 0);
        cell = Math.Min(cell, (int)(bounds.Height - 172) / rows);
        cell = Math.Max(12, cell);
        int w = columns * cell + (chestOpen ? 72 : 48), h = rows * cell + 148;
        int px = (int)(bounds.X + (bounds.Width - w) / 2), py = (int)(bounds.Y + (bounds.Height - h) / 2);
        DrawRectangleRec(bounds, new(0, 0, 0, 125));
        DrawRectangle(px, py, w, h, new(20, 28, 38, 252));
        DrawRectangleLines(px, py, w, h, new(145, 115, 67, 255));
        UiText.Draw(chestOpen ? "SKRZYNIA I EKWIPUNEK" : "EKWIPUNEK", px + 24, py + 17, 20, new(234, 200, 128, 255));
        var close = new Rectangle(px + w - 44, py + 12, 30, 28);
        DrawRectangleRec(close, new(48, 61, 75, 255)); UiText.Draw("X", (int)close.X + 9, (int)close.Y + 5, 17, Color.White);
        if (CheckCollisionPointRec(GetMousePosition(), close) && IsMouseButtonPressed(MouseButton.Left)) { play.CloseInventory(); interaction.Reset(); return; }
        int gx = px + 24, gy = py + 80;
        var views = new List<InventoryGridView>();
        if (chestOpen)
        {
            var chest = play.OpenedChest!;
            views.Add(new(chest.Contents, new(gx, gy, chest.Contents.Width * cell, chest.Contents.Height * cell), chest.Name));
            gx += chest.Contents.Width * cell + 24;
        }
        views.Add(new(play.Inventory, new(gx, gy, play.Inventory.Width * cell, play.Inventory.Height * cell), "Bohater"));
        Views = views; interaction.Draw(Views);
        UiText.Draw(interaction.Description(Views), px + 24, py + h - 53, 14, new(230, 238, 242, 255));
        UiText.Draw(interaction.Message.Length > 0 ? interaction.Message : chestOpen ? "Shift + klik: przenieś   /   I, Esc: zamknij" : "I, Esc: zamknij", px + 24, py + h - 29, 13,
            interaction.Message.Length > 0 ? new(255, 177, 100, 255) : new(150, 170, 186, 255));
    }
}
