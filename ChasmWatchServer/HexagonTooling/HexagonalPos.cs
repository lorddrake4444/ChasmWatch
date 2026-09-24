public enum HexDirection
{
    UP, DOWN, LEFTUP, LEFTDOWN, RIGHTUP, RIGHTDOWN
}

public static class HexDirectionExtensions
{
    public static HexDirection Opposite(this HexDirection direction) => direction switch
    {
        HexDirection.UP => HexDirection.DOWN,
        HexDirection.DOWN => HexDirection.UP,
        HexDirection.LEFTUP => HexDirection.RIGHTDOWN,
        HexDirection.RIGHTDOWN => HexDirection.LEFTUP,
        HexDirection.LEFTDOWN => HexDirection.RIGHTUP,
        HexDirection.RIGHTUP => HexDirection.LEFTDOWN,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    public static HexagonalPos ToOffset(this HexDirection direction) => direction switch
    {
        HexDirection.UP => HexagonalPos.UP,
        HexDirection.DOWN => HexagonalPos.DOWN,
        HexDirection.LEFTUP => HexagonalPos.LEFTUP,
        HexDirection.LEFTDOWN => HexagonalPos.LEFTDOWN,
        HexDirection.RIGHTUP => HexagonalPos.RIGHTUP,
        HexDirection.RIGHTDOWN => HexagonalPos.RIGHTDOWN,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}

public record class HexagonalPos
{
    public int r { get; init; }
    public int q { get; init; }
    public int s { get; init; }

    static public readonly HexagonalPos UP = new HexagonalPos(-1, 0, +1);

    static public readonly HexagonalPos DOWN = new HexagonalPos(+1, 0, -1);

    static public readonly HexagonalPos LEFTUP = new HexagonalPos(0, -1, +1);

    static public readonly HexagonalPos LEFTDOWN = new HexagonalPos(+1, -1, 0);

    static public readonly HexagonalPos RIGHTUP = new HexagonalPos(-1, +1, 0);

    static public readonly HexagonalPos RIGHTDOWN = new HexagonalPos(0, +1, -1);

    static public HexagonalPos operator +(HexagonalPos a, HexagonalPos b) =>
    new HexagonalPos(a.r + b.r, a.q + b.q, a.s + b.s);

    static public HexagonalPos operator -(HexagonalPos a, HexagonalPos b) =>
    new HexagonalPos(a.r - b.r, a.q - b.q, a.s - b.s);

    static public HexagonalPos operator *(HexagonalPos a, HexagonalPos b) =>
    new HexagonalPos(a.r * b.r, a.q * b.q, a.s * b.s);

    public static HexagonalPos operator *(HexagonalPos a, int b) =>
    new HexagonalPos(a.r * b, a.q * b, a.s * b);

    public static HexagonalPos operator /(HexagonalPos a, int b) =>
    new HexagonalPos(a.r / b, a.q / b, a.s / b);


    public HexagonalPos(int r, int q, int s)
    {
        this.r = r;
        this.q = q;
        this.s = s;
        if (r + q + s != 0) throw new ArgumentException("r + q + s must be 0");
    }

    public HexagonalPos Copy() => new HexagonalPos(r, q, s);

    public static int HexDistance (HexagonalPos a, HexagonalPos b)
    {
        HexagonalPos vector = a - b;
        int absolute = Math.Abs(vector.r) + Math.Abs(vector.q) + Math.Abs(vector.s);
        return absolute / 2;
    }

    public static HexagonalPos[] GetNeighbors (HexagonalPos pos) =>
    [pos + UP, pos + DOWN, pos + LEFTUP, pos + LEFTDOWN, pos + RIGHTUP, pos + RIGHTDOWN];
}
