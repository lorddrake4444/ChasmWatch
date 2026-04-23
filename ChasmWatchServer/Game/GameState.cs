using System.Collections.Concurrent;

public class Room
{
    public string RoomName { get; set; } = "";

}

public class GameState
{
    // One room entry per group name
    public ConcurrentDictionary<string, Room> Rooms = new();

    public Room GetOrCreateRoom(string roomName)
        => Rooms.GetOrAdd(roomName, name => new Room { RoomName = name });
}