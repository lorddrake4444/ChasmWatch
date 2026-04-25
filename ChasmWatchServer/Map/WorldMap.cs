
using System.Collections.Concurrent;

public class WorldMap
{
    private ConcurrentDictionary<HexagonalPos, WorldTile> Map = new ConcurrentDictionary<HexagonalPos, WorldTile>();
    public WorldTile GetTile(HexagonalPos pos)
    {
        return Map.GetOrAdd(pos, p =>
        {
            var tile = new WorldTile(p);
            tile.Generate(0.5);
            tile.Active = true;
            return tile;
        });
    }
}