using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using DoudizhuPlugin.DoudizhuCore;
using DoudizhuPlugin.Server;
using DoudizhuPlugin.UI;

namespace DoudizhuPlugin;

public sealed class DoudizhuPlugin : IDalamudPlugin
{
    public string Name => "斗地主";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private readonly WindowSystem _windowSystem = new("Doudizhu");
    private readonly DoudizhuWindow _mainWindow;
    private readonly GameServerClient _serverClient;

    private List<Card> _myHand = [];
    private DoudizhuGameState? _currentState;

    private const string CommandName = "/ddz";

    public DoudizhuPlugin()
    {
        _serverClient = new GameServerClient();

        _mainWindow = new DoudizhuWindow();
        _mainWindow.OnRoomCreateRequested += HandleRoomCreate;
        _mainWindow.OnRoomJoinRequested += HandleRoomJoin;
        _mainWindow.OnDealRequested += HandleDeal;
        _mainWindow.OnBidRequested += HandleBid;
        _mainWindow.OnPlayRequested += HandlePlay;
        _mainWindow.OnPassRequested += HandlePass;
        _mainWindow.OnStartGameRequested += HandleStartGame;
        _mainWindow.OnRefreshStateRequested += HandleRefreshState;
        _mainWindow.OnLeaveRoomRequested += HandleLeaveRoom;

        _windowSystem.AddWindow(_mainWindow);
        PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleWindow;

        CommandManager.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "打开斗地主游戏窗口\n/ddz → 打开/关闭窗口",
        });

        Log.Information("斗地主插件加载成功!");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        CommandManager.RemoveHandler(CommandName);
        _windowSystem.RemoveAllWindows();
        _serverClient.Dispose();
        Log.Information("斗地主插件卸载!");
    }

    private void OnCommand(string command, string args) => ToggleWindow();
    private void ToggleWindow() => _mainWindow.IsOpen = !_mainWindow.IsOpen;

    private async void HandleRoomCreate()
    {
        _mainWindow.StatusMessage = "正在创建房间...";
        _serverClient.SetBaseUrl(_mainWindow.ServerUrl);
        var roomId = await _serverClient.CreateRoom(_mainWindow.PlayerName);
        if (roomId != null)
        {
            _mainWindow.RoomId = roomId;
            _mainWindow.MySeat = 0;
            _mainWindow.StatusMessage = $"房间已创建: {roomId}";
            Log.Information($"Room created: {roomId}");
            var state = await _serverClient.GetGameState();
            UpdateState(state);
        }
        else _mainWindow.StatusMessage = "创建房间失败，请检查服务器地址";
    }

    private async void HandleRoomJoin(string roomId)
    {
        _mainWindow.StatusMessage = $"正在加入房间 {roomId}...";
        _serverClient.SetBaseUrl(_mainWindow.ServerUrl);
        var seat = await _serverClient.JoinRoom(roomId, _mainWindow.PlayerName);
        if (seat != null)
        {
            _mainWindow.RoomId = roomId;
            _mainWindow.MySeat = seat.Value;
            _mainWindow.StatusMessage = $"已加入房间，座位: {seat}";
            Log.Information($"Joined room {roomId}, seat {seat}");
            var state = await _serverClient.GetGameState();
            UpdateState(state);
        }
        else _mainWindow.StatusMessage = "加入房间失败";
    }

    private async void HandleStartGame()
    {
        _mainWindow.StatusMessage = "正在开始游戏...";
        var state = await _serverClient.StartGame();
        if (state != null) { UpdateState(state); _mainWindow.StatusMessage = "游戏已开始，准备发牌..."; }
        else _mainWindow.StatusMessage = "开始游戏失败";
    }

    private async void HandleLeaveRoom()
    {
        _mainWindow.StatusMessage = "正在退出房间...";
        var (ok, msg) = await _serverClient.LeaveRoom();
        if (ok)
        {
            _mainWindow.RoomId = "";
            _mainWindow.MySeat = 0;
            _mainWindow.MyHand = [];
            _mainWindow.CurrentState = null;
            _mainWindow.SelectedIndices.Clear();
            _mainWindow.StatusMessage = "已退出房间";
            _currentState = null;
            _myHand = [];
        }
        else _mainWindow.StatusMessage = $"退出失败: {msg}";
    }

    private async void HandleRefreshState()
    {
        _mainWindow.StatusMessage = "正在刷新...";
        var state = await _serverClient.GetGameState();
        if (state != null) { UpdateState(state); _mainWindow.StatusMessage = "刷新完成"; }
        else _mainWindow.StatusMessage = "刷新失败，请检查服务器连接";
    }

    private async void HandleDeal()
    {
        _mainWindow.StatusMessage = "正在发牌...";
        var state = await _serverClient.DealCards();
        if (state != null)
        {
            UpdateState(state);
            var hand = await _serverClient.GetMyHand();
            if (hand != null) { _myHand = hand; _mainWindow.MyHand = _myHand; }
            _mainWindow.SelectedIndices.Clear();
            _mainWindow.StatusMessage = "发牌完成";
        }
        else _mainWindow.StatusMessage = "发牌失败";
    }

    private async void HandleBid(int score)
    {
        _mainWindow.StatusMessage = $"叫地主: {score} 分";
        var state = await _serverClient.Bid(score);
        UpdateState(state);
        if (state?.LandlordSeat == _mainWindow.MySeat)
        {
            var hand = await _serverClient.GetMyHand();
            if (hand != null) { _myHand = hand; _mainWindow.MyHand = _myHand; }
        }
    }

    private async void HandlePlay(List<Card> selectedCards)
    {
        _mainWindow.StatusMessage = "正在出牌...";
        var (ok, message, state) = await _serverClient.PlayCards(selectedCards);
        if (ok && state != null)
        {
            UpdateState(state);
            foreach (var c in selectedCards)
            {
                var idx = _myHand.FindIndex(hc => hc.Rank == c.Rank && hc.Suit == c.Suit);
                if (idx >= 0) _myHand.RemoveAt(idx);
            }
            _mainWindow.MyHand = _myHand;
            _mainWindow.SelectedIndices.Clear();
            _mainWindow.StatusMessage = "出牌成功";
        }
        else _mainWindow.StatusMessage = $"出牌失败: {message}";
    }

    private async void HandlePass()
    {
        _mainWindow.StatusMessage = "过...";
        var (ok, message, state) = await _serverClient.Pass();
        UpdateState(state);
        if (ok) { _mainWindow.SelectedIndices.Clear(); _mainWindow.StatusMessage = "已过牌"; }
        else _mainWindow.StatusMessage = $"过牌失败: {message}";
    }

    private void UpdateState(DoudizhuGameState? state)
    {
        if (state == null) return;
        _currentState = state;
        _mainWindow.CurrentState = state;
        _mainWindow.MySeat = _serverClient.MySeat;
        if (state.HandCounts.ContainsKey(_mainWindow.MySeat))
            state.HandCounts[_mainWindow.MySeat] = _myHand.Count;
    }
}
