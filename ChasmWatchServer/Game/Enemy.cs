/// <summary>
/// Hostile unit. It has no brain of its own: during its turn the tile's AI
/// (<see cref="WorldTile.RunEnemyTurns"/>) walks it toward the nearest player
/// and attacks whatever is adjacent.
/// </summary>
public class Enemy : Unit
{
    /// <summary>Health a freshly spawned enemy starts with.</summary>
    public const int EnemyHealth = 10;

    public bool IsHostile { get; set; } = true;

    public Enemy(int health = EnemyHealth, string? name = null)
        : base(health, DefaultSpeed, moves: [new HitD2()])
    {
        Name = name ?? "Husk";
    }

    public Enemy() : this(EnemyHealth)
    {
    }
}

/// <summary>Enemy attack: 1d2 damage.</summary>
public sealed class HitD2 : DamageMove
{
    public HitD2() : base("Hit (1d2)", die: 2) { }
}
