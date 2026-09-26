/// <summary>
/// Per-region tuning for world-tile generation. Override per world coordinate
/// via <see cref="WorldMap.SetBiomeOverride"/> to create biomes (e.g. more
/// dangerous regions) without touching generation code.
/// </summary>
public class SpawnSettings
{
    public double Openness { get; set; } = 0.5;
    public double EnemySpawnChance { get; set; } = 0.06;
    public int MaxEnemies { get; set; } = 8;
}
