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
        if (unit.CurrentTile != null && !unit.CurrentTile.Equals(worldPos)
            && _WorldMap.TryGetTile(unit.CurrentTile, out var previousTile)
            && previousTile != null)
        {
            previousTile.RemoveUnit(unit);
        }

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

        unit.CurrentTile = tile.Position.Copy();
        return tile;
    }

    /// <summary>
    /// Returns the tile on success, null when the destination is blocked or out
    /// of range. Throws when the unit does not belong to the source tile.
    /// </summary>
    public WorldTile? MoveUnit(HexagonalPos worldTilePos, HexagonalPos destination, Unit unit)
    {
        if (unit.CurrentTile == null || !unit.CurrentTile.Equals(worldTilePos))
            throw new InvalidOperationException("Unit is not on the specified tile");

        WorldTile tile = _WorldMap.GetTile(worldTilePos);
        if (!tile.TryMoveUnit(unit, destination, Math.Max(1, unit.Speed)))
            return null;
        return tile;
    }

    public void RemoveUnitFromTile(HexagonalPos worldTilePos, Unit unit)
    {
        if (_WorldMap.TryGetTile(worldTilePos, out var tile) && tile != null)
            tile.RemoveUnit(unit);
    }
}
