using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UCS;

public class CasinoEconomyManager : UdonSharpBehaviour
{
    [Header("UdonChips Reference")]
    public UdonChips udonChips;

    // プレイヤーがチップを賭ける
    // 戻り値: ベット成功なら true, 残高不足なら false
    public bool PlayerBet(float amount)
    {
        if (udonChips == null) return false;

        if (udonChips.money >= amount)
        {
            // 直接 money を減らす。
            // これにより UdonChips.cs の Update 内で変更が検知され、
            // UI更新とセーブが自動的に行われます。
            udonChips.money -= amount;
            return true;
        }
        
        Debug.Log("[Casino] Insufficient funds.");
        return false;
    }

    // 配当を支払う
    // multiplier: 2.0 (通常勝利), 2.5 (BJ), 1.0 (プッシュ)
    public void Payout(float betAmount, float multiplier)
    {
        if (udonChips == null) return;

        float payoutAmount = betAmount * multiplier;
        
        // 0倍（負け）の場合は何もしない
        if (payoutAmount > 0)
        {
            udonChips.money += payoutAmount;
            Debug.Log($"[Casino] Payout processed: {payoutAmount}");
        }
    }
}