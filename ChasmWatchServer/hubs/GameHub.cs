using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class GameHub : Hub
{
    private readonly GameState _state;
    private readonly IPlayerService _playerService;

    public GameHub(GameState state, IPlayerService playerService)
    {
        _state = state;
        _playerService = playerService;
    }
    private string Username() => Context.User?.Identity?.Name ?? "Unknown";

    private Player? CurrentPlayer()
    {
        var playerIdClaim = Context.User?.FindFirst("PlayerId")?.Value;
        if (playerIdClaim == null || !Guid.TryParse(playerIdClaim, out var playerId))
            return null;
        return _playerService.GetPlayerById(playerId);
    }

    public async Task JoinTile(HexagonalPos pos, HexDirection? entryDirection)
    {
        string roomName = pos.ToString();
        string username = Username();
        var player = CurrentPlayer();
        if (player == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "Invalid session.");
            return;
        }
        if (player.ActiveChar == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "No active character.");
            return;
        }
        Unit playerUnit = player.ActiveChar;

        if (entryDirection == null)
            entryDirection = HexDirection.DOWN;

        try
        {
            WorldTile tile = _state.AddUnitToTile(pos, entryDirection, playerUnit);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("ReceiveMessage", "System", $"{username} joined tile {roomName}");
            await Clients.Group(roomName).SendAsync("ReceiveTileInfo", BuildBoardDto(tile), BuildUnitDto(playerUnit));
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
        {
            await Clients.Caller.SendAsync("SystemMessage", $"Join failed: {ex.Message}");
        }
    }

    public async Task MoveUnit(HexagonalPos worldTilePos, HexagonalPos destination)
    {
        var player = CurrentPlayer();
        if (player == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "Invalid session.");
            return;
        }
        if (player.ActiveChar == null)
        {
            await Clients.Caller.SendAsync("SystemMessage", "No active character.");
            return;
        }
        Unit playerUnit = player.ActiveChar;

        try
        {
            WorldTile? tile = _state.MoveUnit(worldTilePos, destination, playerUnit);
            if (tile == null)
            {
                await Clients.Caller.SendAsync("SystemMessage", "Move failed.");
                return;
            }
            await Clients.Group(tile.Position.ToString()).SendAsync("ReceiveTileInfo", BuildBoardDto(tile), BuildUnitDto(playerUnit));
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
        {
            await Clients.Caller.SendAsync("SystemMessage", $"Move failed: {ex.Message}");
        }
    }

    private static object BuildBoardDto(WorldTile tile) =>
        tile.Board.Values.Select(b => new
        {
            b.Position,
            b.Pathable,
            b.Occupied,
            b.IsWorldExit,
            WorldExitDirection = b.WorldExitDirection?.ToString(),
            Exits = b.exits.ToDictionary(e => e.Key.ToString(), e => e.Value),
        });

    private static object BuildUnitDto(Unit unit) => new
    {
        unit.ID,
        unit.Name,
        unit.Health,
        unit.Speed,
        BoardPos = unit.CurrentBoardPos,
        TilePos = unit.CurrentTile,
    };
}
