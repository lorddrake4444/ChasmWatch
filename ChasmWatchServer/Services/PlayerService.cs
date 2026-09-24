public interface IPlayerService
{
    List<Player> GetPlayers();
    Player CreatePlayer(string username);
    Player? GetPlayerById(Guid id);
    void DeletePlayer(Guid id);
    void UpdatePlayer(Guid id, Action<Player> update);
}

public class PlayerService : IPlayerService
{
    private readonly List<Player> _players = new List<Player>();
    private readonly object _lock = new();

    public List<Player> GetPlayers()
    {
        lock (_lock) { return _players.ToList(); }
    }

    public Player CreatePlayer(string username)
    {
        lock (_lock)
        {
            var existingPlayer = _players.FirstOrDefault(p =>
                p.UserName != null && p.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (existingPlayer != null)
            {
                EnsureActiveChar(existingPlayer);
                return existingPlayer;
            }

            Player player = new Player()
            {
                ID = Guid.NewGuid(),
                UserName = username,
            };
            EnsureActiveChar(player);

            _players.Add(player);
            return player;
        }
    }

    public Player? GetPlayerById(Guid id)
    {
        lock (_lock) { return _players.FirstOrDefault(x => x.ID == id); }
    }

    public void DeletePlayer(Guid id)
    {
        lock (_lock)
        {
            Player? p = _players.FirstOrDefault(x => x.ID == id);
            if (p != null)
            {
                _players.Remove(p);
            }
        }
    }

    public void UpdatePlayer(Guid id, Action<Player> update)
    {
        lock (_lock)
        {
            Player? p = _players.FirstOrDefault(x => x.ID == id);
            if (p != null)
                update(p);
        }
    }

    private static void EnsureActiveChar(Player player)
    {
        player.ActiveChar ??= new Unit(health: 100, speed: 3)
        {
            Name = player.UserName,
        };
    }
}
