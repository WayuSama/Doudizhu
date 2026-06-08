using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DoudizhuPlugin.DoudizhuCore;

namespace DoudizhuPlugin.UI;

public class DoudizhuWindow : Window
{
    public DoudizhuGameState? CurrentState { get; set; }
    public List<Card> MyHand { get; set; } = [];
    public int MySeat { get; set; } = 0;
    public HashSet<int> SelectedIndices { get; } = [];

    public event Action? OnDealRequested;
    public event Action<int>? OnBidRequested;
    public event Action<List<Card>>? OnPlayRequested;
    public event Action? OnPassRequested;
    public event Action? OnRoomCreateRequested;
    public event Action<string>? OnRoomJoinRequested;
    public event Action? OnStartGameRequested;
    public event Action? OnRefreshStateRequested;
    public event Action? OnLeaveRoomRequested;

    public string RoomId { get; set; } = "";
    public string ServerUrl { get; set; } = "http://localhost:5123";
    public string PlayerName { get; set; } = "Player";
    public string JoinRoomIdInput { get; set; } = "";
    public string StatusMessage { get; set; } = "";

    public DoudizhuWindow() : base("斗地主##Doudizhu")
    {
        Size = new Vector2(900, 650);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawTopBar();
        ImGui.Separator();
        var state = CurrentState;
        if (state == null || state.Phase == GamePhase.Waiting)
        {
            DrawLobbyPanel();
            return;
        }
        switch (state.Phase)
        {
            case GamePhase.Dealing: DrawDealingPhase(state); break;
            case GamePhase.Bidding: DrawBiddingPhase(state); break;
            case GamePhase.Playing: DrawPlayingPhase(state); break;
            case GamePhase.Finished: DrawFinishedPhase(state); break;
        }
    }

    private void DrawTopBar()
    {
        ImGui.Text($"房间: {(string.IsNullOrEmpty(RoomId) ? "未加入" : RoomId)}");
        if (!string.IsNullOrEmpty(RoomId))
        {
            ImGui.SameLine();
            if (ImGui.Button("复制##copyRoom"))
                ImGui.SetClipboardText(RoomId);
        }
        ImGui.SameLine(300);
        ImGui.Text($"状态: {StatusMessage}");
    }

