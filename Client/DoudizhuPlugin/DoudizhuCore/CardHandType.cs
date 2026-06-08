namespace DoudizhuPlugin.DoudizhuCore;

public enum CardHandType
{
    Invalid, Single, Pair, Triple, TripleWithOne, TripleWithPair,
    Straight, DoubleStraight,
    TripleStraight, TripleStraightWithWing, TripleStraightWithPair,
    FourWithTwoSingle, FourWithTwoPair, Bomb, Rocket,
}

public class CardPattern
{
    public CardHandType Type { get; init; }
    public int MainRank { get; init; }
    public int Length { get; init; }
    public List<Card> Cards { get; init; } = [];

    public static CardPattern? Analyze(List<Card> cards)
    {
        if (cards.Count == 0) return null;

        var sorted = new List<Card>(cards);
        sorted.Sort();
        var countMap = GetRankCountMap(sorted);
        int distinctRanks = countMap.Count;

        if (sorted.Count == 2 && sorted[0].Rank == 16 && sorted[1].Rank == 17)
            return new CardPattern { Type = CardHandType.Rocket, MainRank = 17 };

        if (sorted.Count == 4 && distinctRanks == 1)
            return new CardPattern { Type = CardHandType.Bomb, MainRank = sorted[0].Rank };

        if (sorted.Count == 1)
            return new CardPattern { Type = CardHandType.Single, MainRank = sorted[0].Rank };

        if (sorted.Count == 2 && distinctRanks == 1)
            return new CardPattern { Type = CardHandType.Pair, MainRank = sorted[0].Rank };

        if (sorted.Count == 3 && distinctRanks == 1)
            return new CardPattern { Type = CardHandType.Triple, MainRank = sorted[0].Rank };

        if (sorted.Count == 4 && TryFindTriple(countMap, out int tr) && countMap.Values.Count(v => v >= 1) == 2)
            return new CardPattern { Type = CardHandType.TripleWithOne, MainRank = tr };
        if (sorted.Count == 5 && TryFindTriple(countMap, out tr) && countMap.Values.Count(v => v >= 1) == 2)
            return new CardPattern { Type = CardHandType.TripleWithPair, MainRank = tr };

        if (sorted.Count == 6 && TryFindQuad(countMap, out int qr) && countMap.Values.Count(v => v >= 1) == 2)
            return new CardPattern { Type = CardHandType.FourWithTwoSingle, MainRank = qr };
        if (sorted.Count == 8 && TryFindQuad(countMap, out qr) && countMap.Values.Count(v => v >= 1) == 3)
            return new CardPattern { Type = CardHandType.FourWithTwoPair, MainRank = qr };

        if (distinctRanks == sorted.Count && sorted.Count >= 5 && IsConsecutiveStraight(sorted) && sorted[^1].Rank <= 14)
            return new CardPattern { Type = CardHandType.Straight, MainRank = sorted[^1].Rank, Length = sorted.Count };

        if (sorted.Count % 2 == 0 && distinctRanks * 2 == sorted.Count && distinctRanks >= 3)
        {
            var ranks = countMap.Keys.OrderBy(r => r).ToList();
            if (IsConsecutiveRanks(ranks) && ranks[^1] <= 14)
                return new CardPattern { Type = CardHandType.DoubleStraight, MainRank = ranks[^1], Length = ranks.Count };
        }

        var triples = countMap.Where(kv => kv.Value >= 3).Select(kv => kv.Key).OrderBy(r => r).ToList();
        if (triples.Count >= 2)
        {
            int chainLen = FindLongestConsecutive(triples);
            if (chainLen >= 2 && triples[^1] <= 14)
            {
                int wingCount = sorted.Count - chainLen * 3;
                if (wingCount == 0)
                    return new CardPattern { Type = CardHandType.TripleStraight, MainRank = triples[triples.Count - 1], Length = chainLen };
                if (wingCount == chainLen)
                    return new CardPattern { Type = CardHandType.TripleStraightWithWing, MainRank = triples[triples.Count - 1], Length = chainLen };
                if (wingCount == chainLen * 2)
                    return new CardPattern { Type = CardHandType.TripleStraightWithPair, MainRank = triples[triples.Count - 1], Length = chainLen };
            }
        }

        return null;
    }

    public static bool CanBeat(CardPattern? newPattern, CardPattern? lastPattern)
    {
        if (newPattern == null) return false;
        if (lastPattern == null) return true;
        if (newPattern.Type == CardHandType.Rocket) return true;
        if (lastPattern.Type == CardHandType.Rocket) return false;
        if (newPattern.Type == CardHandType.Bomb && lastPattern.Type != CardHandType.Bomb) return true;
        if (lastPattern.Type == CardHandType.Bomb && newPattern.Type != CardHandType.Bomb) return false;
        if (newPattern.Type == lastPattern.Type && newPattern.Cards.Count == lastPattern.Cards.Count)
            return newPattern.MainRank > lastPattern.MainRank;
        return false;
    }

    private static Dictionary<int, int> GetRankCountMap(List<Card> cards)
    {
        var map = new Dictionary<int, int>();
        foreach (var c in cards) { map.TryGetValue(c.Rank, out int cnt); map[c.Rank] = cnt + 1; }
        return map;
    }

    private static bool TryFindTriple(Dictionary<int, int> map, out int rank)
    {
        rank = map.FirstOrDefault(kv => kv.Value >= 3).Key;
        return rank != 0;
    }

    private static bool TryFindQuad(Dictionary<int, int> map, out int rank)
    {
        rank = map.FirstOrDefault(kv => kv.Value >= 4).Key;
        return rank != 0;
    }

    private static bool IsConsecutiveStraight(List<Card> cards)
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

    private static int FindLongestConsecutive(List<int> sortedRanks)
    {
        int best = 1, cur = 1;
        for (int i = 1; i < sortedRanks.Count; i++)
        {
            if (sortedRanks[i] == sortedRanks[i - 1] + 1) { cur++; if (cur > best) best = cur; }
            else cur = 1;
        }
        return best;
    }
}
