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

    /// <summary>
    /// Max hexes per room site. Rooms seeded adjacently merge into open space.
    /// </summary>
    public int RoomSize { get; set; } = 100;

    /// <summary>
    /// Max steps per corridor site between rooms. 0 disables corridors entirely,
    /// yielding open areas of merged rooms.
    /// </summary>
    public int CorridorLength { get; set; } = 0;
}
