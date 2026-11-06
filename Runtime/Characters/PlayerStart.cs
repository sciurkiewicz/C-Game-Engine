using System.Numerics;
using System.Text.Json.Serialization;

// Map-specific appearance, starting position and equipment; shared stats live in player.json.
sealed class PlayerStart
{
    public float X { get; set; }
    public float Z { get; set; }
    public string? Asset { get; set; }
    public float Height { get; set; } = 1.6f;
    public InventoryGrid Inventory { get; set; } = new();
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
}
