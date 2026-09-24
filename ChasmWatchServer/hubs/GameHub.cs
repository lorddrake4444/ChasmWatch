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
            await SendExitPromptIfOnExit(worldTilePos, tile, playerUnit);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
        {
            await Clients.Caller.SendAsync("SystemMessage", $"Move failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called by the client after the player accepts the exit prompt.
    /// Removes the unit from its current world tile and places it on the
    /// neighboring tile attached to the exit, then moves the connection
    /// to the new tile's group.
    /// </summary>
    public async Task TraverseWorldExit(HexagonalPos currentWorldTilePos)
    {
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

        try
        {
            TraverseResult result = _state.TraverseWorldExit(currentWorldTilePos, playerUnit);
            string oldRoom = currentWorldTilePos.ToString();
            string newRoom = result.NeighborPos.ToString();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldRoom);
            await Groups.AddToGroupAsync(Context.ConnectionId, newRoom);

            await Clients.Group(oldRoom).SendAsync("ReceiveMessage", "System", $"{username} left tile {oldRoom} through the {result.ExitDirection} exit.");
            await Clients.Group(oldRoom).SendAsync("ReceiveTileInfo", BuildBoardDto(result.OldTile), BuildUnitDto(playerUnit));
            await Clients.Group(newRoom).SendAsync("ReceiveMessage", "System", $"{username} entered tile {newRoom} from {oldRoom}.");
            await Clients.Group(newRoom).SendAsync("ReceiveTileInfo", BuildBoardDto(result.NewTile), BuildUnitDto(playerUnit));
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
        {
            await Clients.Caller.SendAsync("SystemMessage", $"Traverse failed: {ex.Message}");
        }
    }

    private async Task SendExitPromptIfOnExit(HexagonalPos worldTilePos, WorldTile tile, Unit unit)
    {
        if (!tile.TryGetExitDirection(unit, out HexDirection exitDirection))
            return;

        HexagonalPos neighborPos = worldTilePos + exitDirection.ToOffset();
        await Clients.Caller.SendAsync("ExitPrompt", new
        {
            WorldTilePos = worldTilePos,
            BoardPos = unit.CurrentBoardPos,
            ExitDirection = exitDirection.ToString(),
            NeighborTilePos = neighborPos,
        });
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
