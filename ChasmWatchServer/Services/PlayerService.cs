public interface IPlayerService
{
    List<Player> GetPlayers();
    Player CreatePlayer(string username);
    Player? GetPlayerById(Guid id);
    void DeletePlayer(Guid id);
}

public class PlayerService : IPlayerService
{
    private readonly List<Player> _players = new List<Player>();

    public List<Player> GetPlayers() => _players;

    public Player CreatePlayer(string username)
    {
        var existingPlayer = _players.FirstOrDefault(p => 
            p.UserName != null && p.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (existingPlayer != null)
        {
            return existingPlayer;
        }

        Player player = new Player()
        {
            ID = Guid.NewGuid(),
            UserName = username
        };
        
        _players.Add(player);
        return player;
    }
    public Player? GetPlayerById(Guid id)
    {
        return _players.FirstOrDefault(x => x.ID == id);
    }
    public void DeletePlayer(Guid id)
    {
        Player? p = _players.FirstOrDefault(x => x.ID == id);
        if (p != null)
        {
            _players.Remove(p);
        }
    }
}