
public class HexagonalMap<T> where T : HexNode
{
    private readonly Dictionary<HexagonalPos, T> Grid = [];

    public void SetTile(int Q , int R , int S, T tile)
    {
        Grid[new HexagonalPos(R, Q, S)] = tile;
        tile.Position = new HexagonalPos(R, Q, S);
    }

    public void SetTile(HexagonalPos pos, T tile)
    {
        Grid[pos] = tile;
        tile.Position = pos;
    }

    public T? GetTile(int Q , int R , int S)
    {
        if (Grid.ContainsKey(new HexagonalPos(R, Q, S))) return Grid[new HexagonalPos(R, Q, S)];
        else 
        {
            throw new KeyNotFoundException();
        }
    }

    public T? GetTile(HexagonalPos pos)
    {
        if (Grid.ContainsKey(pos)) return Grid[pos];
        else 
        {
            throw new KeyNotFoundException();
        }
    }

    public List<HexagonalPos> PathFind (HexagonalPos start, HexagonalPos end , int SearchRange = int.MaxValue)
    {
        if (!Grid.ContainsKey(start) || !Grid.ContainsKey(end))
        {
            throw new ArgumentException("Start or End position outside of the map");
        }
        if(!Grid[start].Pathable || !Grid[end].Pathable || Grid[end].Occupied)
        {
            throw new ArgumentException("Start or End position is not pathable");
        }
        int MinDistance = HexagonalPos.HexDistance(start, end);
        if (MinDistance > SearchRange)
        {
            throw new ArgumentException("Ending position out of range");
        };
        HashSet<HexagonalPos> visited = new HashSet<HexagonalPos> ();
        Queue<(HexagonalPos pos , List<HexagonalPos> path)> queue = new Queue<(HexagonalPos pos , List<HexagonalPos> path)> ();
        queue.Enqueue((start , new List<HexagonalPos> {start}));

        while (queue.Count > 0)
        {
            var (current , path) = queue.Dequeue();

            if(path.Count -1 > SearchRange){ continue; }
            
            if(current.Equals(end)){ return path; }
            HexagonalPos[] neighbours = HexagonalPos.GetNeighbors(current);
            foreach (HexagonalPos next in neighbours)
            {
                if (visited.Contains(next)) { continue; }
                if(!Grid.ContainsKey(next)) { continue; }
                if(!Grid[next].Pathable || Grid[next].Occupied) { continue; }
                visited.Add(next);
                var newPath = new List<HexagonalPos>(path) { next };
                queue.Enqueue((next , newPath));
            }
        }
        throw new ArgumentException("No Valid path found in range");
    }

    public Dictionary<HexagonalPos,T> GetArea(HexagonalPos Center , int Radius)
    {
        if(!Grid.ContainsKey(Center)) { throw new KeyNotFoundException("Center position outside of the map"); }
        if(Radius < 0) { throw new ArgumentException("Radius must be greater than 0"); }
        Dictionary<HexagonalPos , T> area = new Dictionary<HexagonalPos , T> ();
        for (int dr = -Radius; dr <= Radius; dr++)
        {
            for(int dq = Math.Max(-Radius,-dr-Radius); dq <= Math.Min(Radius,-dr+Radius); dq++)
            {
                int ds = - dr - dq;
                HexagonalPos pos = new(
                    Center.r + dr,
                    Center.q + dq,
                    Center.s + ds
                );
                if(Grid.ContainsKey(pos)){ area[pos] = Grid[pos]; }
            }
        }
        return area;
    }

    public Dictionary<HexagonalPos,T> GetCone(HexagonalPos Start , int Length , HexagonalPos Direction)
    {
        if(!Grid.ContainsKey(Start)) { throw new KeyNotFoundException("Start position outside of the map"); }
        if(Length < 0) { throw new ArgumentException("Length must be at least 0"); }
        Dictionary<HexagonalPos , T> cone = new Dictionary<HexagonalPos , T> ();
        cone[Start] = Grid[Start];
        for (int distance = 1; distance <= Length; distance++)
        {
            HexagonalPos Center = Start + (Direction * distance);
            if(Grid.ContainsKey(Center)){ cone[Center] = Grid[Center]; }
            for (int spread = 1; spread <= distance; spread++)
            {
                var left = GetAdjacents(Center , Direction)[0]*spread;
                var right = GetAdjacents(Center , Direction)[1]*spread;
                if(Grid.ContainsKey(left)){ cone[left] = Grid[left]; }
                if(Grid.ContainsKey(right)){ cone[right] = Grid[right]; }
            }
        }
        return cone;
    }

    public Dictionary<HexagonalPos,T> GetLine(HexagonalPos Start , HexagonalPos Direction, int Length = int.MaxValue)
    {
        if(!Grid.ContainsKey(Start)) { throw new KeyNotFoundException("Start position outside of the map"); }
        if(Length<=0){throw new ArgumentException("Length must be greater than 0"); }
        Dictionary<HexagonalPos , T> line = new Dictionary<HexagonalPos , T> ();
        line[Start] = Grid[Start];
        for (int distance = 1; distance <= Length; distance++)
        {
            HexagonalPos Point = Start + (Direction * distance);
            if(Grid.ContainsKey(Point)){ line[Point] = Grid[Point]; }
        }
        return line;
    }

    public HexagonalPos[] GetAdjacents(HexagonalPos pos , HexagonalPos Direction)
    {
        return Direction switch
        {
            var d when d == HexagonalPos.UP => [pos + HexagonalPos.LEFTUP , pos + HexagonalPos.RIGHTUP],
            var d when d == HexagonalPos.DOWN => [pos + HexagonalPos.LEFTDOWN , pos + HexagonalPos.RIGHTDOWN],
            var d when d == HexagonalPos.LEFTUP => [pos + HexagonalPos.UP , pos + HexagonalPos.LEFTDOWN],
            var d when d == HexagonalPos.LEFTDOWN => [pos + HexagonalPos.DOWN , pos + HexagonalPos.LEFTUP],
            var d when d == HexagonalPos.RIGHTUP => [pos + HexagonalPos.UP , pos + HexagonalPos.RIGHTDOWN],
            var d when d == HexagonalPos.RIGHTDOWN => [pos + HexagonalPos.DOWN , pos + HexagonalPos.RIGHTUP],
            _ => throw new ArgumentException("Invalid direction")
        };
    }
}