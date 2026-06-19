/* * Canvas Name: BlackjackDtab
 * Version: 10
 */
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BlackjackDtab : UdonSharpBehaviour
{
    [Header("---------------- System ----------------")]
    public BlackjackManager manager;

    [Header("---------------- UI Buttons ----------------")]
    public Button dealButton;
    public Button clearButton;
    public Button autoModeButton;
    public Button forceResetButton;

    [Header("---------------- Info Display ----------------")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI tableInfoText;

    private int _lastGameState = -1;
    private bool _lastAutoMode = false;
    private float _lastAutoTimer = -1f;

    void Start()
    {
        UpdateUI();
    }

    void Update()
    {
        if (manager == null) return;

        bool needUpdate = false;
        int state = manager.GetGameState();

        if (state != _lastGameState) needUpdate = true;
        if (manager.isAutoMode != _lastAutoMode) needUpdate = true;
        if (Mathf.Abs(manager.GetAutoTimer() - _lastAutoTimer) > 0.5f) needUpdate = true;

        // 状態の変化、またはベット額の変動などを反映するために定期的に更新をかける
        if (needUpdate) UpdateUI();
    }

    public void UpdateUI()
    {
        if (manager == null) return;

        _lastGameState = manager.GetGameState();
        _lastAutoMode = manager.isAutoMode;
        _lastAutoTimer = manager.GetAutoTimer();

        // ゲームフェーズに応じたボタンの有効・無効化（グレーアウト制御）
        if (dealButton != null) dealButton.interactable = (_lastGameState == 0 || _lastGameState == 1);
        if (clearButton != null) clearButton.interactable = (_lastGameState == 5);

        // ステータステキストの更新
        if (statusText != null)
        {
            string stateStr = "";
            switch (_lastGameState)
            {
                case 0: stateStr = "WAITING"; break;
                case 1: stateStr = "BETTING"; break;
                case 2: stateStr = "DEALING"; break;
                case 3: stateStr = "PLAYER TURN"; break;
                case 4: stateStr = "DEALER TURN"; break;
                case 5: stateStr = "JUDGE"; break;
            }
            string timerText = (_lastAutoMode && _lastGameState == 5) ? $" ({_lastAutoTimer:F0}s)" : "";
            statusText.text = $"STATE: {stateStr}{timerText}\nAUTO: {(_lastAutoMode ? "<color=green>ON</color>" : "<color=red>OFF</color>")}";
        }

        // テーブル全体のベット情報の更新（単位を uc に変更）
        if (tableInfoText != null)
        {
            string info = "<b>--- SEAT STATUS ---</b>\n";
            float totalBet = 0f;

            for (int i = 0; i < manager.maxSeats; i++)
            {
                int ownerId = manager.GetSeatOwnerId(i);
                if (ownerId != -1)
                {
                    float bet = manager.GetSeatBet(i);
                    float betSp = manager.GetSeatBetSp(i);
                    totalBet += (bet + betSp);

                    if (betSp > 0)
                    {
                        info += $"Seat {i + 1}: {bet:F0}uc + {betSp:F0}uc\n";
                    }
                    else
                    {
                        info += $"Seat {i + 1}: {bet:F0}uc\n";
                    }
                }
                else
                {
                    info += $"Seat {i + 1}: <color=gray>Empty</color>\n";
                }
            }

            info += $"\n<b>TOTAL BET: {totalBet:F0}uc</b>";
            tableInfoText.text = info;
        }
    }

    public void OnClickDeal()
    {
        if (manager != null)
        {
            manager.StartDealing();
            UpdateUI();
        }
    }

    public void OnClickClear()
    {
        if (manager != null)
        {
            manager.ClearGame();
            UpdateUI();
        }
    }

    public void OnClickToggleAuto()
    {
        if (manager != null)
        {
            manager.ToggleAutoMode();
            UpdateUI();
        }
    }

    public void OnClickForceReset()
    {
        if (manager != null)
        {
            manager.ForceResetTable();
            UpdateUI();
        }
    }
}