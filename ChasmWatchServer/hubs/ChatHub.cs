using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class GameHub : Hub
{
    private readonly GameState _state;
    public GameHub(GameState state) => _state = state;

    // ── Connection/join ──────────────────────────────────────────────────────

    public async Task JoinRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        var username = Username();
        var room = _state.GetOrCreateRoom(roomName);
        await Clients.Group(roomName).SendAsync("ReceiveMessage", "System",
            $"{username} joined room '{roomName}'");

    }

    public async Task MakeMove(string roomName, int cellIndex)
    {
       
    }

    public async Task RequestRematch(string roomName)
    {
       
    }

    public async Task Resign(string roomName)
    {

    }

    // ── Chat (unchanged from before) ─────────────────────────────────────────

    public async Task SendMessageToGroup(string groupName, string message)
        => await Clients.Group(groupName).SendAsync("ReceiveMessage", Username(), message);

    public async Task SendMessageGlobal(string message)
        => await Clients.All.SendAsync("ReceiveMessage", Username(), message);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string Username() => Context.User?.Identity?.Name ?? "Unknown";
}