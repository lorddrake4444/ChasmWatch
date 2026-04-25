public class BoardTile : HexNode
{
    public Entity? Occupant { get; set; }
    public bool IsWorldExit { get; set; } = false;
    public string? WorldExitDirection { get; set; } = null;

    public Dictionary<string, bool> exits = new()
    {
        { "Up",        false },
        { "Down",      false },
        { "UpLeft",    false },
        { "UpRight",   false },
        { "DownLeft",  false },
        { "DownRight", false },
    };

    public BoardTile(HexagonalPos position, bool pathable, bool occupied)
        : base(position, pathable, occupied)
    {
        this.Position = position;
        this.Pathable = pathable;
        this.Occupied = occupied;
    }
}