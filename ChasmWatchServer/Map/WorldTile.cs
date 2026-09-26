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

    public void Generate(SpawnSettings spawn, int? seedOverride = null)
    {
        double openness = spawn.Openness;
        int seed = HashCode.Combine(Position.q, Position.r, Position.s, seedOverride ?? DateTime.UtcNow.Millisecond);
        Random rand = new Random(seed);
        int targetSize = rand.Next(64, 512);
        int roomBudget = Math.Max(1, spawn.RoomSize);
        int corridorBudget = Math.Max(0, spawn.CorridorLength);

        HexagonalPos entrance = new HexagonalPos(0, 0, 0);
        int roomSeq = 0;
        int corridorSeq = 0;

        // First room at the entrance keeps spawn open for joins.
        roomSeq++;
        var (_, firstLast) = GrowRoom(rand, openness, Math.Min(roomBudget, targetSize), roomSeq, entrance, null);
        HexagonalPos? chainTip = firstLast;

        int guardCap = targetSize * 20 + 200;
        int guard = 0;
        bool capTip = false; // true when the previous site was a corridor needing a room cap
        while (Board.Count < targetSize && guard++ < guardCap)
        {
            bool progressed = false;
            // Cap sites at the remaining target so rooms can't overshoot it.
            int remaining = Math.Max(1, targetSize - Board.Count);
            int roomNow = Math.Max(1, Math.Min(roomBudget, remaining));
            int corrNow = Math.Min(corridorBudget, remaining);

            // Corridor site (skipped when CorridorLength == 0). Always seeded
            // at a random frontier edge: chaining from the newest tip marches
            // growth in one persistent direction instead of coiling.
            if (corrNow > 0 && TryFindEdge(rand, null, out var corridorFrom, out var corridorDir))
            {
                Board[corridorFrom].exits[corridorDir] = true;
                corridorSeq++;
                HexagonalPos? corridorEnd = GrowCorridor(rand, -corridorSeq, corridorFrom, corridorDir, corrNow);
                if (corridorEnd != null)
                {
                    chainTip = corridorEnd;
                    capTip = true;
                    progressed = true;
                }
            }

            // Room site. Seeds at the chain tip only to cap a fresh corridor
            // end; otherwise seeds at a random frontier edge. Always chaining
            // from the tip marches growth in one persistent direction.
            if (TryFindEdge(rand, capTip ? chainTip : null, out var roomFrom, out var roomDir))
            {
                Board[roomFrom].exits[roomDir] = true;
                HexagonalPos roomSeed = roomFrom + DirectionVectors[roomDir];
                if (!Board.ContainsKey(roomSeed))
                {
                    roomSeq++;
                    var (added, last) = GrowRoom(rand, openness, roomNow, roomSeq, roomSeed, roomDir.Opposite());
                    if (added > 0)
                    {
                        chainTip = last;
                        progressed = true;
                    }
                }
                else
                {
                    Board[roomSeed].exits[roomDir.Opposite()] = true;
                    chainTip = roomSeed;
                    progressed = true;
                }
                capTip = false;
            }

            if (!progressed) break; // fully enclosed: nothing borders unvisited space
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
        SpawnEnemies(rand, spawn);
    }

    /// <summary>
    /// Flood-fill room site capped at <paramref name="budget"/> new tiles.
    /// Returns the tiles added and the last one placed (chain tip).
    /// </summary>
    private (int added, HexagonalPos? last) GrowRoom(Random rand, double openness, int budget, int siteId, HexagonalPos seed, HexDirection? requiredExit)
    {
        int added = 0;
        HexagonalPos? last = null;
        var visited = new HashSet<HexagonalPos> { seed };
        var queue = new Queue<(HexagonalPos pos, HexDirection? req)>();
        queue.Enqueue((seed, requiredExit));

        while (queue.Count > 0 && added < budget)
        {
            var (pos, req) = queue.Dequeue();

            if (Board.ContainsKey(pos))
            {
                if (req != null)
                    Board[pos].exits[req.Value] = true;
                continue;
            }

            BoardTile tile = new BoardTile(pos, pathable: true, occupied: false);
            tile.SiteId = siteId;

            if (req != null)
                tile.exits[req.Value] = true;

            foreach (HexDirection direction in DirectionVectors.Keys)
            {
                if (direction == req) continue;
                tile.exits[direction] = rand.NextDouble() < openness;
            }

            Board[pos] = tile;
            added++;
            last = pos;

            // Shuffle expansion order per tile: .NET dictionaries enumerate in
            // fixed hash-bucket order, so a fixed expansion sequence would skew
            // every room toward the same compass direction (and chained rooms
            // would march that way). Shuffling decorrelates growth.
            var expandDirs = new List<HexDirection>(tile.exits.Keys);
            for (int i = expandDirs.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (expandDirs[i], expandDirs[j]) = (expandDirs[j], expandDirs[i]);
            }

            foreach (var direction in expandDirs)
            {
                if (!tile.exits[direction]) continue;
                HexagonalPos neighbourPos = pos + DirectionVectors[direction];
                HexDirection exitBackToUs = direction.Opposite();

                if (Board.ContainsKey(neighbourPos))
                {
                    Board[neighbourPos].exits[exitBackToUs] = true;
                    continue;
                }
                if (visited.Add(neighbourPos))
                    queue.Enqueue((neighbourPos, exitBackToUs));
            }
        }
        return (added, last);
    }

    /// <summary>
    /// Self-avoiding corridor walk of up to <paramref name="maxLength"/> steps.
    /// Winding comes from <see cref="PickCorridorDir"/> (straight preferred,
    /// then turns). Stepping onto board ends the segment with a join so areas
    /// stay open to one another. Returns the endpoint for chaining.
    /// </summary>
    private HexagonalPos? GrowCorridor(Random rand, int siteId, HexagonalPos from, HexDirection initialDir, int maxLength)
    {
        // Caller guarantees from is on the board and initialDir leads outside.
        HexagonalPos pos = from + DirectionVectors[initialDir];
        HexDirection prevDir = initialDir;
        HexagonalPos? last = null;
        int steps = 0;

        while (steps < maxLength)
        {
            if (Board.ContainsKey(pos))
            {
                Board[pos].exits[prevDir.Opposite()] = true;
                return pos;
            }

            var tile = new BoardTile(pos, pathable: true, occupied: false);
            tile.SiteId = siteId;
            tile.exits[prevDir.Opposite()] = true;

            HexDirection next = PickCorridorDir(rand, prevDir, prevDir.Opposite());
            tile.exits[next] = true;
            Board[pos] = tile;
            last = pos;
            steps++;

            HexagonalPos ahead = pos + DirectionVectors[next];
            if (Board.ContainsKey(ahead))
            {
                Board[ahead].exits[next.Opposite()] = true;
                return ahead;
            }
            pos = ahead;
            prevDir = next;
        }
        return last;
    }

    private static readonly HexDirection[] ClockwiseRing =
        [HexDirection.UP, HexDirection.RIGHTUP, HexDirection.RIGHTDOWN,
         HexDirection.DOWN, HexDirection.LEFTDOWN, HexDirection.LEFTUP];

    /// <summary>
    /// Next corridor heading: straight heavily favored, +-60 degree turns
    /// moderate, sharper bends rare. Never U-turns (back is excluded).
    /// </summary>
    private static HexDirection PickCorridorDir(Random rand, HexDirection forward, HexDirection back)
    {
        int fi = Array.IndexOf(ClockwiseRing, forward);
        var options = new List<(HexDirection dir, double weight)>();
        for (int i = 0; i < ClockwiseRing.Length; i++)
        {
            HexDirection d = ClockwiseRing[i];
            if (d == back) continue;
            int ringDist = Math.Min((i - fi + ClockwiseRing.Length) % ClockwiseRing.Length,
                                    (fi - i + ClockwiseRing.Length) % ClockwiseRing.Length);
            double weight = ringDist switch { 0 => 0.50, 1 => 0.17, _ => 0.08 };
            options.Add((d, weight));
        }
        double roll = rand.NextDouble() * options.Sum(o => o.weight);
        foreach (var (d, w) in options)
        {
            roll -= w;
            if (roll <= 0) return d;
        }
        return options[^1].dir;
    }

    /// <summary>
    /// Finds a board tile bordering unvisited space, preferring
    /// <paramref name="preferred"/> (chain tip). False when fully enclosed.
    /// </summary>
    private bool TryFindEdge(Random rand, HexagonalPos? preferred, out HexagonalPos from, out HexDirection dir)
    {
        from = null!;
        dir = default;
        if (preferred != null && Board.ContainsKey(preferred)
            && TryRandomUnvisitedDir(rand, preferred, out dir))
        {
            from = preferred;
            return true;
        }
        HexagonalPos? pick = null;
        int seen = 0;
        foreach (var (pos, _) in Board)
        {
            bool bordersOutside = false;
            foreach (var neighbor in HexagonalPos.GetNeighbors(pos))
            {
                if (!Board.ContainsKey(neighbor)) { bordersOutside = true; break; }
            }
            if (!bordersOutside) continue;
            seen++;
            if (rand.Next(seen) == 0) pick = pos; // reservoir sampling
        }
        if (pick == null || !TryRandomUnvisitedDir(rand, pick, out dir)) return false;
        from = pick;
        return true;
    }

    private bool TryRandomUnvisitedDir(Random rand, HexagonalPos from, out HexDirection dir)
    {
        var dirs = new List<HexDirection>(DirectionVectors.Keys);
        for (int i = dirs.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }
        foreach (var d in dirs)
        {
            if (!Board.ContainsKey(from + DirectionVectors[d]))
            {
                dir = d;
                return true;
            }
        }
        dir = default;
        return false;
    }

    /// <summary>
    /// Stationary enemy seeding: every board hex has a chance to spawn an
    /// enemy, up to <see cref="SpawnSettings.MaxEnemies"/>. Exits stay clear
    /// so traversal never strands, and the entrance stays clear so joining
    /// never lands on an enemy.
    /// </summary>
    private void SpawnEnemies(Random rand, SpawnSettings spawn)
    {
        if (spawn.MaxEnemies <= 0 || spawn.EnemySpawnChance <= 0)
            return;
        var entrance = new HexagonalPos(0, 0, 0);
        var candidates = Board.Values
            .Where(t => t.Pathable && !t.IsWorldExit && !t.Position.Equals(entrance))
            .ToList();
        // Fisher-Yates shuffle so spawn positions are uniform, not scan-ordered.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        int spawned = 0;
        foreach (var tile in candidates)
        {
            if (spawned >= spawn.MaxEnemies) break;
            if (rand.NextDouble() >= spawn.EnemySpawnChance) continue;
            var enemy = new Enemy();
            enemy.CurrentTile = Position.Copy();
            enemy.CurrentBoardPos = tile.Position.Copy();
            tile.Occupant = enemy;
            tile.Occupied = true;
            spawned++;
        }
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

    /// <summary>
    /// Marks a random non-exit, pathable board hex as interactable, preferring
    /// unoccupied tiles. Returns the chosen position, or null when none exists.
    /// </summary>
    public HexagonalPos? RevealInteractable(Random? rand = null)
    {
        _lock.EnterWriteLock();
        try
        {
            rand ??= new Random();
            var candidates = Board.Values
                .Where(t => t.Pathable && !t.IsWorldExit && !t.Interactable)
                .ToList();
            if (candidates.Count == 0)
                return null;
            var free = candidates.Where(t => !t.Occupied).ToList();
            var pool = free.Count > 0 ? free : candidates;
            var chosen = pool[rand.Next(pool.Count)];
            chosen.Interactable = true;
            return chosen.Position.Copy();
        }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Returns true when <paramref name="unit"/> stands on an interactable hex.
    /// </summary>
    public bool IsOnInteractable(Unit unit)
    {
        _lock.EnterReadLock();
        try
        {
            return FindOccupiedTileNoLock(unit)?.Interactable == true;
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Clears the interactable flag on <paramref name="unit"/>'s hex.
    /// Returns false when the unit is not standing on an interactable hex.
    /// </summary>
    public bool TryConsumeInteractable(Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            var tile = FindOccupiedTileNoLock(unit);
            if (tile == null || !tile.Interactable)
                return false;
            tile.Interactable = false;
            return true;
        }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Kills (deletes) the first enemy on a hex neighboring <paramref name="unit"/>.
    /// The tile persists in memory, so the kill is permanent for late joiners.
    /// Returns the slain enemy, or null when no enemy is adjacent.
    /// </summary>
    public Enemy? TryKillAdjacentEnemy(Unit unit)
    {
        _lock.EnterWriteLock();
        try
        {
            var standing = FindOccupiedTileNoLock(unit);
            if (standing == null) return null;
            foreach (var neighborPos in HexagonalPos.GetNeighbors(standing.Position))
            {
                if (Board.TryGetValue(neighborPos, out var neighbor)
                    && neighbor.Occupant is Enemy enemy)
                {
                    neighbor.Occupant = null;
                    neighbor.Occupied = false;
                    return enemy;
                }
            }
            return null;
        }
        finally { _lock.ExitWriteLock(); }
    }

    public int CountEnemies()
    {
        _lock.EnterReadLock();
        try
        {
            int count = 0;
            foreach (var tile in Board.Values)
            {
                if (tile.Occupant is Enemy) count++;
            }
            return count;
        }
        finally { _lock.ExitReadLock(); }
    }

    private BoardTile? FindOccupiedTileNoLock(Unit unit)
    {
        if (Board.TryGetValue(unit.CurrentBoardPos, out var fast)
            && ReferenceEquals(fast.Occupant, unit))
            return fast;
        foreach (var boardTile in Board.Values)
        {
            if (ReferenceEquals(boardTile.Occupant, unit))
                return boardTile;
        }
        return null;
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
