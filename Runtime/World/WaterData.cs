using System.Numerics;

enum WaterEditResult { Filled, Drained, NoBasin, Unchanged }

sealed partial class MapData
{
    // Each bit selects one of the two terrain triangles. Water is independent of its bed material.
    public byte[] WaterCells { get; set; } = [];
    public byte WaterMask(int cell) => WaterCells.Length > 0 ? WaterCells[cell] : Tiles[cell] == "water" ? (byte)3 : (byte)0;
    public byte[] CopyWaterCells()
    {
        if (WaterCells.Length > 0) return (byte[])WaterCells.Clone();
        if (!Tiles.Contains("water")) return [];
        return Tiles.Select(t => t == "water" ? (byte)3 : (byte)0).ToArray();
    }
    void ValidateWater()
    {
        if (WaterCells == null || (WaterCells.Length != 0 && WaterCells.Length != Width * Depth) || WaterCells.Any(mask => mask > 3))
            throw new InvalidDataException("Invalid water area.");
    }
}

sealed partial class MapDocument
{
    void UpdateChangedWater()
    {
        changedWater = 0;
        if (Map.Width != savedWidth || Map.Depth != savedDepth) return;
        for (int i = 0; i < Map.Tiles.Length; i++)
            if (Map.WaterMask(i) != (savedWater.Length == 0 ? 0 : savedWater[i])) changedWater++;
    }
    void ChangeWater(int cell, byte mask)
    {
        byte previous = Map.WaterMask(cell);
        if (previous == mask) return;
        if (Map.WaterCells.Length == 0)
        {
            var water = Map.CopyWaterCells();
            Map.WaterCells = water.Length > 0 ? water : new byte[Map.Tiles.Length];
        }
        if (savedTiles != null && Map.Width == savedWidth && Map.Depth == savedDepth)
        {
            byte original = savedWater.Length == 0 ? (byte)0 : savedWater[cell];
            changedWater += (mask != original ? 1 : 0) - (previous != original ? 1 : 0);
        }
        Map.WaterCells[cell] = mask;
    }
    public WaterEditResult FillWater(Vector3 point, bool erase = false)
    {
        var region = WaterFill.Find(Map, point, erase);
        if (region.Cells.Count == 0) return WaterEditResult.NoBasin;
        if (!region.Cells.Any(i => erase ? (Map.WaterMask(i) & region.Masks[i]) != 0 : (Map.WaterMask(i) | region.Masks[i]) != Map.WaterMask(i)))
            return WaterEditResult.Unchanged;
        Checkpoint();
        foreach (int cell in region.Cells)
            ChangeWater(cell, erase ? (byte)(Map.WaterMask(cell) & ~region.Masks[cell]) : (byte)(Map.WaterMask(cell) | region.Masks[cell]));
        return erase ? WaterEditResult.Drained : WaterEditResult.Filled;
    }
    public void SetWaterLevel(float level)
    {
        level = MapData.SnapElevation(Math.Clamp(level, MapData.MinElevation, MapData.MaxElevation));
        if (level == Map.WaterLevel) return;
        var region = WaterFill.Expand(Map, level);
        Checkpoint(); Map.WaterLevel = level;
        foreach (int cell in region.Cells) ChangeWater(cell, (byte)(Map.WaterMask(cell) | region.Masks[cell]));
    }
}
