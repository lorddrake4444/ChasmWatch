public abstract class HexNode
{
    public string Name;
    public HexagonalPos Position { get; set; }
    public bool Pathable { get; set; }
    public bool Occupied { get; set; }
    
    public HexNode(HexagonalPos position, bool pathable, bool occupied , string name) 
    {
        this.Position = position;
        this.Pathable = pathable;
        this.Occupied = occupied;
        this.Name = name;
    }
}