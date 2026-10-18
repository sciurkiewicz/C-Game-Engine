using System.Text.Json.Serialization;

sealed partial class MapData
{
    public const int TerrainChunkSize = 16;
    long terrainGeneration;
    readonly Dictionary<(int X, int Z), long> terrainChunkVersions = [];
    [JsonIgnore] public long TerrainGeneration => terrainGeneration;
    public long TerrainChunkVersion(int x, int z) => terrainChunkVersions.GetValueOrDefault((x, z));
    void TouchTerrainVertex(int x, int z)
    {
        foreach (int cx in new[] { (x - 1) / TerrainChunkSize, x / TerrainChunkSize })
        foreach (int cz in new[] { (z - 1) / TerrainChunkSize, z / TerrainChunkSize })
            if (cx >= 0 && cz >= 0 && cx * TerrainChunkSize < Width && cz * TerrainChunkSize < Depth)
                terrainChunkVersions[(cx, cz)] = terrainChunkVersions.GetValueOrDefault((cx, cz)) + 1;
    }
}
