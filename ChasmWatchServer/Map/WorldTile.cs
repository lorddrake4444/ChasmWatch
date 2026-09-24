public class WorldTile
{
    public HexagonalPos Position;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    public Dictionary<HexagonalPos, BoardTile> Board = new();
    public bool Active = false;

    private static readonly Dictionary<HexDirection, HexagonalPos> DirectionVectors = new()
    {
        { HexDirection.UP,        HexagonalPos.UP        },
        { HexDirection.DOWN,      HexagonalPos.DOWN      },
        { HexDirection.LEFTUP,    HexagonalPos.LEFTUP    },
        { HexDirection.RIGHTUP,   HexagonalPos.RIGHTUP   },
        { HexDirection.LEFTDOWN,  HexagonalPos.LEFTDOWN  },
        { HexDirection.RIGHTDOWN, HexagonalPos.RIGHTDOWN },
    };

    public Dictionary<HexDirection, List<HexagonalPos>> WorldExits = new();

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
        Queue<(HexagonalPos pos, HexDirection? requiredExit)> frontier = new();
        frontier.Enqueue((entrance, null));

        while (Board.Count < targetSize && frontier.Count > 0)
        {
            var (pos, requiredExit) = frontier.Dequeue();

            if (Board.ContainsKey(pos))
            {
                if (requiredExit != null)
                    Board[pos].exits[requiredExit.Value] = true;
                continue;
            }

            BoardTile tile = new BoardTile(pos, pathable: true, occupied: false);

            if (requiredExit != null)
                tile.exits[requiredExit.Value] = true;

            foreach (HexDirection direction in DirectionVectors.Keys)
            {
                if (direction == requiredExit) continue;
                tile.exits[direction] = rand.NextDouble() < openness;
            }

            Board[pos] = tile;

            foreach (var (direction, isOpen) in tile.exits)
            {
                if (!isOpen) continue;
                HexagonalPos neighbourPos = pos + DirectionVectors[direction];
                HexDirection exitBackToUs = direction.Opposite();

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
            foreach (HexDirection direction in DirectionVectors.Keys)
            {
                if (!tile.exits[direction]) continue;
                HexagonalPos neighbourPos = tile.Position + DirectionVectors[direction];
                if (!Board.ContainsKey(neighbourPos))
                    tile.exits[direction] = false;
            }
        }

        // Enforce bidirectional doors. Generation stops early once the target
        // size is reached, which can orphan a carved door whose back-link was
        // still queued in the frontier. Heal those so every exit maps to its
        // exact opposite on the neighboring tile.
        foreach (var tile in Board.Values)
        {
            foreach (var (direction, isOpen) in tile.exits)
            {
                if (!isOpen) continue;
                HexagonalPos neighbourPos = tile.Position + DirectionVectors[direction];
                if (Board.TryGetValue(neighbourPos, out var neighbour))
                    neighbour.exits[direction.Opposite()] = true;
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
            if (!tile.Pathable || tile.Occupied) return false;
            ClearUnitFromBoardNoLock(unit);
            tile.Occupant = unit;
            tile.Occupied = true;
            unit.CurrentBoardPos = tile.Position.Copy();
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }

    public HexagonalPos? TryPlaceUnitAtEntry(List<HexagonalPos> candidates, Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            var visited  = new HashSet<HexagonalPos>(candidates);
            var frontier = new Queue<HexagonalPos>(candidates);

            while (frontier.Count > 0)
            {
                var pos = frontier.Dequeue();

                if (Board.TryGetValue(pos, out var tile) && tile.Pathable && !tile.Occupied)
                {
                    ClearUnitFromBoardNoLock(unit);
                    tile.Occupant = unit;
                    tile.Occupied = true;
                    unit.CurrentBoardPos = tile.Position.Copy();
                    return tile.Position.Copy();
                }

                // tile is occupied, impassable, or off-board — expand its neighbours
                foreach (var neighbour in HexagonalPos.GetNeighbors(pos))
                {
                    if (visited.Contains(neighbour)) continue;
                    if (!Board.ContainsKey(neighbour)) continue;
                    visited.Add(neighbour);
                    frontier.Enqueue(neighbour);
                }
            }

            return null; // entire reachable board is occupied
        }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Validates ownership, destination walkability, and range (BFS up to
    /// <paramref name="maxSteps"/>). Returns false for blocked/out-of-range
    /// moves; callers treat that as "Move failed", not an error.
    /// </summary>
    public bool TryMoveUnit(Unit unit, HexagonalPos destination, int maxSteps)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!Board.TryGetValue(unit.CurrentBoardPos, out var currentTile)
                || !ReferenceEquals(currentTile.Occupant, unit))
                return false;

            if (!Board.TryGetValue(destination, out var destTile)) return false;
            if (!destTile.Pathable || destTile.Occupied) return false;
            if (destination.Equals(unit.CurrentBoardPos)) return false;

            if (HexagonalPos.HexDistance(unit.CurrentBoardPos, destination) > maxSteps)
                return false;

            if (!IsReachableNoLock(unit.CurrentBoardPos, destination, maxSteps))
                return false;

            currentTile.Occupant = null;
            currentTile.Occupied = false;
            destTile.Occupant = unit;
            destTile.Occupied = true;
            unit.CurrentBoardPos = destTile.Position.Copy();
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }

    public bool RemoveUnit(Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            return ClearUnitFromBoardNoLock(unit);
        }
        finally { _lock.ExitWriteLock(); }
    }

    public bool ContainsUnit(Unit unit)
    {
        _lock.EnterReadLock();
        try
        {
            if (Board.TryGetValue(unit.CurrentBoardPos, out var tile)
                && ReferenceEquals(tile.Occupant, unit))
                return true;
            foreach (var boardTile in Board.Values)
            {
                if (ReferenceEquals(boardTile.Occupant, unit))
                    return true;
            }
            return false;
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Returns the world-exit direction of the board tile <paramref name="unit"/>
    /// currently occupies, or false when it is not standing on an exit.
    /// </summary>
    public bool TryGetExitDirection(Unit unit, out HexDirection exitDirection)
    {
        _lock.EnterReadLock();
        try
        {
            BoardTile? occupied = null;
            if (Board.TryGetValue(unit.CurrentBoardPos, out var fast)
                && ReferenceEquals(fast.Occupant, unit))
            {
                occupied = fast;
            }
            else
            {
                foreach (var boardTile in Board.Values)
                {
                    if (ReferenceEquals(boardTile.Occupant, unit))
                    {
                        occupied = boardTile;
                        break;
                    }
                }
            }

            if (occupied != null && occupied.IsWorldExit && occupied.WorldExitDirection != null)
            {
                exitDirection = occupied.WorldExitDirection.Value;
                return true;
            }
            exitDirection = default;
            return false;
        }
        finally { _lock.ExitReadLock(); }
    }

    private bool ClearUnitFromBoardNoLock(Unit unit)
    {
        if (Board.TryGetValue(unit.CurrentBoardPos, out var currentTile)
            && ReferenceEquals(currentTile.Occupant, unit))
        {
            currentTile.Occupant = null;
            currentTile.Occupied = false;
            return true;
        }
        // Fallback scan: CurrentBoardPos may already point at another tile
        // after a cross-tile placement, or the key instance may differ.
        foreach (var boardTile in Board.Values)
        {
            if (ReferenceEquals(boardTile.Occupant, unit))
            {
                boardTile.Occupant = null;
                boardTile.Occupied = false;
                return true;
            }
        }
        return false;
    }

    private bool IsReachableNoLock(HexagonalPos start, HexagonalPos end, int maxSteps)
    {
        if (maxSteps <= 1)
            return HexagonalPos.HexDistance(start, end) <= 1;

        var visited = new HashSet<HexagonalPos> { start };
        var queue = new Queue<(HexagonalPos pos, int steps)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (current, steps) = queue.Dequeue();
            if (steps >= maxSteps) continue;
            foreach (var next in HexagonalPos.GetNeighbors(current))
            {
                if (visited.Contains(next)) continue;
                if (!Board.TryGetValue(next, out var tile)) continue;
                if (!tile.Pathable) continue;
                if (tile.Occupied && !next.Equals(end)) continue;
                if (next.Equals(end)) return true;
                visited.Add(next);
                queue.Enqueue((next, steps + 1));
            }
        }
        return false;
    }
}
