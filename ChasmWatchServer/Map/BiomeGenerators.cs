/// <summary>
/// Shape-generation strategy for a <see cref="Biome"/>. Receives the tile being
/// generated so custom strategies can carve the board directly.
/// </summary>
public delegate GenerationResult GenerateShapeHook(WorldTile tile, Random rand, SpawnSettings settings, int seed);

/// <summary>World-exit placement strategy for a <see cref="Biome"/>.</summary>
public delegate void PlaceWorldExitsHook(WorldTile tile, Random rand);

/// <summary>Enemy-seeding strategy for a <see cref="Biome"/>.</summary>
public delegate void SpawnEnemiesHook(WorldTile tile, Random rand, SpawnSettings settings);

/// <summary>
/// Default strategy delegates that defer to <see cref="WorldTile"/>'s built-in
/// generation steps. Assign these to a biome when only some hooks are
/// customized.
/// </summary>
public static class BiomeGenerators
{
    public static GenerateShapeHook DefaultShape { get; } =
        static (tile, rand, settings, seed) => tile.GenerateShapeDefault(rand, settings, seed);

    public static PlaceWorldExitsHook DefaultWorldExits { get; } =
        static (tile, rand) => tile.PlaceWorldExitsDefault(rand);

    public static SpawnEnemiesHook DefaultSpawnEnemies { get; } =
        static (tile, rand, settings) => tile.SpawnEnemiesDefault(rand, settings);
}
