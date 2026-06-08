using DoudizhuServer.Game;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RoomManager>();

var app = builder.Build();

var roomManager = app.Services.GetRequiredService<RoomManager>();

// ==================== Room APIs ====================

app.MapPost("/api/room/create", (CreateRoomRequest req) =>
{
    var room = roomManager.CreateRoom();
    room.Join(req.PlayerName ?? "Player");
    return Results.Ok(new { success = true, data = new { roomId = room.RoomId } });
});

app.MapPost("/api/room/join", (JoinRoomRequest req) =>
{
    var room = roomManager.GetRoom(req.RoomId ?? "");
    if (room == null)
        return Results.Ok(new { success = false, message = "房间不存在" });

    var (ok, msg, seat) = room.Join(req.PlayerName ?? "Player");
    return Results.Ok(new { success = ok, message = msg, data = ok ? new { seat } : null });
});

app.MapPost("/api/room/{roomId}/leave", (string roomId, SeatRequest req) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var (ok, msg, remaining) = room.Leave(req.Seat);
    if (room.IsEmpty)
        roomManager.RemoveRoom(roomId);

    return Results.Ok(new { success = ok, message = msg, data = new { remaining } });
});

// ==================== Game APIs ====================

app.MapPost("/api/game/{roomId}/start", (string roomId) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var (ok, msg) = room.Start();
    return Results.Ok(new { success = ok, message = msg, data = BuildStateData(room) });
});

app.MapPost("/api/game/{roomId}/deal", (string roomId) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var (ok, msg) = room.Deal();
    return Results.Ok(new { success = ok, message = msg, data = BuildStateData(room) });
});

app.MapPost("/api/game/{roomId}/bid", (string roomId, BidRequest req) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var (ok, msg) = room.Bid(req.Seat, req.Score);
    return Results.Ok(new { success = ok, message = msg, data = BuildStateData(room) });
});

app.MapPost("/api/game/{roomId}/play", (string roomId, PlayRequest req) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var cards = string.IsNullOrEmpty(req.Cards) ? [] : GameRoom.DeserializeCards(req.Cards);
    var (ok, msg) = room.PlayCards(req.Seat, cards);
    return Results.Ok(new { success = ok, message = msg, data = BuildStateData(room) });
});

app.MapPost("/api/game/{roomId}/pass", (string roomId, SeatRequest req) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    var (ok, msg) = room.Pass(req.Seat);
    return Results.Ok(new { success = ok, message = msg, data = BuildStateData(room) });
});

app.MapGet("/api/game/{roomId}/state", (string roomId) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });

    return Results.Ok(new { success = true, data = BuildStateData(room) });
});

app.MapGet("/api/game/{roomId}/hand", (string roomId, int seat) =>
{
    var room = roomManager.GetRoom(roomId);
    if (room == null) return Results.NotFound(new { error = "房间不存在" });
    if (seat < 0 || seat > 2) return Results.Ok(new { success = false, message = "无效座位" });

    return Results.Ok(new { success = true, data = new { cards = room.GetHandString(seat) } });
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "ok", rooms = roomManager.RoomCount }));

var url = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5123";
Console.WriteLine($"斗地主服务器已启动 → {url}");
app.Run(url);

// ==================== Helper ====================

static object BuildStateData(GameRoom room) => new
{
    roomId = room.RoomId,
    phase = (int)room.Phase,
    playerCount = room.PlayerCount,
    statusMessage = room.StatusMessage,
    landlordSeat = room.LandlordSeat,
    currentTurn = room.CurrentTurn,
    lastPlaySeat = room.LastPlaySeat,
    lastPlayedCards = room.LastPlayCardsString,
    passCount = room.PassCount,
    bottomCards = room.BottomCardsString,
    handCounts = room.GetHandCounts(),
    playerNames = room.GetPlayerNames(),
    winnerSeat = room.WinSeat,
};

// ==================== Request DTOs ====================

record CreateRoomRequest(string? PlayerName);
record JoinRoomRequest(string? RoomId, string? PlayerName);
record BidRequest(int Seat, int Score);
record PlayRequest(int Seat, string? Cards);
record SeatRequest(int Seat);
