using System.ComponentModel.DataAnnotations;

public class Unit(string name, int health, int speed, List<Move>? moves = null)
{
    [Required] public string Name { get; set; } = name;
    public int Health { get; set; } = health;
    public int Speed { get; set; } = speed;
    public List<Move>? Moves { get; set; } = moves;
}

public abstract class Move(string name)
{
    [Required] public string Name { get; set; } = name;

    public abstract void Execute(String move_params);
}