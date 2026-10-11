using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

static class InventoryChecks
{
    public static void Run()
    {
        Check(ItemCatalog.All.Count == 5 && ItemCatalog.All.Select(i => (i.Color.R, i.Color.G, i.Color.B)).Distinct().Count() == 5,
            "five distinct coloured placeholders");
        var grid = new InventoryGrid();
        foreach (var definition in ItemCatalog.All) Check(grid.Add(definition.Id) != null, "automatic placement of " + definition.Name);
        grid.Validate();
        Check(grid.Items.Sum(i => { var d = ItemCatalog.Find(i.DefinitionId)!; return d.Width * d.Height; }) == 16, "items occupy their complete footprints");
        Check(!grid.CanPlace("armor", 9, 0) && !grid.CanPlace("sword", 0, 2) && !grid.CanPlace("armor", int.MaxValue, 0), "bounds include the whole item");
        var narrow = new InventoryGrid { Width = 1, Height = 2 };
        Check(narrow.Add("sword") == null && narrow.Items.Count == 0, "a tall item cannot fit into a short grid");
        var sword = grid.Items.Single(i => i.DefinitionId == "sword");
        var potion = grid.Items.Single(i => i.DefinitionId == "potion");
        string before = JsonSerializer.Serialize(grid);
        Check(!InventoryGrid.Transfer(grid, grid, sword.Id, potion.X, potion.Y) && JsonSerializer.Serialize(grid) == before,
            "an overlapping drop preserves the original position");
        Check(InventoryGrid.Transfer(grid, grid, sword.Id, 8, 0) && sword.X == 8, "moving within the same inventory ignores the item itself");
        var full = new InventoryGrid { Width = 1, Height = 1 }; full.Add("potion");
        before = JsonSerializer.Serialize(grid);
        Check(!InventoryGrid.Transfer(grid, full, sword.Id, 0, 0) && JsonSerializer.Serialize(grid) == before && full.Items.Count == 1,
            "a full destination loses or duplicates no items");
        var chestGrid = new InventoryGrid { Height = 6 };
        Check(InventoryGrid.Transfer(grid, chestGrid, sword.Id, 3, 2) && !grid.Items.Contains(sword) && chestGrid.Items.Single().Id == sword.Id,
            "cross-container transfer preserves the item's identity");
        grid.Validate(); chestGrid.Validate();
        var doc = new MapDocument(MapData.CreateEmpty(16, 16), true);
        var chest = doc.PlaceChest(new(2, 0, 0)); string chestId = chest.Id;
        doc.Checkpoint(); foreach (var definition in ItemCatalog.All) chest.Contents.Add(definition.Id);
        doc.Map.Player.Inventory.Add("potion");
        doc.Map.Validate(); Check(doc.Dirty && !WorldRenderer.CanWalk(doc.Map, doc.Map.GroundPosition(chest)), "chests participate in map editing and collision");
        doc.Undo(); Check(doc.Map.Chests.Single().Contents.Items.Count == 0 && doc.Map.Player.Inventory.Items.Count == 0, "undo removes assigned loot and starting inventory");
        doc.Redo(); chest = doc.Map.Chests.Single();
        Check(chest.Contents.Items.Count == 5 && doc.Map.Player.Inventory.Items.Count == 1, "redo restores contents");
        var copy = doc.DuplicateChest(chest);
        Check(copy.Id != chest.Id && copy.Contents.Items.Select(i => i.Id).Intersect(chest.Contents.Items.Select(i => i.Id)).Count() == 0 &&
            copy.Contents.Items.Select(i => i.DefinitionId).SequenceEqual(chest.Contents.Items.Select(i => i.DefinitionId)), "duplicated chests receive independent item IDs");
        copy.Contents.Items.Clear(); Check(chest.Contents.Items.Count == 5, "duplicated chest inventory is independent");
        var play = new PlaySession(doc.Map);
        var runtimeChest = play.Map.Chests.First(c => c.Id == chestId);
        Check(play.InteractionTarget?.Id == chestId && play.OpenChest(runtimeChest), "nearby chest opens in Runtime");
        var runtimeItem = runtimeChest.Contents.Items.First();
        var spot = play.Inventory.FindSpace(runtimeItem.DefinitionId)!.Value;
        Check(InventoryGrid.Transfer(runtimeChest.Contents, play.Inventory, runtimeItem.Id, spot.X, spot.Y), "looting from a chest");
        Check(chest.Contents.Items.Count == 5 && doc.Map.Player.Inventory.Items.Count == 1, "playtest looting preserves the editor map");
        play.CloseInventory(); play.ToggleInventory(); Check(play.InventoryOpen && play.OpenedChest == null, "standalone inventory panel"); play.CloseInventory();
        runtimeChest.X = 7;
        Check(!play.OpenChest(runtimeChest), "opening a distant chest is rejected");
        string file = Path.Combine(Path.GetTempPath(), "wildwood-inventory-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            doc.Save(file); var loaded = MapData.Load(file);
            Check(!doc.Dirty && loaded.Serialize() == doc.Map.Serialize(), "save/load preserves chest contents and starting inventory");
            var legacy = JsonNode.Parse(MapData.CreateEmpty().Serialize())!;
            legacy.AsObject().Remove("Chests"); legacy["Player"]!.AsObject().Remove("Inventory");
            var older = MapData.Parse(legacy.ToJsonString());
            Check(older.Chests.Count == 0 && older.Player.Inventory.Items.Count == 0, "older maps load with empty inventories");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        foreach (Action<MapData> corrupt in new Action<MapData>[]
        {
            m => m.Player.Inventory.Items.Add(new() { DefinitionId = "unknown" }),
            m => { m.Player.Inventory.Add("armor"); m.Player.Inventory.Items.Add(new() { DefinitionId = "potion" }); },
            m => { var c = new ChestData(); m.Chests.Add(c); var i = m.Player.Inventory.Add("potion")!; c.Contents.Items.Add(new() { Id = i.Id }); },
            m => m.Chests.Add(new() { Id = m.Objects[0].Id }),
            m => m.Player.Inventory.Items.Add(new() { X = int.MaxValue, DefinitionId = "armor" })
        })
        {
            var map = MapData.CreateEmpty(); map.Objects.Add(new() { Asset = ChestData.DefaultAsset }); corrupt(map);
            try { map.Validate(); }
            catch (InvalidDataException) { continue; }
            throw new InvalidOperationException("Invalid inventory map accepted.");
        }
        Console.WriteLine("Inventory checks passed: footprints, placement, collision, transfers, full grids, chest duplication/range, undo/redo, save/load and playtest isolation.");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Inventory check failed: " + message); }
}
