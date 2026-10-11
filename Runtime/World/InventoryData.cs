using System.Numerics;
using Raylib_cs;

sealed record ItemDefinition(string Id, string Name, int Width, int Height, Color Color);

static class ItemCatalog
{
    public static readonly IReadOnlyList<ItemDefinition> All = new ItemDefinition[]
    {
        new("potion", "Mikstura", 1, 1, new(229, 86, 91, 255)),
        new("dagger", "Sztylet", 1, 2, new(73, 154, 233, 255)),
        new("sword", "Miecz", 1, 3, new(92, 197, 126, 255)),
        new("armor", "Zbroja", 2, 2, new(235, 177, 61, 255)),
        new("shield", "Tarcza", 2, 3, new(166, 116, 231, 255))
    };
    public static ItemDefinition? Find(string id) => All.FirstOrDefault(item => item.Id == id);
}

sealed class InventoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DefinitionId { get; set; } = "potion";
    public int X { get; set; }
    public int Y { get; set; }
}

sealed class InventoryGrid
{
    public int Width { get; set; } = 10;
    public int Height { get; set; } = 4;
    public List<InventoryItem> Items { get; set; } = [];
    public bool CanPlace(string definitionId, int x, int y, string? ignoreId = null)
    {
        var definition = ItemCatalog.Find(definitionId);
        if (definition == null || x < 0 || y < 0 || x > Width - definition.Width || y > Height - definition.Height) return false;
        return !Items.Where(item => item.Id != ignoreId).Any(item =>
        {
            var other = ItemCatalog.Find(item.DefinitionId)!;
            return x < item.X + other.Width && x + definition.Width > item.X && y < item.Y + other.Height && y + definition.Height > item.Y;
        });
    }
    public (int X, int Y)? FindSpace(string definitionId, string? ignoreId = null)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (CanPlace(definitionId, x, y, ignoreId)) return (x, y);
        return null;
    }
    public InventoryItem? Add(string definitionId)
    {
        if (FindSpace(definitionId) is not { } space) return null;
        var item = new InventoryItem { DefinitionId = definitionId, X = space.X, Y = space.Y };
        Items.Add(item); return item;
    }
    public static bool Transfer(InventoryGrid source, InventoryGrid target, string itemId, int x, int y)
    {
        var item = source.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || (!ReferenceEquals(source, target) && target.Items.Any(i => i.Id == itemId)) ||
            !target.CanPlace(item.DefinitionId, x, y, ReferenceEquals(source, target) ? item.Id : null)) return false;
        if (!ReferenceEquals(source, target)) { source.Items.Remove(item); target.Items.Add(item); }
        item.X = x; item.Y = y; return true;
    }
    public void Validate()
    {
        if (Width < 1 || Width > 20 || Height < 1 || Height > 20 || Items == null || Items.Count > Width * Height ||
            Items.Any(i => i == null || string.IsNullOrWhiteSpace(i.Id) || ItemCatalog.Find(i.DefinitionId) == null) ||
            Items.Select(i => i.Id).Distinct().Count() != Items.Count)
            throw new InvalidDataException("Invalid inventory grid or item.");
        foreach (var item in Items)
            if (!CanPlace(item.DefinitionId, item.X, item.Y, item.Id)) throw new InvalidDataException("Inventory items overlap or extend outside the grid.");
    }
}

sealed class ChestData : SceneObject
{
    public const string DefaultAsset = "objects/props/containers/chest.png";
    public string Name { get; set; } = "Skrzynia";
    public InventoryGrid Contents { get; set; } = new() { Height = 6 };
    public ChestData() { Asset = DefaultAsset; Height = 1.4f; }
}

sealed partial class MapData
{
    public List<ChestData> Chests { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<SceneObject> SceneEntities => Objects.Concat<SceneObject>(Npcs).Concat(Chests).Concat(Monsters);
    void ValidateInventory()
    {
        if (Player.Inventory == null || Chests == null || Chests.Count > 1000 || Chests.Any(c => c == null ||
            string.IsNullOrWhiteSpace(c.Id) || !ProjectPaths.ValidAssetId(c.Asset) || !Contains(c.X, c.Z) || !ValidHeight(c.Height) ||
            string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 80 || c.Contents == null) ||
            SceneEntities.Select(o => o.Id).Distinct().Count() != Objects.Count + Npcs.Count + Chests.Count + Monsters.Count)
            throw new InvalidDataException("Invalid chests or player inventory.");
        Player.Inventory.Validate();
        foreach (var chest in Chests) chest.Contents.Validate();
        var items = Player.Inventory.Items.Concat(Chests.SelectMany(c => c.Contents.Items)).ToArray();
        if (items.Select(i => i.Id).Distinct().Count() != items.Length) throw new InvalidDataException("Inventory item IDs must be unique across containers.");
    }
}

sealed partial class MapDocument
{
    public ChestData PlaceChest(Vector3 position)
    {
        Checkpoint(); var p = Map.Clamp(position);
        var chest = new ChestData { X = p.X, Z = p.Z };
        Map.Chests.Add(chest); return chest;
    }
    public ChestData ConvertToChest(SceneObject item)
    {
        if (!Map.Objects.Contains(item)) throw new ArgumentException("Select a scene object.");
        Checkpoint();
        var chest = new ChestData { Id = item.Id, Asset = item.Asset, X = item.X, Z = item.Z, Height = item.Height, Solid = item.Solid };
        Map.Objects.Remove(item); Map.Chests.Add(chest); return chest;
    }
    public ChestData DuplicateChest(ChestData source)
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<ChestData>(System.Text.Json.JsonSerializer.Serialize(source))!;
        Checkpoint(); copy.Id = Guid.NewGuid().ToString("N");
        foreach (var item in copy.Contents.Items) item.Id = Guid.NewGuid().ToString("N");
        var p = Map.Clamp(source.Position + new Vector3(1, 0, 1)); copy.X = p.X; copy.Z = p.Z;
        Map.Chests.Add(copy); return copy;
    }
}