    private void DrawLobbyPanel()
    {
        if (!string.IsNullOrEmpty(RoomId) && (CurrentState == null || CurrentState.Phase == GamePhase.Waiting))
        {
            DrawRoomLobby();
            return;
        }

        ImGui.Separator();
        ImGui.Text("斗地主 - 游戏大厅");
        ImGui.Spacing();
        ImGui.Text("玩家名称:");
        ImGui.SameLine();
        var name = PlayerName;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("##playerName", ref name, 32)) PlayerName = name;
        ImGui.Spacing();
        ImGui.Text("服务器地址:");
        ImGui.SameLine();
        var url = ServerUrl;
        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("##serverUrl", ref url, 128)) ServerUrl = url;
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("创建房间", new Vector2(150, 40)))
            OnRoomCreateRequested?.Invoke();
        ImGui.Spacing();
        ImGui.Text("加入房间 ID:");
        ImGui.SameLine();
        var joinId = JoinRoomIdInput;
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##joinRoomId", ref joinId, 32);
        JoinRoomIdInput = joinId;
        ImGui.SameLine();
        if (ImGui.Button("加入", new Vector2(80, 0)))
            OnRoomJoinRequested?.Invoke(JoinRoomIdInput);
    }

    private void DrawRoomLobby()
    {
        ImGui.Separator();
        ImGui.Text("房间大厅");
        ImGui.Spacing();
        ImGui.Text("当前玩家:");
        ImGui.Spacing();
        var state = CurrentState;
        if (state?.PlayerNames != null)
        {
            for (int seat = 0; seat < 3; seat++)
            {
                string playerName = state.PlayerNames.GetValueOrDefault(seat, "");
                if (!string.IsNullOrEmpty(playerName))
                {
                    bool isMe = seat == MySeat;
                    string display = $"  Seat {seat}: {playerName}{(isMe ? " (你)" : "")}";
                    ImGui.TextColored(isMe ? new Vector4(0, 1, 0.5f, 1) : new Vector4(1, 1, 1, 1), display);
                }
                else
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"  Seat {seat}: 等待加入...");
            }
        }
        else
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "  (等待加载...)");

        int playerCount = state?.PlayerCount ?? 0;
        ImGui.Spacing();
        ImGui.Text($"人数: {playerCount}/3");
        ImGui.Spacing();
        if (ImGui.Button("刷新", new Vector2(80, 30)))
            OnRefreshStateRequested?.Invoke();
        ImGui.SameLine();
        if (playerCount >= 3)
        {
            if (ImGui.Button("开始游戏", new Vector2(120, 30)))
                OnStartGameRequested?.Invoke();
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("开始游戏 (需3人)", new Vector2(150, 30));
            ImGui.EndDisabled();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.Button("退出房间", new Vector2(120, 30)))
            OnLeaveRoomRequested?.Invoke();
    }

    private void DrawDealingPhase(DoudizhuGameState state)
    {
        ImGui.Spacing();
        ImGui.Text("发牌中...");
        DrawPlayerHandsInfo(state);
        if (ImGui.Button("开始发牌", new Vector2(120, 35)))
            OnDealRequested?.Invoke();
    }

    private void DrawBiddingPhase(DoudizhuGameState state)
    {
        ImGui.Spacing();
        ImGui.Text("叫地主阶段");
        ImGui.Text($"当前叫地主分数: {(state.LandlordSeat >= 0 ? state.LandlordSeat.ToString() : "待定")}");
        DrawOpponentAreas(state);
        DrawPlayerHandsInfo(state);
        ImGui.Spacing();
        if (MySeat == state.CurrentTurn)
        {
            ImGui.Text("请选择:");
            if (ImGui.Button("不叫", new Vector2(80, 35))) OnBidRequested?.Invoke(0);
            ImGui.SameLine();
            if (ImGui.Button("1 分", new Vector2(80, 35))) OnBidRequested?.Invoke(1);
            ImGui.SameLine();
            if (ImGui.Button("2 分", new Vector2(80, 35))) OnBidRequested?.Invoke(2);
            ImGui.SameLine();
            if (ImGui.Button("3 分", new Vector2(80, 35))) OnBidRequested?.Invoke(3);
        }
        else
            ImGui.Text($"等待 Seat {state.CurrentTurn} 叫地主...");
    }

    private void DrawPlayingPhase(DoudizhuGameState state)
    {
        DrawOpponentAreas(state);
        ImGui.Spacing();
        DrawLastPlayedCards(state);
        if (state.BottomCards.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Text("底牌:");
            ImGui.SameLine();
            DrawCardRow(state.BottomCards, false, []);
        }
        ImGui.Spacing();
        ImGui.Separator();
        DrawMyHandArea(state);
        ImGui.Spacing();
        if (MySeat == state.CurrentTurn)
        {
            ImGui.Text("轮到你出牌!");
            ImGui.SameLine();
            if (ImGui.Button("出牌", new Vector2(80, 35)))
            {
                var selected = SelectedIndices.OrderBy(i => i).Select(i => MyHand[i]).ToList();
                if (selected.Count > 0) OnPlayRequested?.Invoke(selected);
            }
            ImGui.SameLine();
            if (ImGui.Button("不出", new Vector2(80, 35)))
                OnPassRequested?.Invoke();
        }
        else
            ImGui.Text($"等待 Seat {state.CurrentTurn} 出牌...");
        ImGui.SameLine();
        if (ImGui.Button("排序", new Vector2(60, 35)))
            MyHand = MyHand.OrderBy(c => c.Rank).ThenBy(c => (int)c.Suit).ToList();
    }

    private void DrawFinishedPhase(DoudizhuGameState state)
    {
        ImGui.Spacing();
        string winnerName = state.PlayerNames.GetValueOrDefault(state.WinnerSeat, $"Seat {state.WinnerSeat}");
        ImGui.Text($"游戏结束! 赢家: {winnerName}");
        DrawOpponentAreas(state);
        DrawPlayerHandsInfo(state);
        ImGui.Spacing();
        if (ImGui.Button("再来一局", new Vector2(120, 35)))
            OnDealRequested?.Invoke();
    }

    private void DrawOpponentAreas(DoudizhuGameState state)
    {
        for (int seat = 1; seat <= 2; seat++)
        {
            string name = state.PlayerNames.GetValueOrDefault(seat, $"Seat {seat}");
            int handCount = state.HandCounts.GetValueOrDefault(seat, 0);
            bool isLandlord = state.LandlordSeat == seat;
            bool isCurrentTurn = state.CurrentTurn == seat;
            string label = $"{name} {(isLandlord ? "[地主]" : "[农民]")} 剩余: {handCount} 张";
            if (isCurrentTurn) label += " <--";
            ImGui.TextColored(isCurrentTurn ? new Vector4(1, 1, 0, 1) : new Vector4(1, 1, 1, 1), label);
            if (handCount > 0)
            {
                for (int i = 0; i < handCount && i < 25; i++)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.3f, 0.5f, 0.8f, 1), "[]");
                }
            }
        }
    }

    private void DrawMyHandArea(DoudizhuGameState state)
    {
        for (int i = 0; i < MyHand.Count; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool selected = SelectedIndices.Contains(i);
            DrawCardButton(MyHand[i], i, selected);
        }
    }

    private void DrawCardButton(Card card, int index, bool selected)
    {
        Vector4 color = card.Suit switch
        {
            CardSuit.Hearts or CardSuit.Diamonds => new Vector4(1, 0.3f, 0.3f, 1),
            CardSuit.Joker => new Vector4(1, 0, 0, 1),
            _ => new Vector4(0.9f, 0.9f, 0.9f, 1),
        };
        string label = card.ToString();
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.Text, color);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.15f, 0.15f, 1));
            ImGui.PushStyleColor(ImGuiCol.Text, color);
        }
        if (ImGui.Button($"{label}##card{index}", new Vector2(42, 60)))
        {
            if (selected) SelectedIndices.Remove(index);
            else SelectedIndices.Add(index);
        }
        ImGui.PopStyleColor(2);
    }

    private static void DrawCardRow(List<Card> cards, bool showBack, HashSet<int> highlightIndices)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0) ImGui.SameLine();
            Vector4 color = showBack
                ? new Vector4(0.3f, 0.5f, 0.8f, 1)
                : cards[i].Suit switch
                {
                    CardSuit.Hearts or CardSuit.Diamonds => new Vector4(1, 0.3f, 0.3f, 1),
                    CardSuit.Joker => new Vector4(1, 0, 0, 1),
                    _ => new Vector4(0.9f, 0.9f, 0.9f, 1),
                };
            string label = showBack ? "[]" : cards[i].ToString();
            Vector4 bgColor = highlightIndices.Contains(i)
                ? new Vector4(0.2f, 0.6f, 0.2f, 0.6f)
                : new Vector4(0.12f, 0.12f, 0.12f, 1);
            ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Button($"{label}##played{i}", new Vector2(42, 60));
            ImGui.PopStyleColor(2);
        }
    }

    private void DrawLastPlayedCards(DoudizhuGameState state)
    {
        ImGui.Text("上次出牌:");
        ImGui.SameLine();
        if (state.LastPlayedCards.Count > 0)
        {
            string whoPlayed = state.PlayerNames.GetValueOrDefault(state.LastPlaySeat, $"Seat {state.LastPlaySeat}");
            ImGui.Text($"({whoPlayed})");
            ImGui.Spacing();
            DrawCardRow(state.LastPlayedCards, false, []);
        }
        else ImGui.Text("(无)");
    }

    private void DrawPlayerHandsInfo(DoudizhuGameState state)
    {
        ImGui.Spacing();
        foreach (var kvp in state.HandCounts.OrderBy(k => k.Key))
        {
            string name = state.PlayerNames.GetValueOrDefault(kvp.Key, $"Seat {kvp.Key}");
            ImGui.Text($"{name}: {kvp.Value} 张牌");
        }
    }
}
