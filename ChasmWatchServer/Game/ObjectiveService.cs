/// <summary>
/// Per-player objective quests: a random world tile is chosen as the target,
/// the player is guided there via exit hints, an interactable hex is revealed
/// on arrival, and interacting completes the quest.
/// </summary>
public class ObjectiveService
{
    private readonly WorldMap _worldMap;
    private readonly IPlayerService _playerService;
    private readonly Random _rand = new();

    public ObjectiveService(WorldMap worldMap, IPlayerService playerService)
    {
        _worldMap = worldMap;
        _playerService = playerService;
    }

    /// <summary>
    /// Assigns a fresh objective <paramref name="distanceMin"/>–<paramref name="distanceMax"/>
    /// world tiles away from <paramref name="fromWorldPos"/> and stores it on the player.
    /// </summary>
    public Objective AssignObjective(Player player, HexagonalPos fromWorldPos, int distanceMin = 1, int distanceMax = 3)
    {
        HexagonalPos target = PickRandomTileAtDistance(fromWorldPos, distanceMin, distanceMax);
        _worldMap.GetTile(target); // generate eagerly so the target exists
        var objective = new Objective(player.ID, target);
        _playerService.UpdatePlayer(player.ID, p => p.ActiveObjective = objective);
        return objective;
    }

    /// <summary>
    /// Greedy best exit towards the target: the direction whose neighbor is
    /// closest to <paramref name="to"/>. Null when already there.
    /// </summary>
    public HexDirection? GetHintDirection(HexagonalPos from, HexagonalPos to)
    {
        if (from.Equals(to))
            return null;
        HexDirection best = HexDirection.UP;
        int bestDistance = int.MaxValue;
        foreach (HexDirection direction in Enum.GetValues<HexDirection>())
        {
            int distance = HexagonalPos.HexDistance(from + direction.ToOffset(), to);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = direction;
            }
        }
        return best;
    }

    public string BuildHintMessage(HexagonalPos from, HexagonalPos to)
    {
        HexDirection? hint = GetHintDirection(from, to);
        if (hint == null)
            return $"Objective reached — tile {to.q},{to.r},{to.s}. Find the marked [!] hex and interact (E).";
        int remaining = HexagonalPos.HexDistance(from, to);
        return $"Objective: tile {to.q},{to.r},{to.s} ({remaining} away). Take the {hint} exit to get closer.";
    }

    /// <summary>
    /// Reveals the interactable hex when the owner's unit stands on the
    /// objective tile. Returns a message for chat, or null when nothing happens.
    /// </summary>
    public string? CheckArrival(WorldTile tile, Player player, Unit unit)
    {
        Objective? objective = player.ActiveObjective;
        if (objective == null || objective.InteractableBoardPos != null)
            return null;
        if (unit.CurrentTile == null || !unit.CurrentTile.Equals(objective.TargetTile))
            return null;

        HexagonalPos? revealed = tile.RevealInteractable(_rand);
        if (revealed == null)
            return "Objective reached, but nothing here responds. (No free hex to mark.)";

        HexagonalPos revealedCopy = revealed.Copy();
        _playerService.UpdatePlayer(player.ID, p =>
        {
            if (p.ActiveObjective?.Id == objective.Id)
                p.ActiveObjective.InteractableBoardPos = revealedCopy;
        });
        return $"Objective reached — an interactable [!] hex is marked at {revealedCopy.q},{revealedCopy.r},{revealedCopy.s}. Stand on it and interact (E).";
    }

    public enum InteractOutcome
    {
        NotOnTile,
        NothingHere,
        NotYours,
        Completed,
        EnemyKilled,
    }

    public sealed record InteractResult(InteractOutcome Outcome, string Message, Objective? NextObjective = null);

    /// <summary>
    /// Generic positional interaction. Quest hexes take priority; otherwise an
    /// adjacent enemy is killed (deleted). Only the objective owner can complete
    /// their objective; completion clears the flag, increments the counter, and
    /// starts the next quest from the player's current position.
    /// </summary>
    public InteractResult Interact(Player player, Unit unit, HexagonalPos claimedWorldPos)
    {
        if (unit.CurrentTile == null || !unit.CurrentTile.Equals(claimedWorldPos))
            return new InteractResult(InteractOutcome.NotOnTile, "You are not on that tile.");

        if (!_worldMap.TryGetTile(claimedWorldPos, out WorldTile? tile) || tile == null)
            return new InteractResult(InteractOutcome.NotOnTile, "You are not on that tile.");

        if (!tile.IsOnInteractable(unit))
        {
            Enemy? slain = tile.TryKillAdjacentEnemy(unit);
            if (slain != null)
                return new InteractResult(InteractOutcome.EnemyKilled, $"{slain.Name} destroyed. ({tile.CountEnemies()} remain on this tile.)");
            return new InteractResult(InteractOutcome.NothingHere, "Nothing to interact with here.");
        }

        Objective? objective = player.ActiveObjective;
        if (objective == null || !claimedWorldPos.Equals(objective.TargetTile))
            return new InteractResult(InteractOutcome.NotYours, "It responds only to the one it called for.");

        tile.TryConsumeInteractable(unit);
        Objective? next = null;
        _playerService.UpdatePlayer(player.ID, p =>
        {
            p.QuestsFinished++;
            next = new Objective(p.ID, PickRandomTileAtDistance(claimedWorldPos, 1, 3));
            p.ActiveObjective = next;
        });
        _worldMap.GetTile(next!.TargetTile);

        return new InteractResult(
            InteractOutcome.Completed,
            $"Quest complete! ({player.QuestsFinished} finished.) {BuildHintMessage(claimedWorldPos, next.TargetTile)}",
            next);
    }

    private HexagonalPos PickRandomTileAtDistance(HexagonalPos from, int min, int max)
    {
        int distance = _rand.Next(min, max + 1);
        while (true)
        {
            int q = _rand.Next(-distance, distance + 1);
            int r = _rand.Next(-distance, distance + 1);
            int s = -q - r;
            var offset = new HexagonalPos(r, q, s);
            if (HexagonalPos.HexDistance(new HexagonalPos(0, 0, 0), offset) == distance)
                return from + offset;
        }
    }
}
