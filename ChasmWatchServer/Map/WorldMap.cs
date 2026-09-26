using System.Collections.Concurrent;

public sealed record BiomeRegion(string Name, Func<HexagonalPos, bool> Matches, SpawnSettings Settings);

public class WorldMap
{
    private ConcurrentDictionary<HexagonalPos, WorldTile> Map = new ConcurrentDictionary<HexagonalPos, WorldTile>();
    private ConcurrentDictionary<HexagonalPos, SpawnSettings> BiomeOverrides = new ConcurrentDictionary<HexagonalPos, SpawnSettings>();
    private readonly List<BiomeRegion> RegionRules = new();

    /// <summary>Fallback generation tuning for tiles without a biome override.</summary>
    public SpawnSettings DefaultSpawn { get; set; } = new SpawnSettings();

    /// <summary>
    /// Deep-south tuning: tight enclosed corridors, fewer enemies.
    /// Applies to any world tile south of <see cref="DeepSouthBoundaryR"/>.
    /// </summary>
    public SpawnSettings DeepSouthSpawn { get; set; } = new SpawnSettings
    {
        Openness = 0.2,
        EnemySpawnChance = 0.02,
        MaxEnemies = 3,
        RoomSize = 10,
        CorridorLength = 50,
    };

    /// <summary>
    /// World tiles with r greater than this are "south". +r projects straight
    /// down on screen (DOWN = +1 r), so r &gt; 10 means south of 10 units.
    /// </summary>
    public int DeepSouthBoundaryR { get; set; } = 10;

    public WorldMap()
    {
        RegionRules.Add(new BiomeRegion("Deep Warrens", p => p.r > DeepSouthBoundaryR, DeepSouthSpawn));
    }

    /// <summary>Pin per-tile generation tuning; beats region rules.</summary>
    public void SetBiomeOverride(HexagonalPos worldPos, SpawnSettings settings) =>
        BiomeOverrides[worldPos] = settings;

    public bool ClearBiomeOverride(HexagonalPos worldPos) =>
        BiomeOverrides.TryRemove(worldPos, out _);

    public void AddRegionRule(BiomeRegion region) =>
        RegionRules.Add(region);

    public SpawnSettings GetSettingsFor(HexagonalPos pos)
    {
        if (BiomeOverrides.TryGetValue(pos, out var pinned))
            return pinned;
        foreach (var region in RegionRules)
        {
            if (region.Matches(pos))
                return region.Settings;
        }
        return DefaultSpawn;
    }

    public WorldTile GetTile(HexagonalPos pos)
    {
        return Map.GetOrAdd(pos, p =>
        {
            var tile = new WorldTile(p.Copy());
            tile.Generate(GetSettingsFor(p));
            tile.Active = true;
            return tile;
        });
    }

    public bool TryGetTile(HexagonalPos pos, out WorldTile? tile) =>
        Map.TryGetValue(pos, out tile);
}
