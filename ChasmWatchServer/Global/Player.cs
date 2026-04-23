using System.ComponentModel.Design;

public class Player
{
    public string? UserName { get; set; }
    public Guid ID { get; init; }

    public Unit? ActiveChar {get; set;}
}