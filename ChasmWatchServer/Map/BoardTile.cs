public class BoardTile : HexNode
{
    public Entity? Occupant { get; set; }
    public bool IsWorldExit { get; set; } = false;
    public HexDirection? WorldExitDirection { get; set; } = null;
    public bool Interactable { get; set; } = false;

    /// <summary>
    /// Growth site that created this tile. Positive = room site, negative =
    /// corridor site (magnitude = segment index). Diagnostics only; never sent
    /// to clients.
    /// </summary>
    internal int SiteId { get; set; } = 0;

    public Dictionary<HexDirection, bool> exits = new()
    {
        { HexDirection.UP,        false },
        { HexDirection.DOWN,      false },
        { HexDirection.LEFTUP,    false },
        { HexDirection.RIGHTUP,   false },
        { HexDirection.LEFTDOWN,  false },
        { HexDirection.RIGHTDOWN, false },
    };

    public BoardTile(HexagonalPos position, bool pathable, bool occupied)
        : base(position, pathable, occupied)
    {
        this.Position = position;
        this.Pathable = pathable;
        this.Occupied = occupied;
    }
}
