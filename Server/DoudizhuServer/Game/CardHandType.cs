namespace DoudizhuServer.Game;

public enum CardHandType
{
    Invalid, Single, Pair, Triple, TripleWithOne, TripleWithPair,
    Straight, DoubleStraight,
    TripleStraight, TripleStraightWithWing, TripleStraightWithPair,
    FourWithTwoSingle, FourWithTwoPair, Bomb, Rocket,
}

public class CardPattern
{
    public CardHandType Type { get; set; }
    public int MainRank { get; set; }
    public List<Card> Cards { get; init; } = [];

    public static CardPattern? Analyze(List<Card> cards)
    {
        if (cards.Count == 0) return null;
        var sorted = new List<Card>(cards); sorted.Sort();
        var countMap = GetRankCountMap(sorted);
        int dr = countMap.Count;

        // Rocket
        if (sorted.Count == 2 && sorted[0].Rank == 16 && sorted[1].Rank == 17)
            return new() { Type = CardHandType.Rocket, MainRank = 17, Cards = sorted };

        // Bomb
        if (sorted.Count == 4 && dr == 1)
            return new() { Type = CardHandType.Bomb, MainRank = sorted[0].Rank, Cards = sorted };

        // Single / Pair / Triple
        if (sorted.Count == 1)
            return new() { Type = CardHandType.Single, MainRank = sorted[0].Rank, Cards = sorted };
        if (sorted.Count == 2 && dr == 1)
            return new() { Type = CardHandType.Pair, MainRank = sorted[0].Rank, Cards = sorted };
        if (sorted.Count == 3 && dr == 1)
            return new() { Type = CardHandType.Triple, MainRank = sorted[0].Rank, Cards = sorted };

        // Triple with kicker
        if (sorted.Count == 4 && TryFindTriple(countMap, out var tr))
            return new() { Type = CardHandType.TripleWithOne, MainRank = tr, Cards = sorted };
        if (sorted.Count == 5 && TryFindTriple(countMap, out tr))
            return new() { Type = CardHandType.TripleWithPair, MainRank = tr, Cards = sorted };

        // Four with kickers
        if (sorted.Count == 6 && TryFindQuad(countMap, out var qr))
            return new() { Type = CardHandType.FourWithTwoSingle, MainRank = qr, Cards = sorted };
        if (sorted.Count == 8 && TryFindQuad(countMap, out qr))
            return new() { Type = CardHandType.FourWithTwoPair, MainRank = qr, Cards = sorted };

        // Straight (>=5 cards, no 2 or jokers, all distinct consecutive)
        if (dr == sorted.Count && sorted.Count >= 5 && IsConsecutive(sorted) && sorted[^1].Rank <= 14)
            return new() { Type = CardHandType.Straight, MainRank = sorted[^1].Rank, Cards = sorted };

        // Double straight (>=3 pairs)
        if (sorted.Count % 2 == 0 && dr * 2 == sorted.Count && dr >= 3)
        {
            var ranks = countMap.Keys.OrderBy(r => r).ToList();
            if (IsConsecutiveRanks(ranks) && ranks[^1] <= 14)
                return new() { Type = CardHandType.DoubleStraight, MainRank = ranks[^1], Cards = sorted };
        }

        // Triple straight (plane)
        var triples = countMap.Where(kv => kv.Value >= 3).Select(kv => kv.Key).OrderBy(r => r).ToList();
        if (triples.Count >= 2)
        {
            int chainLen = LongestConsecutive(triples);
            if (chainLen >= 2 && (triples[triples.Count - 1] <= 14))
            {
                int wing = sorted.Count - chainLen * 3;
                if (wing == 0)
                    return new() { Type = CardHandType.TripleStraight, MainRank = triples[triples.Count - 1], Cards = sorted };
                if (wing == chainLen)
                    return new() { Type = CardHandType.TripleStraightWithWing, MainRank = triples[triples.Count - 1], Cards = sorted };
                if (wing == chainLen * 2)
                    return new() { Type = CardHandType.TripleStraightWithPair, MainRank = triples[triples.Count - 1], Cards = sorted };
            }
        }

        return null;
    }

    public static bool CanBeat(CardPattern? newPat, CardPattern? lastPat)
    {
        if (newPat == null) return false;
        if (lastPat == null) return true;
        if (newPat.Type == CardHandType.Rocket) return true;
        if (lastPat.Type == CardHandType.Rocket) return false;
        if (newPat.Type == CardHandType.Bomb && lastPat.Type != CardHandType.Bomb) return true;
        if (lastPat.Type == CardHandType.Bomb && newPat.Type != CardHandType.Bomb) return false;
        if (newPat.Type == lastPat.Type && newPat.Cards.Count == lastPat.Cards.Count)
            return newPat.MainRank > lastPat.MainRank;
        return false;
    }

    private static Dictionary<int, int> GetRankCountMap(List<Card> cards)
    {
        var m = new Dictionary<int, int>();
        foreach (var c in cards) { m.TryGetValue(c.Rank, out var v); m[c.Rank] = v + 1; }
        return m;
    }

    private static bool TryFindTriple(Dictionary<int, int> m, out int r)
    {
        r = m.FirstOrDefault(kv => kv.Value >= 3).Key;
        return r != 0;
    }

    private static bool TryFindQuad(Dictionary<int, int> m, out int r)
    {
        r = m.FirstOrDefault(kv => kv.Value >= 4).Key;
        return r != 0;
    }

    private static bool IsConsecutive(List<Card> cards)
    {
        for (int i = 1; i < cards.Count; i++)
            if (cards[i].Rank != cards[i - 1].Rank + 1) return false;
        return true;
    }

    private static bool IsConsecutiveRanks(List<int> ranks)
    {
        for (int i = 1; i < ranks.Count; i++)
            if (ranks[i] != ranks[i - 1] + 1) return false;
        return true;
    }

    private static int LongestConsecutive(List<int> sorted)
    {
        int best = 1, cur = 1;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == sorted[i - 1] + 1) { cur++; if (cur > best) best = cur; }
            else cur = 1;
        }
        return best;
    }
}
