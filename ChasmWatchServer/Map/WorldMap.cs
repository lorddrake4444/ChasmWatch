using System.Collections.Concurrent;

public class WorldMap
{
    private ConcurrentDictionary<HexagonalPos, WorldTile> Map = new ConcurrentDictionary<HexagonalPos, WorldTile>();
    private ConcurrentDictionary<HexagonalPos, Biome> BiomeOverrides = new ConcurrentDictionary<HexagonalPos, Biome>();
    private readonly List<BiomeRegion> RegionRules = new();
    private readonly Dictionary<string, Biome> Biomes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fallback biome for tiles without a biome override or region match.
    /// Roughly 1 in 10 default tiles rolls rotten, lifting the enemy cap there.
    /// </summary>
    public Biome DefaultBiome { get; set; } = new Biome("Default", new SpawnSettings
    {
        RottenTileChance = 0.1,
    });

    /// <summary>
    /// Deep-south biome: tight enclosed corridors, fewer enemies. Applies to
    /// any world tile south of <see cref="DeepSouthBoundaryR"/>.
    /// </summary>
    public Biome DeepSouthBiome { get; set; } = new Biome("Deep Warrens", new SpawnSettings
    {
        Openness = 0.2,
        EnemySpawnChance = 0.02,
        MaxEnemies = 3,
        RoomSize = 10,
        CorridorLength = 50,
    });

    /// <summary>World tiles with r greater than this are "south". +r projects straight
    /// down on screen (DOWN = +1 r), so r &gt; 10 means south of 10 units.</summary>
    public int DeepSouthBoundaryR { get; set; } = 10;

    /// <summary>Back-compat view of <see cref="DefaultBiome"/>'s tuning.</summary>
    public SpawnSettings DefaultSpawn
    {
        get => DefaultBiome.Settings;
        set => DefaultBiome.Settings = value;
    }

    /// <summary>Back-compat view of <see cref="DeepSouthBiome"/>'s tuning.</summary>
    public SpawnSettings DeepSouthSpawn
    {
        get => DeepSouthBiome.Settings;
        set => DeepSouthBiome.Settings = value;
    }

    public WorldMap()
    {
        AddBiome(DefaultBiome);
        AddBiome(DeepSouthBiome);
        RegionRules.Add(new BiomeRegion("Deep Warrens", p => p.r > DeepSouthBoundaryR, DeepSouthBiome));
    }

    /// <summary>Registers a biome by name so it can be looked up later.</summary>
    public Biome AddBiome(Biome biome)
    {
        Biomes[biome.Name] = biome;
        return biome;
    }

    public bool TryGetBiome(string name, out Biome? biome) =>
        Biomes.TryGetValue(name, out biome);

    /// <summary>Pin a per-tile biome; beats region rules.</summary>
    public void SetBiomeOverride(HexagonalPos worldPos, Biome biome) =>
        BiomeOverrides[worldPos] = biome;

    /// <summary>Back-compat overload wrapping tuning in a one-off biome.</summary>
    public void SetBiomeOverride(HexagonalPos worldPos, SpawnSettings settings) =>
        SetBiomeOverride(worldPos, new Biome("Override", settings));

    public bool ClearBiomeOverride(HexagonalPos worldPos) =>
        BiomeOverrides.TryRemove(worldPos, out _);

    public void AddRegionRule(BiomeRegion region) =>
        RegionRules.Add(region);

    /// <summary>Resolves the biome governing a world tile: pinned override, then
    /// the first matching region rule, then <see cref="DefaultBiome"/>.</summary>
    public Biome GetBiomeFor(HexagonalPos pos)
    {
        if (BiomeOverrides.TryGetValue(pos, out var pinned))
            return pinned;
        foreach (var region in RegionRules)
        {
            if (region.Matches(pos))
                return region.Biome;
        }
        return DefaultBiome;
    }

    /// <summary>Back-compat view of the resolved biome's tuning.</summary>
    public SpawnSettings GetSettingsFor(HexagonalPos pos) => GetBiomeFor(pos).Settings;

    public WorldTile GetTile(HexagonalPos pos)
    {
        return Map.GetOrAdd(pos, p =>
        {
            var tile = new WorldTile(p.Copy());
            tile.Generate(GetBiomeFor(p));
            tile.Active = true;
            return tile;
        });
    }

    public bool TryGetTile(HexagonalPos pos, out WorldTile? tile) =>
        Map.TryGetValue(pos, out tile);
}
