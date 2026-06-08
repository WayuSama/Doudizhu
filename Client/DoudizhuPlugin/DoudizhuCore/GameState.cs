namespace DoudizhuPlugin.DoudizhuCore;

public enum GamePhase { Waiting, Dealing, Bidding, Playing, Finished }

public enum Seat { Seat0 = 0, Seat1 = 1, Seat2 = 2 }

public enum PlayResult { Ok, InvalidPattern, CannotBeat, NotYourTurn }

public class DoudizhuGameState
{
    public string RoomId { get; set; } = "";
    public GamePhase Phase { get; set; } = GamePhase.Waiting;
    public int PlayerCount { get; set; } = 0;
    public string StatusMessage { get; set; } = "";
    public int LandlordSeat { get; set; } = -1;
    public int CurrentTurn { get; set; } = -1;
    public int LastPlaySeat { get; set; } = -1;
    public List<Card> LastPlayedCards { get; set; } = [];
    public int PassCount { get; set; } = 0;
    public List<Card> BottomCards { get; set; } = [];
    public Dictionary<int, int> HandCounts { get; set; } = new();
    public Dictionary<int, string> PlayerNames { get; set; } = new();
    public int WinnerSeat { get; set; } = -1;

    public List<Card> LastPlayedCardList
    {
        get => LastPlayedCards;
        set => LastPlayedCards = value;
    }
}
