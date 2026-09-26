using System.Collections.Concurrent;

public class WorldMap
{
    private ConcurrentDictionary<HexagonalPos, WorldTile> Map = new ConcurrentDictionary<HexagonalPos, WorldTile>();
    private ConcurrentDictionary<HexagonalPos, SpawnSettings> BiomeOverrides = new ConcurrentDictionary<HexagonalPos, SpawnSettings>();

    /// <summary>Fallback generation tuning for tiles without a biome override.</summary>
    public SpawnSettings DefaultSpawn { get; set; } = new SpawnSettings();

    /// <summary>Pin per-region generation tuning (dangerous biomes, etc.).</summary>
    public void SetBiomeOverride(HexagonalPos worldPos, SpawnSettings settings) =>
        BiomeOverrides[worldPos] = settings;

    public bool ClearBiomeOverride(HexagonalPos worldPos) =>
        BiomeOverrides.TryRemove(worldPos, out _);

    public WorldTile GetTile(HexagonalPos pos)
    {
        return Map.GetOrAdd(pos, p =>
        {
            var settings = BiomeOverrides.TryGetValue(p, out var biome) ? biome : DefaultSpawn;
            var tile = new WorldTile(p.Copy());
            tile.Generate(settings);
            tile.Active = true;
            return tile;
        });
    }

    public bool TryGetTile(HexagonalPos pos, out WorldTile? tile) =>
        Map.TryGetValue(pos, out tile);
}
