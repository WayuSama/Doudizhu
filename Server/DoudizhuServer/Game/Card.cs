namespace DoudizhuServer.Game;

public enum CardSuit { Spades = 0, Hearts = 1, Clubs = 2, Diamonds = 3, Joker = 4 }

public readonly struct Card : IComparable<Card>, IEquatable<Card>
{
    public int Rank { get; }
    public CardSuit Suit { get; }
    public Card(int rank, CardSuit suit) { Rank = rank; Suit = suit; }
    public bool IsJoker => Suit == CardSuit.Joker;
    public static Card SmallJoker => new(16, CardSuit.Joker);
    public static Card BigJoker => new(17, CardSuit.Joker);

    public string DisplayName => Rank switch
    {
        16 => "SJ", 17 => "BJ",
        11 => "J", 12 => "Q", 13 => "K", 14 => "A", 15 => "2",
        _ => Rank.ToString(),
    };

    public string SuitSymbol => Suit switch
    {
        CardSuit.Spades => "S", CardSuit.Hearts => "H",
        CardSuit.Clubs => "C", CardSuit.Diamonds => "D", _ => "J",
    };

    public override string ToString() => IsJoker ? DisplayName : $"{SuitSymbol}{DisplayName}";

    public int CompareTo(Card other) => Rank.CompareTo(other.Rank);
    public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
    public override bool Equals(object? obj) => obj is Card c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Rank, Suit);
    public static bool operator ==(Card l, Card r) => l.Equals(r);
    public static bool operator !=(Card l, Card r) => !l.Equals(r);
}
