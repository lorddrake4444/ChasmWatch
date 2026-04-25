using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class GameHub : Hub
{
    private readonly GameState _state;
    private readonly PlayerService _playerService;

    public GameHub(GameState state, PlayerService playerService)
    {
        _state = state;
        _playerService = playerService;
    }
    private string Username() => Context.User?.Identity?.Name ?? "Unknown";

    public async Task JoinTile(HexagonalPos pos , HexDirection? entryDirection)
    {
        string roomName = pos.ToString();
        string username = Username();
        var playerIdClaim = Context.User?.FindFirst("PlayerId")?.Value;
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            await Clients.Caller.SendAsync("SystemMessage", "Invalid session.");
            return;
        }
        var player = _playerService.GetPlayerById(playerId);
        if (player?.ActiveChar == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "No active character.");
            return;
        }
        Unit playerUnit = player.ActiveChar;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        await Clients.Group(roomName).SendAsync("ReceiveMessage", "System", $"{username} joined tile {roomName}");
        WorldTile Tile = _state.AddUnitToTile(pos, entryDirection, playerUnit);
        var boardDto = Tile.Board.Values.Select(b => new
        {
            b.Position,
            b.Pathable,
            b.Occupied,
            b.IsWorldExit,
            b.WorldExitDirection,
            b.exits
        });
        await Clients.Group(roomName).SendAsync("ReceiveTileInfo", boardDto);
    }

    public async Task MoveUnit(HexagonalPos WorldTilePos , HexagonalPos destination)
    {
        var username = Username();
        var playerIdClaim = Context.User?.FindFirst("PlayerId")?.Value;
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            await Clients.Caller.SendAsync("SystemMessage", "Invalid session.");
            return;
        }
        var player = _playerService.GetPlayerById(playerId);
        if (player?.ActiveChar == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "No active character.");
            return;
        }
        WorldTile? tile = _state.MoveUnit(WorldTilePos, destination, player.ActiveChar);
        if (tile == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "Move failed.");
            return;
        }
        var boardDto = tile.Board.Values.Select(b => new
        {
            b.Position,
            b.Pathable,
            b.Occupied,
            b.IsWorldExit,
            b.WorldExitDirection,
            b.exits
        });
        await Clients.Group(tile.Position.ToString()).SendAsync("ReceiveTileInfo", boardDto);
    }
}