/// <summary>
/// Stationary hostile unit. Enemies do nothing for now: with <see cref="Unit.Speed"/>
/// fixed at 0 they block movement and pathing purely by occupying their hex.
/// </summary>
public class Enemy : Unit
{
    public bool IsHostile { get; set; } = true;

    public Enemy(int health = 30, string? name = null) : base(health, 0)
    {
        Name = name ?? "Husk";
    }

    public Enemy() : this(30)
    {
    }
}
