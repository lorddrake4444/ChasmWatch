public class GameState
{
    public WorldMap _WorldMap { get; }
    public GameState(WorldMap worldMap)
    {
        _WorldMap = worldMap;
    }

    public WorldTile AddUnitToTile(HexagonalPos worldPos, HexDirection? entryDirection, Unit unit)
    {
        WorldTile tile = _WorldMap.GetTile(worldPos);

        if (entryDirection != null)
        {
            string oppositeKey = WorldTile.Opposites[entryDirection.ToString()!];
            if (!tile.WorldExits.TryGetValue(oppositeKey, out var exits))
                throw new InvalidOperationException($"No world exits found for direction {oppositeKey}");

            if (tile.TryPlaceUnitAtEntry(exits, unit) == null)
                throw new InvalidOperationException("All entry tiles are occupied");
        }
        else
        {
            if (!tile.TryPlaceUnit(new HexagonalPos(0, 0, 0), unit))
                throw new InvalidOperationException("Center tile is occupied or missing");
        }
        return tile;
    }
    public WorldTile? MoveUnit(HexagonalPos worldTilePos, HexagonalPos destination, Unit unit)
    {
        WorldTile tile = _WorldMap.GetTile(worldTilePos);
        if (!tile.Board.TryGetValue(unit.CurrentWorldPos, out var currentTile) 
            || currentTile.Occupant != unit)
            throw new InvalidOperationException("Unit is not on the specified tile");
        if (!tile.TryPlaceUnit(destination, unit))
            return null;
        return tile;
    }
}