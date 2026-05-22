using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CasinoSeatManager : UdonSharpBehaviour
{
    [Header("Settings")]
    public int maxSeats = 5; // 最大同時プレイ人数
    
    // --- 同期変数 ---
    // 席に座っているプレイヤーのID（VRCPlayerApi.playerId）を格納
    // -1 は空席を意味する
    [UdonSynced] private int[] _seatedPlayerIds = { -1, -1, -1, -1, -1 };

    // 各席の座標（カードを配る位置の基準など）
    public Transform[] seatPositions;

    public void CasinoInteract()
    {
        // 空いている席を探して座る
        JoinGame();
    }

    public void JoinGame()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        
        // 既にどこかの席に座っていないかチェック
        if (IsPlayerSeated(localPlayer.playerId)) return;

        // オーナーシップを取得して同期変数を書き換える
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(localPlayer, gameObject);

        for (int i = 0; i < _seatedPlayerIds.Length; i++)
        {
            if (_seatedPlayerIds[i] == -1)
            {
                _seatedPlayerIds[i] = localPlayer.playerId;
                RequestSerialization();
                Debug.Log($"[Casino] Player {localPlayer.displayName} joined at seat {i}");
                break;
            }
        }
    }

    public void LeaveGame()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(localPlayer, gameObject);

        for (int i = 0; i < _seatedPlayerIds.Length; i++)
        {
            if (_seatedPlayerIds[i] == localPlayer.playerId)
            {
                _seatedPlayerIds[i] = -1;
                RequestSerialization();
                break;
            }
        }
    }

    public bool IsPlayerSeated(int playerId)
    {
        foreach (int id in _seatedPlayerIds)
        {
            if (id == playerId) return true;
        }
        return false;
    }

    public int GetSeatIndex(int playerId)
    {
        for (int i = 0; i < _seatedPlayerIds.Length; i++)
        {
            if (_seatedPlayerIds[i] == playerId) return i;
        }
        return -1;
    }
}