/// <summary>
/// A per-player quest. The objective lives at a world-tile coordinate; once
/// the owner arrives, a single interactable board hex is revealed there.
/// </summary>
public class Objective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerPlayerId { get; set; }
    public HexagonalPos TargetTile { get; set; } = new(0, 0, 0);

    /// <summary>Board hex of the revealed interactable, if revealed yet.</summary>
    public HexagonalPos? InteractableBoardPos { get; set; }

    public Objective(Guid ownerPlayerId, HexagonalPos targetTile)
    {
        OwnerPlayerId = ownerPlayerId;
        TargetTile = targetTile.Copy();
    }
}
