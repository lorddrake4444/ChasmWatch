using System.ComponentModel.DataAnnotations;

public class Unit(int health, int speed, List<Move>? moves = null) : Entity
{
    /// <summary>Health a player character starts — and respawns — with.</summary>
    public const int PlayerHealth = 50;

    /// <summary>Cells a unit can cross with a single move action.</summary>
    public const int DefaultSpeed = 3;

    public int Health { get; set; } = health;
    public int Speed { get; set; } = speed;
    public List<Move>? Moves { get; set; } = moves;

    /// <summary>Board-local position within <see cref="CurrentTile"/>.</summary>
    public HexagonalPos CurrentBoardPos { get; set; } = new(0, 0, 0);

    /// <summary>World-tile position this unit currently occupies, if any.</summary>
    public HexagonalPos? CurrentTile { get; set; }

    /// <summary>This unit's basic attack move, if it has one.</summary>
    public DamageMove? Attack => Moves?.OfType<DamageMove>().FirstOrDefault();

    /// <summary>Rolls this unit's attack damage (0 when it has no attack move).</summary>
    public int RollAttackDamage(Random rand) => Attack?.Roll(rand) ?? 0;

    public Unit() : this(health: PlayerHealth, speed: DefaultSpeed, moves: [new HitD4()])
    {
    }
}

public abstract class Move(string name)
{
    [Required] public string Name { get; set; } = name;

    /// <summary>
    /// Runs the move's effect. Does nothing by default, so a plain damaging move
    /// only has to describe its die; override it as moves grow behaviour.
    /// </summary>
    public virtual void Execute(string move_params) { }
}

/// <summary>
/// A move that hits for <c>1d<see cref="Die"/></c>. The caller that knows the
/// target rolls it and applies the result.
/// </summary>
public class DamageMove(string name, int die) : Move(name)
{
    /// <summary>Faces on the damage die (4 for 1d4).</summary>
    public int Die { get; } = die;

    /// <summary>Rolls the move's damage, 1 through <see cref="Die"/>.</summary>
    public int Roll(Random rand) => rand.Next(1, Die + 1);
}

/// <summary>Player attack: 1d4 damage.</summary>
public sealed class HitD4 : DamageMove
{
    public HitD4() : base("Hit (1d4)", die: 4) { }
}
