/* * Canvas Name: BlackjackTab
 * Version: 40
 */
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;
using System.Diagnostics;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BlackjackTab : UdonSharpBehaviour
{
    [Header("---------------- System ----------------")]
    public BlackjackManager manager;
    public int seatIndex;

    [Header("---------------- UI Panels ----------------")]
    public GameObject panelJoin;
    public GameObject panelBet;
    public GameObject panelWait;
    public GameObject panelAction;
    public GameObject panelResult;

    [Header("---------------- Info Display ----------------")]
    public TextMeshProUGUI statusText; 
    public TextMeshProUGUI moneyText; 
    
    [Header("---------------- Bet Control ----------------")]
    public TextMeshProUGUI betModeText;

    [Header("---------------- Auto Bet ----------------")]
    public TextMeshProUGUI autoBetButtonText;

    [Header("---------------- Hands ----------------")]
    public Transform handContainer;
    public Transform dealerHandContainer;
    public GameObject cardIconPrefab;
    public Transform handContainerSp;
    public CanvasGroup mainHandCanvasGroup;
    public CanvasGroup spHandCanvasGroup;
    public Button splitButton;
    public Button doubleButton;

    [UdonSynced] private int _ownerPlayerId = -1;

    private int _lastHandCount = -1;
    private int _lastDealerHandCount = -1;
    private int _lastGameState = -1;
    private int _lastTurnSeat = -1;
    private float _lastBetAmount = -1f;
    private float _lastMoney = -1f;
    private int _lastOwnerId = -2;
    private bool _lastReadyState = false;
    private bool _lastAutoMode = false;
    private float _lastAutoTimer = -1f;
    
    private bool _localResultConfirmed = false;
    private bool _isSubtractMode = false;
    private float _carriedOverBetAmount = 0f; 

    private bool _pendingAutoBet = false;
    private float _autoBetTimer = 0f;
    private bool _isAutoBetEnabled = false;

    void Start()
    {
        UpdateUI();
        UpdateModeText();
        UpdateAutoBetText();
    }

    public override void OnDeserialization()
    {
        UpdateUI();
    }

    void Update()
    {
        if (manager == null) return;

        if (manager.GetSeatOwnerId(seatIndex) == -1 && _ownerPlayerId != -1)
        {
            if (IsOwner())
            {
                _ownerPlayerId = -1;
                _carriedOverBetAmount = 0;
                _pendingAutoBet = false;
                _isAutoBetEnabled = false;
                UpdateAutoBetText();
                RequestSerialization();
            }
            else
            {
                _ownerPlayerId = -1;
                _carriedOverBetAmount = 0;
                _pendingAutoBet = false;
                _isAutoBetEnabled = false;
                UpdateAutoBetText();
            }
            UpdateUI();
        }

        int state = manager.GetGameState();
        
        bool enteredWaiting = (_lastGameState == 5 && state == 0);
        bool enteredBetting = ((_lastGameState == 5 || _lastGameState == 0) && state == 1);
        bool gameStarted = (_lastGameState <= 1 && state > 1);

        if (gameStarted)
        {
            float b = manager.GetSeatBet(seatIndex);
            if (b > 0) _carriedOverBetAmount = b;
        }

        if (enteredWaiting || enteredBetting)
        {
            if (IsOwner() && _isAutoBetEnabled && _carriedOverBetAmount > 0 && manager.GetSeatBet(seatIndex) == 0)
            {
                _pendingAutoBet = true;
                _autoBetTimer = 0.5f;
            }
        }

        if (_pendingAutoBet)
        {
            _autoBetTimer -= Time.deltaTime;
            if (_autoBetTimer <= 0)
            {
                _pendingAutoBet = false;
                if (IsOwner() && _carriedOverBetAmount > 0 && manager.GetSeatBet(seatIndex) == 0)
                {
                    if (manager.udonChips != null && manager.udonChips.money >= _carriedOverBetAmount)
                    {
                        manager.RequestUpdateBet(seatIndex, (int)_carriedOverBetAmount);
                        manager.RequestReady(seatIndex, true);
                        UpdateUI();
                    }
                    else
                    {
                        _isAutoBetEnabled = false;
                        UpdateAutoBetText();
                    }
                }
            }
        }

        bool needUpdate = false;
        
        if (manager.isAutoMode != _lastAutoMode) needUpdate = true;
        if (Mathf.Abs(manager.GetAutoTimer() - _lastAutoTimer) > 0.5f) needUpdate = true;
        
        if (state != _lastGameState)
        {
            needUpdate = true;
            if (state <= 1) OnClickSetAddMode();
            if (_lastGameState == 5) _localResultConfirmed = false;
        }

        if (manager.GetCurrentTurnSeat() != _lastTurnSeat)
        {
            needUpdate = true;
            if (manager.GetCurrentTurnSeat() == seatIndex) _localResultConfirmed = false;
        }

        int currentHandCount = manager.GetPlayerHandCount(seatIndex);
        int currentDealerHandCount = manager.GetDealerHandCount();
        
        if (currentHandCount != _lastHandCount) needUpdate = true;
        if (currentDealerHandCount != _lastDealerHandCount) needUpdate = true;

        if (manager.GetSeatBet(seatIndex) != _lastBetAmount) needUpdate = true;
        if (manager.udonChips != null && manager.udonChips.money != _lastMoney) needUpdate = true;
        if (manager.GetSeatReady(seatIndex) != _lastReadyState) needUpdate = true;

        if (manager.IsSplitTurn() != (_lastGameState == 3 && mainHandCanvasGroup != null && mainHandCanvasGroup.alpha < 1f)) needUpdate = true;

        if (needUpdate) UpdateUI();
    }

    public void UpdateUI()
    {
        if (manager == null) return;

        _lastGameState = manager.GetGameState();
        _lastTurnSeat = manager.GetCurrentTurnSeat();
        _lastBetAmount = manager.GetSeatBet(seatIndex);
        if (manager.udonChips != null) _lastMoney = manager.udonChips.money;
        _lastReadyState = manager.GetSeatReady(seatIndex);
        _lastAutoMode = manager.isAutoMode;
        _lastAutoTimer = manager.GetAutoTimer();
        
        _lastHandCount = manager.GetPlayerHandCount(seatIndex);
        _lastDealerHandCount = manager.GetDealerHandCount();

        if (dealerHandContainer != null) dealerHandContainer.gameObject.SetActive(_lastDealerHandCount > 0);
        if (handContainer != null) handContainer.gameObject.SetActive(_lastHandCount > 0);

        bool hideHoleCard = (_lastGameState <= 3 && _lastDealerHandCount >= 2);
        RefreshCards(dealerHandContainer, manager.GetDealerHand(), _lastDealerHandCount, hideHoleCard);
        RefreshCards(handContainer, manager.GetPlayerHand(seatIndex), _lastHandCount, false);

        float betSp = manager.GetSeatBetSp(seatIndex);
        bool hasSplit = (betSp > 0);
        int spHandCount = manager.GetPlayerHandCountSp(seatIndex);
        
        if (handContainerSp != null)
        {
            handContainerSp.gameObject.SetActive(spHandCount > 0);
            if (spHandCount > 0)
            {
                RefreshCards(handContainerSp, manager.GetPlayerHandSp(seatIndex), spHandCount, false);
            }
        }

        if (mainHandCanvasGroup != null) mainHandCanvasGroup.alpha = 1f;
        if (spHandCanvasGroup != null) spHandCanvasGroup.alpha = 1f;

        if (hasSplit && _lastGameState == 3 && _lastTurnSeat == seatIndex)
        {
            if (manager.IsSplitTurn())
            {
                if (mainHandCanvasGroup != null) mainHandCanvasGroup.alpha = 0.5f;
            }
            else
            {
                if (spHandCanvasGroup != null) spHandCanvasGroup.alpha = 0.5f;
            }
        }

        UpdateMoneyDisplay();

        if (panelJoin) panelJoin.SetActive(false);
        if (panelBet) panelBet.SetActive(false);
        if (panelWait) panelWait.SetActive(false);
        if (panelAction) panelAction.SetActive(false);
        if (panelResult) panelResult.SetActive(false);

        bool isTaken = (_ownerPlayerId != -1);
        bool isMe = (Networking.LocalPlayer != null && Networking.LocalPlayer.playerId == _ownerPlayerId);

        if (!isTaken)
        {
            if (panelJoin) panelJoin.SetActive(true);
            if (statusText) statusText.text = "Touch to Join";
            return;
        }

        if (!isMe)
        {
            if (panelWait) panelWait.SetActive(true);
            if (statusText) statusText.text = "In Use";
            return;
        }

        bool hasParticipation = manager.HasGameParticipation(seatIndex);
        if (_lastGameState > 1 && !hasParticipation)
        {
            if (panelWait) panelWait.SetActive(true);
            statusText.text = "Please wait for the next game...";
            return;
        }

        string timerText = (_lastAutoMode && _lastGameState == 5) ? $"\n(Next Game: {_lastAutoTimer:F0}s)" : "";

        if (_lastGameState == 3 && _lastTurnSeat == seatIndex)
        {
            if (panelAction) panelAction.SetActive(true);
            statusText.text = "Your Turn";

            if (splitButton != null)
            {
                bool canSplit = false;
                if (!hasSplit && !manager.IsSplitTurn() && _lastHandCount == 2)
                {
                    int[] hand = manager.GetPlayerHand(seatIndex);
                    if (hand[0] > 0 && hand[1] > 0)
                    {
                        int rank1 = (hand[0] - 1) % 13;
                        int rank2 = (hand[1] - 1) % 13;
                        if (rank1 == rank2)
                        {
                            if (manager.udonChips != null && manager.udonChips.money >= _lastBetAmount)
                            {
                                canSplit = true;
                            }
                        }
                    }
                }
                splitButton.interactable = canSplit;
            }

            if (doubleButton != null)
            {
                bool canDouble = false;
                if (manager.udonChips != null)
                {
                    if (!manager.IsSplitTurn())
                    {
                        if (_lastHandCount == 2 && manager.udonChips.money >= _lastBetAmount)
                        {
                            canDouble = true;
                        }
                    }
                    else
                    {
                        if (spHandCount == 2 && manager.udonChips.money >= betSp)
                        {
                            canDouble = true;
                        }
                    }
                }
                doubleButton.interactable = canDouble;
            }
        }
        else if (_lastGameState >= 2 && _lastGameState <= 4)
        {
            if (panelWait) panelWait.SetActive(true);
            statusText.text = "Waiting...";
        }
        else if (_lastGameState == 5)
        {
            if (_localResultConfirmed)
            {
                if (panelWait) panelWait.SetActive(true);
                statusText.text = "Confirmed." + timerText;
            }
            else
            {
                if (panelResult) panelResult.SetActive(true);
                
                float bet = manager.GetSeatBet(seatIndex);
                float payout = manager.GetSeatPayout(seatIndex);
                float payoutSp = manager.GetSeatPayoutSp(seatIndex);
                
                if (bet > 0)
                {
                    float netMain = payout - bet;
                    string mainStr = netMain > 0 ? $"+{netMain:F0}uc" : (netMain < 0 ? $"-{Mathf.Abs(netMain):F0}uc" : "±0uc");

                    if (hasSplit)
                    {
                        float netSp = payoutSp - betSp;
                        string spStr = netSp > 0 ? $"+{netSp:F0}uc" : (netSp < 0 ? $"-{Mathf.Abs(netSp):F0}uc" : "±0uc");
                        
                        statusText.text = $"{mainStr}        {spStr}" + timerText;
                    }
                    else
                    {
                        statusText.text = mainStr + timerText;
                    }
                }
                else 
                {
                    statusText.text = "Game Over" + timerText;
                }
            }
        }
        else
        {
            if (_lastReadyState)
            {
                if (panelWait) panelWait.SetActive(true);
                statusText.text = "Ready! Waiting...";
            }
            else
            {
                if (panelBet) panelBet.SetActive(true);
                statusText.text = "Place Your Bet & Ready";
            }
        }
    }

    public void OnClickJoin()
    {
        if (_ownerPlayerId != -1) return;
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _ownerPlayerId = Networking.LocalPlayer.playerId;
        _localResultConfirmed = false;
        RequestSerialization();
        manager.OnPlayerSit(seatIndex, Networking.LocalPlayer);
        UpdateUI();
    }

    public void OnClickLeave()
    {
        if (!IsOwner()) return;
        if (manager.GetGameState() <= 1 && !manager.GetSeatReady(seatIndex))
        {
            float currentBet = manager.GetSeatBet(seatIndex);
            if (currentBet > 0) manager.RequestUpdateBet(seatIndex, -(int)currentBet);
        }
        _ownerPlayerId = -1;
        _localResultConfirmed = false;
        _carriedOverBetAmount = 0;
        _pendingAutoBet = false;
        _isAutoBetEnabled = false;
        UpdateAutoBetText();
        RequestSerialization();
        manager.OnPlayerStandUp(seatIndex);
        UpdateUI();
    }

    public void OnClickReady()
    {
        if (!IsOwner()) return;
        if (manager.GetSeatBet(seatIndex) > 0)
        {
            manager.RequestReady(seatIndex, true);
            UpdateUI();
        }
    }

    public void OnClickBet1() { TryChangeBet(1); }
    public void OnClickBet5() { TryChangeBet(5); }
    public void OnClickBet25() { TryChangeBet(25); }
    public void OnClickBet100() { TryChangeBet(100); }
    public void OnClickBet500() { TryChangeBet(500); }
    public void OnClickBet1k() { TryChangeBet(1000); }
    public void OnClickBet5k() { TryChangeBet(5000); }
    public void OnClickBet25k() { TryChangeBet(25000); }

    public void OnClickBetAll()
    {
        if (!IsOwner() || manager == null || manager.udonChips == null) return;
        if (_isSubtractMode)
        {
            OnClickResetBet();
            return;
        }
        int allMoney = (int)manager.udonChips.money;
        if (allMoney > 0) TryChangeBet(allMoney);
    }

    public void OnClickResetBet()
    {
        if (!IsOwner()) return;
        float currentBet = manager.GetSeatBet(seatIndex);
        if (currentBet > 0)
        {
            manager.RequestUpdateBet(seatIndex, -(int)currentBet);
            manager.RequestReady(seatIndex, false);
            OnClickSetAddMode();
            UpdateUI();
        }
    }

    public void OnClickSetAddMode()
    {
        _isSubtractMode = false;
        UpdateModeText();
    }

    public void OnClickSetSubMode()
    {
        _isSubtractMode = true;
        UpdateModeText();
    }

    public void OnClickToggleAutoBet()
    {
        if (!IsOwner()) return;
        _isAutoBetEnabled = !_isAutoBetEnabled;
        UpdateAutoBetText();

        if (_isAutoBetEnabled && (manager.GetGameState() == 0 || manager.GetGameState() == 1))
        {
            if (_carriedOverBetAmount > 0 && manager.GetSeatBet(seatIndex) == 0)
            {
                _pendingAutoBet = true;
                _autoBetTimer = 0.2f;
            }
        }
    }

    private void UpdateModeText()
    {
        if (betModeText != null)
        {
            betModeText.text = _isSubtractMode ? "Mode: <color=red>SUB (-)</color>" : "Mode: <color=green>ADD (+)</color>";
        }
    }

    private void UpdateAutoBetText()
    {
        if (autoBetButtonText != null)
        {
            autoBetButtonText.text = _isAutoBetEnabled ? "Auto Bet: <color=green>ON</color>" : "Auto Bet: <color=red>OFF</color>";
        }
    }

    private void TryChangeBet(int amount)
    {
        if (!IsOwner()) return;
        int finalAmount = _isSubtractMode ? -amount : amount;
        manager.RequestUpdateBet(seatIndex, finalAmount);
        manager.RequestReady(seatIndex, false);
        UpdateUI();
    }

    public void OnClickHit() { if (IsOwner()) manager.RequestHit(seatIndex); }
    public void OnClickStand() { if (IsOwner()) manager.RequestStand(seatIndex); }
    public void OnClickDouble() { if (IsOwner()) manager.RequestDouble(seatIndex); } 
    public void OnClickSplit() { if (IsOwner()) manager.RequestSplit(seatIndex); } 
    
    public void OnClickRebet()
    {
        if (IsOwner())
        {
            _localResultConfirmed = true;
            manager.RequestConfirmResult(seatIndex);
            OnClickSetAddMode();
            UpdateUI();
        }
    }

    private bool IsOwner()
    {
        return Networking.LocalPlayer != null && Networking.LocalPlayer.playerId == _ownerPlayerId;
    }

    private void UpdateMoneyDisplay()
    {
        if (moneyText != null && manager != null)
        {
            float currentMoney = (manager.udonChips != null) ? manager.udonChips.money : 0f;
            float currentBet = manager.GetSeatBet(seatIndex);
            float currentBetSp = manager.GetSeatBetSp(seatIndex);
            
            if (currentBetSp > 0)
            {
                moneyText.text = $"Money: {currentMoney:F0}uc \nBet: {currentBet:F0}uc + {currentBetSp:F0}uc";
            }
            else
            {
                moneyText.text = $"Money: {currentMoney:F0}uc \nBet: {currentBet:F0}uc";
            }
        }
    }

    private void RefreshCards(Transform container, int[] cards, int count, bool hideHoleCard)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);
        if (cardIconPrefab == null || cards == null) return;
        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(cardIconPrefab);
            icon.transform.SetParent(container, false);
            CasinoCardUI ui = icon.GetComponent<CasinoCardUI>();
            if (ui != null)
            {
                if (hideHoleCard && i == 1) ui.SetCard(-1);
                else ui.SetCard(cards[i]);
            }
        }
    }
}