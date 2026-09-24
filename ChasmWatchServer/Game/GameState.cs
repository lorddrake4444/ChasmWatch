public sealed record TraverseResult(
    WorldTile OldTile,
    WorldTile NewTile,
    HexagonalPos NeighborPos,
    HexDirection ExitDirection);

public class GameState
{
    public WorldMap _WorldMap { get; }
    public GameState(WorldMap worldMap)
    {
        _WorldMap = worldMap;
    }

    /// <summary>
    /// Places <paramref name="unit"/> on <paramref name="worldPos"/>. If the unit
    /// already occupies a different world tile it is removed from there first so
    /// no ghost occupants are left behind.
    /// </summary>
    public WorldTile AddUnitToTile(HexagonalPos worldPos, HexDirection? entryDirection, Unit unit)
    {
        WorldTile tile = _WorldMap.GetTile(worldPos);

        if (entryDirection != null)
        {
            HexDirection opposite = entryDirection.Value.Opposite();
            if (!tile.WorldExits.TryGetValue(opposite, out var exits))
                throw new InvalidOperationException($"No world exits found for direction {opposite}");

            if (tile.TryPlaceUnitAtEntry(exits, unit) == null)
                throw new InvalidOperationException("All entry tiles are occupied");
        }
        else
        {
            if (!tile.TryPlaceUnit(new HexagonalPos(0, 0, 0), unit))
                throw new InvalidOperationException("Center tile is occupied or missing");
        }

        // Placement succeeded: clear any ghost on the previous world tile.
        // Scan-based removal still finds the unit even though CurrentBoardPos
        // was just overwritten to the new tile.
        if (unit.CurrentTile != null && !unit.CurrentTile.Equals(worldPos)
            && _WorldMap.TryGetTile(unit.CurrentTile, out var previousTile)
            && previousTile != null && !ReferenceEquals(previousTile, tile))
        {
            previousTile.RemoveUnit(unit);
        }

        unit.CurrentTile = tile.Position.Copy();
        return tile;
    }

    /// <summary>
    /// Returns the tile on success, null when the destination is blocked or out
    /// of range. Throws when the unit does not belong to the source tile.
    /// Clicking the tile the unit already stands on is a no-op success so
    /// callers can re-prompt for world exits.
    /// </summary>
    public WorldTile? MoveUnit(HexagonalPos worldTilePos, HexagonalPos destination, Unit unit)
    {
        if (unit.CurrentTile == null || !unit.CurrentTile.Equals(worldTilePos))
            throw new InvalidOperationException("Unit is not on the specified tile");

        WorldTile tile = _WorldMap.GetTile(worldTilePos);

        if (!tile.ContainsUnit(unit))
            throw new InvalidOperationException("Unit is not on the specified tile");

        if (destination.Equals(unit.CurrentBoardPos))
            return tile;

        if (!tile.TryMoveUnit(unit, destination, Math.Max(1, unit.Speed)))
            return null;
        return tile;
    }

    /// <summary>
    /// Moves <paramref name="unit"/> through the world exit it currently stands
    /// on into the neighboring world tile. Throws when the unit is not on an
    /// exit tile; returns both tiles so callers can broadcast to both rooms.
    /// </summary>
    public TraverseResult TraverseWorldExit(HexagonalPos currentWorldPos, Unit unit)
    {
        if (unit.CurrentTile == null || !unit.CurrentTile.Equals(currentWorldPos))
            throw new InvalidOperationException("Unit is not on the specified tile");

        WorldTile oldTile = _WorldMap.GetTile(currentWorldPos);

        if (!oldTile.TryGetExitDirection(unit, out HexDirection exitDirection))
            throw new InvalidOperationException("Unit is not standing on a world exit.");

        HexagonalPos neighborPos = currentWorldPos + exitDirection.ToOffset();
        WorldTile newTile = AddUnitToTile(neighborPos, exitDirection, unit);

        return new TraverseResult(oldTile, newTile, neighborPos.Copy(), exitDirection);
    }

    public void RemoveUnitFromTile(HexagonalPos worldTilePos, Unit unit)
    {
        if (_WorldMap.TryGetTile(worldTilePos, out var tile) && tile != null)
            tile.RemoveUnit(unit);
    }
}
