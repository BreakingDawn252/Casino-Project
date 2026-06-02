/* * Canvas Name: BlackjackManager
 * Version: 42
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UCS;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BlackjackManager : UdonSharpBehaviour
{
    [Header("External Systems")]
    public CasinoDeckSystem deckSystem;
    public BlackjackLogic logic;
    public UdonChips udonChips;

    [Header("Config")]
    public int maxSeats = 7;
    public int cutCardThreshold = 75;

    [Header("Auto Dealer Settings")]
    [UdonSynced] public bool isAutoMode = false;
    [UdonSynced] public float autoClearDelay = 10f;
    private float _autoTimer = 0f; 

    private const int STATE_WAITING = 0;
    private const int STATE_BETTING = 1;
    private const int STATE_DEALING = 2;
    private const int STATE_PLAYER_TURN = 3;
    private const int STATE_DEALER_TURN = 4;
    private const int STATE_JUDGE = 5;

    [UdonSynced] private int _currentState = STATE_WAITING;
    [UdonSynced] private int _activeSeatIndex = -1; 

    [UdonSynced] private int[] _seatOwnerIds = new int[] { -1, -1, -1, -1, -1, -1, -1 }; 
    [UdonSynced] private float[] _seatBets = new float[7]; 
    [UdonSynced] private float[] _seatPayouts = new float[7]; 
    [UdonSynced] private bool[] _seatReadies = new bool[7];
    [UdonSynced] private float[] _lastInitialBets = new float[7];
    [UdonSynced] private bool[] _seatResultConfirmed = new bool[7];
    [UdonSynced] private bool[] _hasGameParticipation = new bool[7]; 

    // --- Split用変数群 ---
    [UdonSynced] private int[] _allPlayerHandsSp = new int[70]; 
    [UdonSynced] private int[] _playerHandCountsSp = new int[7]; 
    [UdonSynced] private float[] _seatBetsSp = new float[7]; 
    [UdonSynced] private float[] _seatPayoutsSp = new float[7]; 
    [UdonSynced] private bool _isSplitTurn = false;
    [UdonSynced] private bool[] _isSplitAces = new bool[7];

    [UdonSynced] private int[] _allPlayerHands = new int[70]; 
    [UdonSynced] private int[] _playerHandCounts = new int[7]; 
    
    [UdonSynced] private int[] _dealerHand = new int[10];
    [UdonSynced] private int _dealerHandCount = 0;
    
    [UdonSynced] private bool _isCutCardDrawn = false;

    private bool _payoutReceived = false;

    public override void OnDeserialization()
    {
        if (_currentState == STATE_JUDGE)
        {
            if (_autoTimer <= 0) _autoTimer = autoClearDelay; 
            ProcessLocalPayout();
        }
        else if (_currentState == STATE_WAITING || _currentState == STATE_BETTING)
        {
            _payoutReceived = false;
            _autoTimer = 0;
        }
    }

    void Update()
    {
        if (isAutoMode && _currentState == STATE_JUDGE)
        {
            _autoTimer -= Time.deltaTime;
            if (Networking.IsOwner(gameObject) && _autoTimer <= 0)
            {
                ClearGame();
            }
        }
    }

    public float GetAutoTimer() { return _autoTimer; }
    public void ToggleAutoMode()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        isAutoMode = !isAutoMode;
        if (isAutoMode && _currentState == STATE_JUDGE) _autoTimer = autoClearDelay;
        RequestSerialization();
    }

    public int GetGameState() { return _currentState; }
    public int GetCurrentTurnSeat() { return _activeSeatIndex; }
    public float GetSeatBet(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatBets[seatIndex] : 0f; }
    public int GetSeatOwnerId(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatOwnerIds[seatIndex] : -1; }
    public float GetSeatPayout(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatPayouts[seatIndex] : 0f; }
    public bool GetSeatReady(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatReadies[seatIndex] : false; }
    public bool GetSeatResultConfirmed(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatResultConfirmed[seatIndex] : false; }
    public bool HasGameParticipation(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _hasGameParticipation[seatIndex] : false; }
    public bool IsCutCardDrawn() { return _isCutCardDrawn; }

    // --- Split用ゲッター ---
    public bool IsSplitTurn() { return _isSplitTurn; }
    public float GetSeatBetSp(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatBetsSp[seatIndex] : 0f; }
    public float GetSeatPayoutSp(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _seatPayoutsSp[seatIndex] : 0f; }
    public int GetPlayerHandCountSp(int seatIndex) { return (seatIndex >= 0 && seatIndex < maxSeats) ? _playerHandCountsSp[seatIndex] : 0; }

    public int[] GetPlayerHand(int seatIndex)
    {
        int[] hand = new int[10];
        int startIdx = seatIndex * 10;
        for (int i = 0; i < 10; i++) if (startIdx + i < _allPlayerHands.Length) hand[i] = _allPlayerHands[startIdx + i];
        return hand;
    }

    public int GetPlayerHandCount(int seatIndex) { return _playerHandCounts[seatIndex]; }
    
    public int[] GetPlayerHandSp(int seatIndex)
    {
        int[] hand = new int[10];
        int startIdx = seatIndex * 10;
        for (int i = 0; i < 10; i++) if (startIdx + i < _allPlayerHandsSp.Length) hand[i] = _allPlayerHandsSp[startIdx + i];
        return hand;
    }

    public int[] GetDealerHand()
    {
        int[] hand = new int[10];
        for (int i = 0; i < 10; i++) hand[i] = _dealerHand[i];
        return hand;
    }
    public int GetDealerHandCount() { return _dealerHandCount; }

    private bool CheckAllReady()
    {
        bool anyPlayer = false;
        for (int i = 0; i < maxSeats; i++)
        {
            if (_seatOwnerIds[i] != -1)
            {
                anyPlayer = true;
                if (!_seatReadies[i] || _seatBets[i] <= 0) return false;
            }
        }
        return anyPlayer;
    }

    public void OnPlayerSit(int seatIndex, VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _seatOwnerIds[seatIndex] = player.playerId;
        _seatReadies[seatIndex] = false;
        _seatResultConfirmed[seatIndex] = false;
        _hasGameParticipation[seatIndex] = false; 
        RequestSerialization();
    }

    public void OnPlayerStandUp(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _seatOwnerIds[seatIndex] = -1;
        _seatBets[seatIndex] = 0;
        _seatPayouts[seatIndex] = 0;
        _playerHandCounts[seatIndex] = 0;
        _seatReadies[seatIndex] = false;
        _seatResultConfirmed[seatIndex] = false;
        _hasGameParticipation[seatIndex] = false;
        
        _seatBetsSp[seatIndex] = 0;
        _seatPayoutsSp[seatIndex] = 0;
        _playerHandCountsSp[seatIndex] = 0;
        _isSplitAces[seatIndex] = false;

        bool anyoneLeft = false;
        for (int i = 0; i < maxSeats; i++) if (_seatOwnerIds[i] != -1) { anyoneLeft = true; break; }

        if (!anyoneLeft && deckSystem != null)
        {
            deckSystem.ShuffleDeck();
            _isCutCardDrawn = false;
            ClearGame();
        }
        else if (isAutoMode && CheckAllReady()) StartDealing();
        else RequestSerialization();
    }

    public void RequestUpdateBet(int seatIndex, int changeAmount)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState != STATE_WAITING && _currentState != STATE_BETTING) return;
        if (udonChips == null) return;

        if (changeAmount > 0 && udonChips.money >= changeAmount)
        {
            udonChips.money -= changeAmount;
            _seatBets[seatIndex] += changeAmount;
        }
        else if (changeAmount < 0 && _seatBets[seatIndex] >= Mathf.Abs(changeAmount))
        {
            udonChips.money += Mathf.Abs(changeAmount);
            _seatBets[seatIndex] -= Mathf.Abs(changeAmount);
        }
        
        _seatReadies[seatIndex] = false;
        _currentState = STATE_BETTING;
        RequestSerialization();
    }

    public void RequestRebet(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState != STATE_WAITING && _currentState != STATE_BETTING) return;
        if (udonChips == null) return;

        float lastBet = _lastInitialBets[seatIndex];
        if (lastBet > 0 && udonChips.money >= lastBet)
        {
            udonChips.money += _seatBets[seatIndex];
            udonChips.money -= lastBet;
            _seatBets[seatIndex] = lastBet;
            _seatReadies[seatIndex] = false;
            _currentState = STATE_BETTING;
            RequestSerialization();
        }
    }

    public void RequestReady(int seatIndex, bool isReady)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState != STATE_WAITING && _currentState != STATE_BETTING) return;
        
        if (seatIndex >= 0 && seatIndex < maxSeats)
        {
            if (isReady && _seatBets[seatIndex] <= 0) return;
            _seatReadies[seatIndex] = isReady;

            if (isAutoMode && CheckAllReady()) StartDealing();
            else RequestSerialization();
        }
    }

    public void RequestConfirmResult(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _seatResultConfirmed[seatIndex] = true;
        RequestSerialization();
    }

    // ★新規: スプリット要求の処理
    public void RequestSplit(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState != STATE_PLAYER_TURN || _activeSeatIndex != seatIndex) return;
        if (_isSplitTurn) return; // 既にSpターンの場合は不可
        if (_seatBetsSp[seatIndex] > 0) return; // 既にスプリット済みの場合は不可
        if (_playerHandCounts[seatIndex] != 2) return; // 手札が2枚の時のみ
        
        float betAmount = _seatBets[seatIndex];
        if (udonChips == null || udonChips.money < betAmount) return;
        
        // 追加ベット支払い
        udonChips.money -= betAmount;
        _seatBetsSp[seatIndex] = betAmount;
        
        // 2枚目のカードをSpハンドの1枚目へ移動
        int card1 = _allPlayerHands[seatIndex * 10];
        int card2 = _allPlayerHands[seatIndex * 10 + 1];
        
        _allPlayerHandsSp[seatIndex * 10] = card2;
        _allPlayerHands[seatIndex * 10 + 1] = 0; 
        
        _playerHandCounts[seatIndex] = 1;
        _playerHandCountsSp[seatIndex] = 1;
        
        // Aのスプリットか判定 (1枚の合計が11ならAce)
        int val = logic.CalculateTotal(new int[] { card1 }, 1);
        bool isAce = (val == 11);
        _isSplitAces[seatIndex] = isAce;
        
        // 両方に1枚ずつ配る
        DrawCardForSeat(seatIndex);
        DrawCardForSpSeat(seatIndex);
        
        if (isAce)
        {
            // Aのスプリットなら両方強制スタンドになるため、ターンを強制終了して次へ
            _isSplitTurn = true; 
            MoveToNextTurn();
        }
        else
        {
            RequestSerialization();
        }
    }

    public void RequestHit(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState == STATE_PLAYER_TURN && _activeSeatIndex == seatIndex)
        {
            if (!_isSplitTurn)
            {
                DrawCardForSeat(seatIndex);
                int total = logic.CalculateTotal(GetPlayerHand(seatIndex), _playerHandCounts[seatIndex]);
                if (logic.IsBust(total)) MoveToNextTurn();
            }
            else
            {
                DrawCardForSpSeat(seatIndex);
                int totalSp = logic.CalculateTotal(GetPlayerHandSp(seatIndex), _playerHandCountsSp[seatIndex]);
                if (logic.IsBust(totalSp)) MoveToNextTurn();
            }
            RequestSerialization();
        }
    }

    public void RequestStand(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState == STATE_PLAYER_TURN && _activeSeatIndex == seatIndex)
        {
            MoveToNextTurn();
            RequestSerialization();
        }
    }

    public void RequestDouble(int seatIndex)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState == STATE_PLAYER_TURN && _activeSeatIndex == seatIndex && udonChips != null)
        {
            if (!_isSplitTurn)
            {
                if (udonChips.money >= _seatBets[seatIndex])
                {
                    udonChips.money -= _seatBets[seatIndex];
                    _seatBets[seatIndex] *= 2;
                    DrawCardForSeat(seatIndex);
                    MoveToNextTurn();
                    RequestSerialization();
                }
            }
            else
            {
                if (udonChips.money >= _seatBetsSp[seatIndex])
                {
                    udonChips.money -= _seatBetsSp[seatIndex];
                    _seatBetsSp[seatIndex] *= 2;
                    DrawCardForSpSeat(seatIndex);
                    MoveToNextTurn();
                    RequestSerialization();
                }
            }
        }
    }

    public void StartDealing()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (_currentState != STATE_BETTING && _currentState != STATE_WAITING) return;

        for (int i = 0; i < maxSeats; i++)
        {
            if (_seatOwnerIds[i] != -1 && _seatBets[i] > 0 && _seatReadies[i])
            {
                _lastInitialBets[i] = _seatBets[i];
                _hasGameParticipation[i] = true;
            }
        }

        if (deckSystem.GetRemainingCardsCount() < 20)
        {
            deckSystem.ShuffleDeck();
            _isCutCardDrawn = false;
        }

        for (int i = 0; i < maxSeats; i++) 
        {
            _playerHandCounts[i] = 0;
            _playerHandCountsSp[i] = 0;
            _seatBetsSp[i] = 0;
            _seatPayoutsSp[i] = 0;
            _isSplitAces[i] = false;
        }
        _dealerHandCount = 0;
        _isSplitTurn = false;

        for (int i = 0; i < maxSeats; i++)
        {
            if (_hasGameParticipation[i])
            {
                DrawCardForSeat(i);
                DrawCardForSeat(i);
            }
        }

        _dealerHand[_dealerHandCount++] = DrawFromDeck();
        _dealerHand[_dealerHandCount++] = DrawFromDeck();
        
        _currentState = STATE_PLAYER_TURN;
        _activeSeatIndex = -1;
        MoveToNextTurn();
    }

    public void ClearGame()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _currentState = STATE_WAITING;
        for (int i = 0; i < maxSeats; i++)
        {
            _seatBets[i] = 0;
            _seatPayouts[i] = 0;
            _playerHandCounts[i] = 0;
            _seatReadies[i] = false;
            _seatResultConfirmed[i] = false;
            _hasGameParticipation[i] = false;
            
            _seatBetsSp[i] = 0;
            _seatPayoutsSp[i] = 0;
            _playerHandCountsSp[i] = 0;
            _isSplitAces[i] = false;
        }
        _dealerHandCount = 0;
        _isSplitTurn = false;
        _payoutReceived = false;
        _autoTimer = 0;
        RequestSerialization();
    }

    public void ForceResetTable()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _currentState = STATE_WAITING;
        _activeSeatIndex = -1;
        _dealerHandCount = 0;
        _isCutCardDrawn = false;
        _payoutReceived = false;
        _autoTimer = 0;
        isAutoMode = false;
        for (int i = 0; i < maxSeats; i++)
        {
            _seatOwnerIds[i] = -1;
            _seatBets[i] = 0;
            _seatPayouts[i] = 0;
            _seatReadies[i] = false;
            _playerHandCounts[i] = 0;
            _seatResultConfirmed[i] = false;
            _hasGameParticipation[i] = false;
            
            _seatBetsSp[i] = 0;
            _seatPayoutsSp[i] = 0;
            _playerHandCountsSp[i] = 0;
            _isSplitAces[i] = false;
        }
        for (int j = 0; j < _dealerHand.Length; j++) _dealerHand[j] = 0;
        for (int k = 0; k < _allPlayerHands.Length; k++) _allPlayerHands[k] = 0;
        for (int l = 0; l < _allPlayerHandsSp.Length; l++) _allPlayerHandsSp[l] = 0;
        
        _isSplitTurn = false;
        
        if (deckSystem != null) deckSystem.ShuffleDeck();
        RequestSerialization();
    }

    private int DrawFromDeck()
    {
        int cardId = deckSystem.DrawCard();
        if (deckSystem.GetRemainingCardsCount() <= cutCardThreshold) _isCutCardDrawn = true;
        return cardId;
    }

    private void DrawCardForSeat(int seatIndex)
    {
        int count = _playerHandCounts[seatIndex];
        _allPlayerHands[(seatIndex * 10) + count] = DrawFromDeck();
        _playerHandCounts[seatIndex]++;
    }

    private void DrawCardForSpSeat(int seatIndex)
    {
        int count = _playerHandCountsSp[seatIndex];
        _allPlayerHandsSp[(seatIndex * 10) + count] = DrawFromDeck();
        _playerHandCountsSp[seatIndex]++;
    }

    // ★改修: ターンの進行ロジック
    private void MoveToNextTurn()
    {
        if (_activeSeatIndex != -1)
        {
            // メインハンドが終了し、かつスプリットが存在する場合
            if (!_isSplitTurn && _seatBetsSp[_activeSeatIndex] > 0)
            {
                _isSplitTurn = true;
                RequestSerialization();
                return; // 同じ人のSpハンドのターンへ
            }
        }

        // 次のプレイヤーを検索
        _isSplitTurn = false;
        int next = -1;
        for (int i = _activeSeatIndex + 1; i < maxSeats; i++)
        {
            if (_hasGameParticipation[i] && _playerHandCounts[i] > 0)
            {
                next = i;
                break;
            }
        }

        if (next != -1)
        {
            _activeSeatIndex = next;
            RequestSerialization();
        }
        else
        {
            _activeSeatIndex = -1;
            _currentState = STATE_DEALER_TURN;
            RequestSerialization();
            SendCustomEventDelayedSeconds(nameof(DealerDrawLoop), 0.5f);
        }
    }

    public void DealerDrawLoop()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (logic.CalculateTotal(_dealerHand, _dealerHandCount) >= 17 || _dealerHandCount >= 10)
        {
            JudgeOutcome();
            return;
        }
        _dealerHand[_dealerHandCount++] = DrawFromDeck();
        RequestSerialization();
        SendCustomEventDelayedSeconds(nameof(DealerDrawLoop), 0.5f);
    }

    private void JudgeOutcome()
    {
        int dTotal = logic.CalculateTotal(_dealerHand, _dealerHandCount);
        bool dBJ = logic.IsBlackjack(_dealerHand, _dealerHandCount);
        
        for (int i = 0; i < maxSeats; i++)
        {
            if (_hasGameParticipation[i] && _playerHandCounts[i] > 0)
            {
                bool hasSplit = _seatBetsSp[i] > 0;
                
                // メインハンドの判定
                int pTotal = logic.CalculateTotal(GetPlayerHand(i), _playerHandCounts[i]);
                bool pBJ = logic.IsBlackjack(GetPlayerHand(i), _playerHandCounts[i]) && !hasSplit;
                float bet = _seatBets[i];
                
                if (pTotal > 21) _seatPayouts[i] = 0;
                else if (pBJ && !dBJ) _seatPayouts[i] = bet * 2.5f;
                else if (pBJ && dBJ) _seatPayouts[i] = bet * 1.0f;
                else if (dTotal > 21 || pTotal > dTotal) _seatPayouts[i] = bet * 2.0f;
                else if (pTotal == dTotal) _seatPayouts[i] = bet * 1.0f;
                else _seatPayouts[i] = 0;

                // Spハンドの判定
                if (hasSplit)
                {
                    int spTotal = logic.CalculateTotal(GetPlayerHandSp(i), _playerHandCountsSp[i]);
                    float betSp = _seatBetsSp[i]; // スプリットはBJにならない(21扱い)
                    
                    if (spTotal > 21) _seatPayoutsSp[i] = 0;
                    else if (dTotal > 21 || spTotal > dTotal) _seatPayoutsSp[i] = betSp * 2.0f;
                    else if (spTotal == dTotal) _seatPayoutsSp[i] = betSp * 1.0f;
                    else _seatPayoutsSp[i] = 0;
                }
            }
        }
        
        _currentState = STATE_JUDGE;
        _autoTimer = autoClearDelay; 
        RequestSerialization();
        ProcessLocalPayout();
    }

    private void ProcessLocalPayout()
    {
        if (_payoutReceived || udonChips == null) return;
        _payoutReceived = true;
        for (int i = 0; i < maxSeats; i++)
        {
            if (_seatOwnerIds[i] == Networking.LocalPlayer.playerId)
            {
                float totalPayout = _seatPayouts[i] + _seatPayoutsSp[i];
                if (totalPayout > 0) udonChips.money += totalPayout;
            }
        }
    }
}