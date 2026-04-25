using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class ChatHub : Hub
{
    private string Username() => Context.User?.Identity?.Name ?? "Unknown";

    public async Task JoinRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
    }

    public async Task SendMessageToGroup(string groupName, string message)
        => await Clients.Group(groupName).SendAsync("ReceiveMessage", Username(), message);

    public async Task SendMessageGlobal(string message)
        => await Clients.All.SendAsync("ReceiveMessage", Username(), message);
}