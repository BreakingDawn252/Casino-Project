using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BlackjackLogic : UdonSharpBehaviour
{
    // カードID (0-51) からランク (0-12) を取得する
    // 0=A, 1=2, ..., 9=10, 10=J, 11=Q, 12=K
    public int GetRank(int cardId)
    {
        return cardId % 13;
    }

    // BJにおけるカードの点数を返す (Aは一旦11として計算)
    public int GetCardValue(int cardId)
    {
        int rank = GetRank(cardId);
        if (rank == 0) return 11; // A
        if (rank >= 9) return 10; // 10, J, Q, K
        return rank + 1;         // 2-9
    }

    // 手札配列から合計値を計算する (Aの最適化込み)
    public int CalculateTotal(int[] hand, int cardCount)
    {
        int total = 0;
        int aceCount = 0;

        for (int i = 0; i < cardCount; i++)
        {
            int val = GetCardValue(hand[i]);
            total += val;
            if (GetRank(hand[i]) == 0) aceCount++;
        }

        // 21を超えている場合、Aを11から1に変換していく
        while (total > 21 && aceCount > 0)
        {
            total -= 10;
            aceCount--;
        }

        return total;
    }

    // バースト判定
    public bool IsBust(int total)
    {
        return total > 21;
    }

    // ブラックジャック判定 (最初の2枚で21点)
    public bool IsBlackjack(int[] hand, int cardCount)
    {
        if (cardCount != 2) return false;
        return CalculateTotal(hand, cardCount) == 21;
    }
}