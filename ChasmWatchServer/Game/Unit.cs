using System.ComponentModel.DataAnnotations;

public class Unit(int health, int speed, List<Move>? moves = null) :Entity 
{
    public int Health { get; set; } = health;
    public int Speed { get; set; } = speed;
    public List<Move>? Moves { get; set; } = moves;
    public HexagonalPos CurrentWorldPos { get; set; } = new(0,0,0);
}

public abstract class Move(string name)
{
    [Required] public string Name { get; set; } = name;

    public abstract void Execute(String move_params);
}