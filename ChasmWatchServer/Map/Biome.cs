/// <summary>
/// A biome bundles generation tuning with optional generation hooks. A biome
/// owns its own shape/exits/enemy logic without forcing a class hierarchy:
/// any hook left null falls back to the default <see cref="WorldTile"/> step.
/// </summary>
public class Biome
{
    public string Name { get; }

    /// <summary>
    /// Tuning for the default hooks and for anything reading
    /// <see cref="WorldMap.GetSettingsFor"/>.
    /// </summary>
    public SpawnSettings Settings { get; set; }

    /// <summary>Carves the board shape. Null uses <see cref="WorldTile.GenerateShapeDefault"/>.</summary>
    public GenerateShapeHook? GenerateShape { get; set; }

    /// <summary>Places world exits. Null uses <see cref="WorldTile.PlaceWorldExitsDefault"/>.</summary>
    public PlaceWorldExitsHook? PlaceWorldExits { get; set; }

    /// <summary>Seeds enemies. Null uses <see cref="WorldTile.SpawnEnemiesDefault"/>.</summary>
    public SpawnEnemiesHook? SpawnEnemies { get; set; }

    public Biome(string name, SpawnSettings? settings = null)
    {
        Name = name;
        Settings = settings ?? new SpawnSettings();
    }
}

/// <summary>
/// Outcome of a biome's shape-generation step, handed back by
/// <see cref="WorldTile.Generate(Biome, int?)"/> so callers can inspect what
/// was produced without poking at the tile's internals.
/// </summary>
public sealed record GenerationResult(int Seed, HexagonalPos Entrance, int Tiles);

/// <summary>
/// Region of the world map that resolves to a <see cref="Biome"/>. The first
/// matching rule wins; see <see cref="WorldMap.GetBiomeFor"/>.
/// </summary>
public sealed record BiomeRegion(string Name, Func<HexagonalPos, bool> Matches, Biome Biome)
{
    /// <summary>Convenience overload wrapping tuning in an unnamed biome.</summary>
    public BiomeRegion(string name, Func<HexagonalPos, bool> matches, SpawnSettings settings)
        : this(name, matches, new Biome(name, settings)) { }
}
