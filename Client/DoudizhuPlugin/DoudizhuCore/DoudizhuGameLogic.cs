using System.Text;

namespace DoudizhuPlugin.DoudizhuCore;

public class DoudizhuGameLogic
{
    public static PlayResult ValidatePlay(List<Card> selectedCards, List<Card> handCards,
        DoudizhuGameState state, int playerSeat)
    {
        if (state.CurrentTurn != playerSeat)
            return PlayResult.NotYourTurn;

        var pattern = CardPattern.Analyze(selectedCards);
        if (pattern == null)
            return PlayResult.InvalidPattern;

        foreach (var c in selectedCards)
        {
            if (!handCards.Any(hc => hc.Rank == c.Rank && hc.Suit == c.Suit))
                return PlayResult.InvalidPattern;
        }

        CardPattern? lastPattern = null;
        if (state.LastPlayedCards.Count > 0 && state.LastPlaySeat != playerSeat)
            lastPattern = CardPattern.Analyze(state.LastPlayedCards);

        if (!CardPattern.CanBeat(pattern, lastPattern))
            return PlayResult.CannotBeat;

        return PlayResult.Ok;
    }

    public static string SerializeHand(List<Card> hand)
    {
        var sb = new StringBuilder();
        foreach (var c in hand)
            sb.Append(c.IsJoker ? (c.Rank == 16 ? "S" : "B") : $"{c.Rank}:{(int)c.Suit},");
        if (sb.Length > 0) sb.Length--;
        return sb.ToString();
    }

    public static List<Card> DeserializeHand(string data)
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
                if (parts.Length == 2 && int.TryParse(parts[0], out int rank) && int.TryParse(parts[1], out int suit))
                    hand.Add(new Card(rank, (CardSuit)suit));
            }
        }
        return hand;
    }

    public static string SerializeCards(List<Card> cards) => SerializeHand(cards);

    public static List<Card> DeserializeCards(string data) => DeserializeHand(data);
}
