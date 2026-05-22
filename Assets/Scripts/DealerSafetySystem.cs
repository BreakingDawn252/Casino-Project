using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class DealerSafetySystem : UdonSharpBehaviour
{
    [Header("Settings")]
    public bool isSafetyEnabled = true; // セーフティのオンオフ
    
    [Header("Whitelisted Dealer Names")]
    public string[] allowedDealers; // インスペクタで名前を入力

    public bool IsAllowedToBeDealer(VRCPlayerApi player)
    {
        if (!isSafetyEnabled) return true;
        if (player == null) return false;

        foreach (string name in allowedDealers)
        {
            if (player.displayName == name) return true;
        }
        return false;
    }

    // ディーラー操作パネル等の表示制御に使用
    public void UpdateDealerUI(GameObject dealerPanel)
    {
        bool canOperate = IsAllowedToBeDealer(Networking.LocalPlayer);
        dealerPanel.SetActive(canOperate);
    }
}