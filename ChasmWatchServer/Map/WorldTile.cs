public class WorldTile
{
    public HexagonalPos Position;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    public Dictionary<HexagonalPos, BoardTile> Board = new();
    public bool Active = false;

    private static readonly Dictionary<string, HexagonalPos> DirectionVectors = new()
    {
        { "Up",        HexagonalPos.UP        },
        { "Down",      HexagonalPos.DOWN      },
        { "UpLeft",    HexagonalPos.LEFTUP    },
        { "UpRight",   HexagonalPos.RIGHTUP   },
        { "DownLeft",  HexagonalPos.LEFTDOWN  },
        { "DownRight", HexagonalPos.RIGHTDOWN },
    };

    public static readonly Dictionary<string, string> Opposites = new()
    {
        { "Up",        "Down"      },
        { "Down",      "Up"        },
        { "UpLeft",    "DownRight" },
        { "UpRight",   "DownLeft"  },
        { "DownLeft",  "UpRight"   },
        { "DownRight", "UpLeft"    },
    };

    public Dictionary<string, List<HexagonalPos>> WorldExits = new();

    public WorldTile(HexagonalPos position)
    {
        this.Position = position;
    }

    public void Generate(double openness)
    {
        int seed = HashCode.Combine(Position.q, Position.r, Position.s, DateTime.UtcNow.Millisecond);
        Random rand = new Random(seed);
        int targetSize = rand.Next(64, 512);

        HexagonalPos entrance = new HexagonalPos(0, 0, 0);
        Queue<(HexagonalPos pos, string? requiredExit)> frontier = new();
        frontier.Enqueue((entrance, null));

        while (Board.Count < targetSize && frontier.Count > 0)
        {
            var (pos, requiredExit) = frontier.Dequeue();

            if (Board.ContainsKey(pos))
            {
                if (requiredExit != null)
                    Board[pos].exits[requiredExit] = true;
                continue;
            }

            BoardTile tile = new BoardTile(pos, pathable: true, occupied: false);

            if (requiredExit != null)
                tile.exits[requiredExit] = true;

            foreach (string direction in DirectionVectors.Keys)
            {
                if (direction == requiredExit) continue;
                tile.exits[direction] = rand.NextDouble() < openness;
            }

            Board[pos] = tile;

            foreach (var (direction, isOpen) in tile.exits)
            {
                if (!isOpen) continue;
                HexagonalPos neighbourPos = pos + DirectionVectors[direction];
                string exitBackToUs = Opposites[direction];

                if (Board.ContainsKey(neighbourPos))
                {
                    Board[neighbourPos].exits[exitBackToUs] = true;
                    continue;
                }
                frontier.Enqueue((neighbourPos, exitBackToUs));
            }
        }
        foreach (var tile in Board.Values)
        {
            foreach (string direction in DirectionVectors.Keys)
            {
                if (!tile.exits[direction]) continue;
                HexagonalPos neighbourPos = tile.Position + DirectionVectors[direction];
                if (!Board.ContainsKey(neighbourPos))
                    tile.exits[direction] = false;
            }
        }

        PlaceWorldExits(rand);
    }

    private void PlaceWorldExits(Random rand)
    {
        foreach (var (direction, vector) in DirectionVectors)
        {
            int maxProjection = int.MinValue;

            foreach (var (pos, _) in Board)
            {
                int projection = (pos.r * vector.r) + (pos.q * vector.q) + (pos.s * vector.s);
                if (projection > maxProjection)
                    maxProjection = projection;
            }

            var edgeTiles = Board.Values
                .Where(t =>
                {
                    int p = (t.Position.r * vector.r) +
                            (t.Position.q * vector.q) +
                            (t.Position.s * vector.s);
                    return p == maxProjection;
                })
                .ToList();

            // Store all exit positions per direction instead of one
            WorldExits[direction] = edgeTiles.Select(t => t.Position).ToList();

            foreach (var tile in edgeTiles)
            {
                tile.exits[direction] = true;
                tile.IsWorldExit = true;
                tile.WorldExitDirection = direction;
            }
        }
    }
    public bool TryPlaceUnit(HexagonalPos boardPos, Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!Board.TryGetValue(boardPos, out var tile)) return false;
            if (tile.Occupied) return false;
            Board.TryGetValue(unit.CurrentWorldPos, out var currentTile);
            if (currentTile != null)
            {
                currentTile.Occupant = null;
                currentTile.Occupied = false;
            }
            tile.Occupant = unit;
            tile.Occupied = true;
            unit.CurrentWorldPos = boardPos;
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }
    public HexagonalPos? TryPlaceUnitAtEntry(List<HexagonalPos> candidates, Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var pos in candidates)
            {
                if (!Board.TryGetValue(pos, out var tile)) continue;
                if (tile.Occupied) continue;
                tile.Occupant = unit;
                tile.Occupied = true;
                unit.CurrentWorldPos = pos;
                return pos;
            }
            return null;
        }
        finally { _lock.ExitWriteLock(); }
    }
}
