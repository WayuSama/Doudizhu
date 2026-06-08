namespace DoudizhuPlugin.DoudizhuCore;

public static class CardDeck
{
    private static readonly Random Rng = new();

    public static List<Card> CreateFullDeck()
    {
        var deck = new List<Card>(54);
        for (int rank = 3; rank <= 15; rank++)
        {
            deck.Add(new Card(rank, CardSuit.Spades));
            deck.Add(new Card(rank, CardSuit.Hearts));
            deck.Add(new Card(rank, CardSuit.Clubs));
            deck.Add(new Card(rank, CardSuit.Diamonds));
        }
        deck.Add(Card.SmallJoker);
        deck.Add(Card.BigJoker);
        return deck;
    }

    public static void Shuffle(List<Card> deck)
    {
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = Rng.Next(n + 1);
            (deck[k], deck[n]) = (deck[n], deck[k]);
        }
    }

    public static (List<Card> player0, List<Card> player1, List<Card> player2, List<Card> bottom)
        Deal()
    {
        var deck = CreateFullDeck();
        Shuffle(deck);

        var p0 = new List<Card>(17);
        var p1 = new List<Card>(17);
        var p2 = new List<Card>(17);
        var bottom = new List<Card>(3);

        for (int i = 0; i < 51; i++)
        {
            var target = (i % 3) switch { 0 => p0, 1 => p1, _ => p2 };
            target.Add(deck[i]);
        }

        bottom.Add(deck[51]);
        bottom.Add(deck[52]);
        bottom.Add(deck[53]);

        p0.Sort();
        p1.Sort();
        p2.Sort();

        return (p0, p1, p2, bottom);
    }
}
