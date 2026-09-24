using System.ComponentModel.DataAnnotations;

public class Unit(int health, int speed, List<Move>? moves = null) : Entity
{
    public int Health { get; set; } = health;
    public int Speed { get; set; } = speed;
    public List<Move>? Moves { get; set; } = moves;

    /// <summary>Board-local position within <see cref="CurrentTile"/>.</summary>
    public HexagonalPos CurrentBoardPos { get; set; } = new(0, 0, 0);

    /// <summary>World-tile position this unit currently occupies, if any.</summary>
    public HexagonalPos? CurrentTile { get; set; }

    public Unit() : this(health: 100, speed: 3, moves: null)
    {
    }
}

public abstract class Move(string name)
{
    [Required] public string Name { get; set; } = name;

    public abstract void Execute(String move_params);
}
