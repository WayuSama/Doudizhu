using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoudizhuPlugin.DoudizhuCore;

namespace DoudizhuPlugin.Server;

public class ServerResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

public class GameServerClient : IDisposable
{
    private readonly HttpClient _http;
    private string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public string? RoomId { get; private set; }
    public int MySeat { get; private set; }

    public GameServerClient(string baseUrl = "http://localhost:5123")
    {
        _baseUrl = baseUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
    }

    public void SetBaseUrl(string url) => _baseUrl = url;

    public async Task<string?> CreateRoom(string playerName)
    {
        var resp = await PostAsync("/api/room/create", new { playerName });
        if (resp?.Success == true)
        {
            RoomId = resp.Data?.GetProperty("roomId").GetString();
            MySeat = 0;
        }
        return RoomId;
    }

    public async Task<int?> JoinRoom(string roomId, string playerName)
    {
        var resp = await PostAsync("/api/room/join", new { roomId, playerName });
        if (resp?.Success == true)
        {
            RoomId = roomId;
            var seat = resp.Data?.GetProperty("seat").GetInt32() ?? -1;
            MySeat = seat;
            return seat;
        }
        return null;
    }

    public async Task<(bool ok, string msg)> LeaveRoom()
    {
        var resp = await PostAsync($"/api/room/{RoomId}/leave", new { seat = MySeat });
        var ok = resp?.Success ?? false;
        var msg = resp?.Message ?? "网络错误";
        if (ok)
        {
            RoomId = null;
            MySeat = 0;
        }
        return (ok, msg);
    }

    public async Task<DoudizhuGameState?> StartGame()
    {
        var resp = await PostAsync($"/api/game/{RoomId}/start", null);
        if (resp?.Success == true)
            return ParseGameState(resp.Data);
        return null;
    }

    public async Task<DoudizhuGameState?> DealCards()
    {
        var resp = await PostAsync($"/api/game/{RoomId}/deal", null);
        if (resp?.Success == true)
            return ParseGameState(resp.Data);
        return null;
    }

    public async Task<DoudizhuGameState?> Bid(int score)
    {
        var resp = await PostAsync($"/api/game/{RoomId}/bid", new { seat = MySeat, score });
        if (resp?.Success == true)
            return ParseGameState(resp.Data);
        return null;
    }

    public async Task<(bool ok, string message, DoudizhuGameState? state)> PlayCards(List<Card> cards)
    {
        var cardsStr = DoudizhuGameLogic.SerializeCards(cards);
        var resp = await PostAsync($"/api/game/{RoomId}/play", new { seat = MySeat, cards = cardsStr });
        if (resp == null) return (false, "网络错误", null);
        if (resp.Success) return (true, "", ParseGameState(resp.Data));
        return (false, resp.Message, null);
    }

    public async Task<(bool ok, string message, DoudizhuGameState? state)> Pass()
    {
        var resp = await PostAsync($"/api/game/{RoomId}/pass", new { seat = MySeat });
        if (resp == null) return (false, "网络错误", null);
        if (resp.Success) return (true, "", ParseGameState(resp.Data));
        return (false, resp.Message, null);
    }

    public async Task<DoudizhuGameState?> GetGameState()
    {
        var resp = await GetAsync($"/api/game/{RoomId}/state");
        if (resp?.Success == true) return ParseGameState(resp.Data);
        return null;
    }

    public async Task<List<Card>?> GetMyHand()
    {
        var resp = await GetAsync($"/api/game/{RoomId}/hand?seat={MySeat}");
        if (resp?.Success == true)
        {
            var cardsStr = resp.Data?.GetProperty("cards").GetString() ?? "";
            return DoudizhuGameLogic.DeserializeHand(cardsStr);
        }
        return null;
    }

    private async Task<ServerResponse?> PostAsync(string path, object? body)
    {
        try
        {
            var content = body != null ? JsonContent.Create(body, options: _jsonOptions) : null;
            var response = await _http.PostAsync(_baseUrl + path, content);
            return await response.Content.ReadFromJsonAsync<ServerResponse>(_jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DDZ] POST {path} failed: {ex.Message}");
            return null;
        }
    }

    private async Task<ServerResponse?> GetAsync(string path)
    {
        try
        {
            var response = await _http.GetAsync(_baseUrl + path);
            return await response.Content.ReadFromJsonAsync<ServerResponse>(_jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DDZ] GET {path} failed: {ex.Message}");
            return null;
        }
    }

    private static DoudizhuGameState? ParseGameState(JsonElement? data)
    {
        if (data == null) return null;
        try
        {
            var raw = data.Value.GetRawText();
            var opts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };
            var state = JsonSerializer.Deserialize<DoudizhuGameState>(raw, opts);
            if (state == null) return null;
            if (data.Value.TryGetProperty("bottomCards", out var bc))
                state.BottomCards = DoudizhuGameLogic.DeserializeCards(bc.GetString() ?? "");
            if (data.Value.TryGetProperty("lastPlayedCards", out var lpc))
                state.LastPlayedCards = DoudizhuGameLogic.DeserializeCards(lpc.GetString() ?? "");
            return state;
        }
        catch { return null; }
    }

    public void Dispose() { _http.Dispose(); }
}
