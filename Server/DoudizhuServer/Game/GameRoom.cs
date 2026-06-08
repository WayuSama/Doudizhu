using System.Text;

namespace DoudizhuServer.Game;

public enum GamePhase { Waiting, Dealing, Bidding, Playing, Finished }

/// <summary>
/// 一个游戏房间，管理完整的斗地主对局
/// </summary>
public class GameRoom
{
    public string RoomId { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Waiting;

    // 玩家信息
    public readonly string?[] Players = new string[3];
    public int PlayerCount { get; private set; } = 0;

    // 手牌
    public readonly List<Card>[] Hands = new List<Card>[3] { [], [], [] };
    public List<Card> BottomCards { get; private set; } = [];

    // 地主
    public int LandlordSeat { get; private set; } = -1;
    // 当前回合
    public int CurrentTurn { get; private set; } = -1;
    // 叫地主状态
    public int BiddingSeat { get; private set; } = 0; // 叫地从谁开始
    public int HighestBid { get; private set; } = 0;
    public int HighestBidSeat { get; private set; } = -1;
    public int BidRound { get; private set; } = 0;

    // 出牌状态
    public int LastPlaySeat { get; set; } = -1;
    public List<Card> LastPlayedCards { get; set; } = [];
    public int PassCount { get; set; } = 0;
    public int WinSeat { get; private set; } = -1;

    public string StatusMessage { get; private set; } = "";

    public GameRoom(string roomId)
    {
        RoomId = roomId;
    }

    // ================= 玩家管理 =================

    public (bool ok, string msg, int seat) Join(string playerName)
    {
        if (PlayerCount >= 3) return (false, "房间已满", -1);
        int seat = PlayerCount;
        Players[seat] = playerName;
        PlayerCount++;
        StatusMessage = $"玩家 {playerName} 加入, 当前 {PlayerCount}/3";
        return (true, "", seat);
    }

    /// <summary>
    /// 离开房间
    /// </summary>
    public (bool ok, string msg, int remaining) Leave(int seat)
    {
        if (seat < 0 || seat >= 3) return (false, "无效座位", PlayerCount);
        if (Players[seat] == null) return (false, "该座位无人", PlayerCount);

        string name = Players[seat]!;

        // 如果游戏进行中，直接踢出该玩家（手牌清零视为输）
        if (Phase == GamePhase.Playing || Phase == GamePhase.Bidding)
        {
            Hands[seat].Clear();
            // 如果是该玩家的回合，跳到下一个
            if (CurrentTurn == seat)
                CurrentTurn = (CurrentTurn + 1) % 3;
            // 如果只剩 1 人或更少，游戏直接结束
            int activePlayers = 0;
            int lastActive = -1;
            for (int i = 0; i < 3; i++)
            {
                if (Hands[i].Count > 0 && Players[i] != null) { activePlayers++; lastActive = i; }
            }
            if (activePlayers <= 1)
            {
                WinSeat = lastActive >= 0 ? lastActive : (seat + 1) % 3;
                Phase = GamePhase.Finished;
            }
        }

        Players[seat] = null;
        PlayerCount--;
        StatusMessage = $"玩家 {name} 离开房间, 当前 {PlayerCount}/3";

        // 重置到等待状态
        if (Phase != GamePhase.Waiting && PlayerCount < 3)
        {
            Phase = GamePhase.Waiting;
            ResetGameState();
            StatusMessage = $"玩家 {name} 离开，游戏取消, 当前 {PlayerCount}/3";
        }

        return (true, "", PlayerCount);
    }

    private void ResetGameState()
    {
        Phase = GamePhase.Waiting;
        LandlordSeat = -1;
        CurrentTurn = -1;
        LastPlaySeat = -1;
        LastPlayedCards = [];
        PassCount = 0;
        WinSeat = -1;
        Hands[0] = []; Hands[1] = []; Hands[2] = [];
        BottomCards = [];
        BiddingSeat = 0;
        HighestBid = 0;
        HighestBidSeat = -1;
        BidRound = 0;
    }

    public bool IsEmpty => PlayerCount == 0;

    /// <summary>
    /// 手动开始游戏（需 3 人）
    /// </summary>
    public (bool ok, string msg) Start()
    {
        if (PlayerCount < 3) return (false, $"玩家不足 3 人（当前 {PlayerCount}）");
        if (Phase != GamePhase.Waiting) return (false, "游戏已在进行中");
        return Deal();
    }

    // ================= 发牌 =================

    public (bool ok, string msg) Deal()
    {
        if (PlayerCount < 3)
            return (false, "玩家不足 3 人");

        var deck = CreateFullDeck();
        Shuffle(deck);

        Hands[0] = deck.GetRange(0, 17); Hands[0].Sort();
        Hands[1] = deck.GetRange(17, 17); Hands[1].Sort();
        Hands[2] = deck.GetRange(34, 17); Hands[2].Sort();
        BottomCards = deck.GetRange(51, 3);

        Phase = GamePhase.Dealing;
        StatusMessage = "发牌完成，开始叫地主";
        StartBidding();
        return (true, "");
    }

    // ================= 叫地主 =================

    private void StartBidding()
    {
        Phase = GamePhase.Bidding;
        BiddingSeat = new Random().Next(3);
        CurrentTurn = BiddingSeat;
        HighestBid = 0;
        HighestBidSeat = -1;
        BidRound = 0;
    }

    public (bool ok, string msg) Bid(int seat, int score)
    {
        if (Phase != GamePhase.Bidding) return (false, "不在叫地主阶段");
        if (seat != CurrentTurn) return (false, "不是你的叫地回合");
        if (score < 0 || score > 3) return (false, "分数必须在 0~3");
        if (score > 0 && score <= HighestBid) return (false, $"出分必须高于 {HighestBid}");

        if (score > 0)
        {
            HighestBid = score;
            HighestBidSeat = seat;
        }

        BidRound++;
        CurrentTurn = (CurrentTurn + 1) % 3;

        // 判断叫地主是否结束
        // 有人叫了 3 分 → 直接结束
        if (HighestBid >= 3)
        {
            LandlordSeat = HighestBidSeat;
            GiveBottomCards();
            return (true, "");
        }

        // 所有人都叫过一轮且没人叫 → 重新发牌
        if (BidRound >= 3 && HighestBid == 0)
        {
            StatusMessage = "无人叫地主，重新发牌";
            return Deal();
        }

        // 三个都叫完了 → 分数最高的是地主
        if (BidRound >= 3)
        {
            LandlordSeat = HighestBidSeat;
            GiveBottomCards();
            return (true, "");
        }

        return (true, "");
    }

    private void GiveBottomCards()
    {
        Hands[LandlordSeat].AddRange(BottomCards);
        Hands[LandlordSeat].Sort();
        Phase = GamePhase.Playing;
        CurrentTurn = LandlordSeat;
        LastPlaySeat = -1;
        LastPlayedCards = [];
        PassCount = 0;
        StatusMessage = $"{Players[LandlordSeat]} 是地主! 开始出牌";
    }

    // ================= 出牌 =================

    public (bool ok, string msg) PlayCards(int seat, List<Card> submittedCards)
    {
        if (Phase != GamePhase.Playing) return (false, "不在出牌阶段");
        if (seat != CurrentTurn) return (false, "不是你的回合");
        if (submittedCards.Count == 0) return (false, "请选择要出的牌");

        var hand = Hands[seat];

        // 验证牌都在手牌里
        if (!CardsInHand(submittedCards, hand))
            return (false, "手牌中没有这些牌");

        var pattern = CardPattern.Analyze(submittedCards);
        if (pattern == null)
            return (false, "无效牌型");

        // 不是自由出牌时，需要能打过上家
        CardPattern? lastPattern = null;
        if (LastPlayedCards.Count > 0 && LastPlaySeat != seat)
        {
            lastPattern = CardPattern.Analyze(LastPlayedCards);
        }

        if (!CardPattern.CanBeat(pattern, lastPattern))
            return (false, "打不过上家的牌");

        // 从手牌中移除
        RemoveFromHand(submittedCards, hand);

        LastPlaySeat = seat;
        LastPlayedCards = new List<Card>(submittedCards);
        PassCount = 0;

        // 检查胜利
        if (hand.Count == 0)
        {
            WinSeat = seat;
            Phase = GamePhase.Finished;
            StatusMessage = $"{Players[seat]} 赢了!";
            return (true, "");
        }

        CurrentTurn = (CurrentTurn + 1) % 3;
        return (true, "");
    }

    public (bool ok, string msg) Pass(int seat)
    {
        if (Phase != GamePhase.Playing) return (false, "不在出牌阶段");
        if (seat != CurrentTurn) return (false, "不是你的回合");
        // 自由出牌时不能过
        if (LastPlaySeat == seat || LastPlayCardsString == "")
            return (false, "你不能过牌（自由出牌轮）");

        PassCount++;
        CurrentTurn = (CurrentTurn + 1) % 3;

        // 其他两人都过了 → 出牌人重新自由出牌
        if (PassCount >= 2)
        {
            // 重置出牌状态，让上家（最后出牌的人）重新出
            CurrentTurn = LastPlaySeat;
            LastPlayedCards = [];
            LastPlaySeat = -1;
            PassCount = 0;
        }

        return (true, "");
    }

    // ================= 序列化 =================

    public string GetHandString(int seat) => SerializeCards(Hands[seat]);
    public string BottomCardsString => SerializeCards(BottomCards);
    public string LastPlayCardsString => SerializeCards(LastPlayedCards);

    public Dictionary<int, int> GetHandCounts() => new()
    {
        [0] = Hands[0].Count, [1] = Hands[1].Count, [2] = Hands[2].Count,
    };

    public Dictionary<int, string> GetPlayerNames() => new()
    {
        [0] = Players[0] ?? "Seat0",
        [1] = Players[1] ?? "Seat1",
        [2] = Players[2] ?? "Seat2",
    };

    // ================= 内部工具 =================

    private static List<Card> CreateFullDeck()
    {
        var d = new List<Card>(54);
        for (int r = 3; r <= 15; r++)
            for (int s = 0; s < 4; s++)
                d.Add(new(r, (CardSuit)s));
        d.Add(Card.SmallJoker);
        d.Add(Card.BigJoker);
        return d;
    }

    private static void Shuffle(List<Card> deck)
    {
        var rng = new Random();
        int n = deck.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); (deck[k], deck[n]) = (deck[n], deck[k]); }
    }

    private static bool CardsInHand(List<Card> submitted, List<Card> hand)
    {
        var handCopy = new List<Card>(hand);
        foreach (var c in submitted)
        {
            var idx = handCopy.FindIndex(h => h.Rank == c.Rank && h.Suit == c.Suit);
            if (idx < 0) return false;
            handCopy.RemoveAt(idx);
        }
        return true;
    }

    private static void RemoveFromHand(List<Card> submitted, List<Card> hand)
    {
        foreach (var c in submitted)
        {
            var idx = hand.FindIndex(h => h.Rank == c.Rank && h.Suit == c.Suit);
            if (idx >= 0) hand.RemoveAt(idx);
        }
    }

    public static string SerializeCards(List<Card> cards)
    {
        var sb = new StringBuilder();
        foreach (var c in cards)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(c.IsJoker ? (c.Rank == 16 ? "S" : "B") : $"{c.Rank}:{(int)c.Suit}");
        }
        return sb.ToString();
    }

    public static List<Card> DeserializeCards(string data)
    {
        var hand = new List<Card>();
        if (string.IsNullOrEmpty(data)) return hand;
        foreach (var token in data.Split(','))
        {
            if (token == "S") hand.Add(Card.SmallJoker);
            else if (token == "B") hand.Add(Card.BigJoker);
            else
            {
                var parts = token.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int r) && int.TryParse(parts[1], out int s))
                    hand.Add(new(r, (CardSuit)s));
            }
        }
        return hand;
    }
}

/// <summary>
/// 全局房间管理器（线程安全）
/// </summary>
public class RoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _lock = new();

    public GameRoom CreateRoom()
    {
        var roomId = GenerateRoomId();
        var room = new GameRoom(roomId);
        lock (_lock) { _rooms[roomId] = room; }
        return room;
    }

    public GameRoom? GetRoom(string roomId)
    {
        lock (_lock) { _rooms.TryGetValue(roomId, out var r); return r; }
    }

    public void RemoveRoom(string roomId)
    {
        lock (_lock) { _rooms.Remove(roomId); }
    }

    public int RoomCount { get { lock (_lock) return _rooms.Count; } }

    private static string GenerateRoomId()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}
