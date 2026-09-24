public class Player
{
    public string? UserName { get; set; }
    public Guid ID { get; init; }

    public Unit? ActiveChar { get; set; }

    public int QuestsFinished { get; set; } = 0;
    public Objective? ActiveObjective { get; set; }
}
