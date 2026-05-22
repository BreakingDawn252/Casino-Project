/* * Canvas Name: ChipVisualizer
 * Version: 11
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChipVisualizer : UdonSharpBehaviour
{
    [Header("System")]
    public BlackjackManager manager;

    [Header("Prefabs & Positions")]
    public GameObject chipPrefab;
    [Tooltip("ディーラーのバンクトレイ")]
    public Transform dealerBankPosition;
    
    [Tooltip("手元のベット操作エリア (7席分)")]
    public Transform[] seatBasePositions;
    [Tooltip("テーブルのベットサークル (7席分)")]
    public Transform[] seatCirclePositions;
    [Tooltip("所持金(Money)の山を置く場所 (7席分)")]
    public Transform[] seatMoneyPositions;

    [Header("Chip Settings")]
    public float chipThickness = 0.003f;
    public float twoUpOffset = 0.05f;
    [Tooltip("通常のチップ移動速度")]
    public float moveDuration = 0.5f;

    [Header("Payout Animation Settings")]
    [Tooltip("勝敗決定後、配当が飛び始めるまでの待機時間（秒）")]
    public float payoutDelay = 1.0f; 
    [Tooltip("配当チップが飛ぶ時間。少し長め(0.8等)にするとカジノ感が出ます")]
    public float payoutMoveDuration = 0.8f;

    [Header("Colors (Element 0=$1, 1=$5 ... 7=$25k)")]
    public Color[] chipColors = new Color[8];
    private int[] _chipValues = { 1, 5, 25, 100, 500, 1000, 5000, 25000 };

    private ChipObject[] _chipPool;
    private int _poolIndex = 0;
    private const int POOL_SIZE = 1500; 

    private ChipObject[] _seatBetChips = new ChipObject[700]; 
    private int[] _seatBetChipCounts = new int[7];
    
    private ChipObject[] _seatPayoutChips = new ChipObject[700];
    private int[] _seatPayoutChipCounts = new int[7];

    private ChipObject[] _seatMoneyChips = new ChipObject[700];
    private int[] _seatMoneyChipCounts = new int[7];

    private float[] _lastBetAmounts = new float[7];
    private float[] _lastMoneyAmounts = new float[7];
    private bool[] _lastSeatReadies = new bool[7];
    
    private int[] _lastOwnerIds = new int[] { -1, -1, -1, -1, -1, -1, -1 };
    private bool[] _lastSeatConfirmed = new bool[7];
    private bool[] _seatIsLoss = new bool[7];

    private int[] _chipStates = new int[7]; 
    private int _lastGameState = -1;
    
    private float _state5Timer = 0f;
    private bool _initialized = false;

    void Start()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        if (chipPrefab == null) return;

        _chipPool = new ChipObject[POOL_SIZE];
        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject obj = Instantiate(chipPrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            _chipPool[i] = obj.GetComponent<ChipObject>();
        }
        _initialized = true;
    }

    private ChipObject GetChipFromPool()
    {
        ChipObject chip = _chipPool[_poolIndex];
        _poolIndex++;
        if (_poolIndex >= POOL_SIZE)
        {
            _poolIndex = 0;
        }
        return chip;
    }

    void Update()
    {
        if (!_initialized || manager == null) return;

        int state = manager.GetGameState();

        if (state != _lastGameState)
        {
            if (state == 5)
            {
                _state5Timer = 0f;
            }
        }

        if (state == 5)
        {
            _state5Timer += Time.deltaTime;
        }

        for (int i = 0; i < 7; i++)
        {
            int ownerId = manager.GetSeatOwnerId(i);
            
            if (_lastOwnerIds[i] != -1 && ownerId == -1)
            {
                ClearChipsToPlayer(i);
                ClearMoneyStack(i);
                _chipStates[i] = 0;
            }

            float targetMoney = 0f;
            if (ownerId != -1)
            {
                if (Networking.LocalPlayer != null && ownerId == Networking.LocalPlayer.playerId)
                {
                    targetMoney = manager.udonChips != null ? manager.udonChips.money : 0f;
                }
                else
                {
                    targetMoney = Mathf.Max(0, 10000f - manager.GetSeatBet(i));
                }
            }

            if (Mathf.Abs(targetMoney - _lastMoneyAmounts[i]) > 0.1f)
            {
                RebuildMoneyStack(i, targetMoney);
                _lastMoneyAmounts[i] = targetMoney;
            }

            float currentBet = manager.GetSeatBet(i);
            bool currentReady = manager.GetSeatReady(i);
            bool currentConfirmed = manager.GetSeatResultConfirmed(i);

            // ダブルダウン等によるプレイ中のベット額変更を検知
            if (Mathf.Abs(currentBet - _lastBetAmounts[i]) > 0.1f)
            {
                if (_chipStates[i] == 1)
                {
                    RebuildBetStackAtCircle(i, currentBet);
                }
                else if (state <= 1)
                {
                    RebuildBetStack(i, currentBet);
                    _lastSeatReadies[i] = currentReady; 
                }
                _lastBetAmounts[i] = currentBet;
            }

            if (state <= 1)
            {
                if (_lastGameState == 5 && state == 0 && _chipStates[i] == 2)
                {
                    ClearChipsToPlayer(i);
                    _chipStates[i] = 0;
                }

                if (currentReady && !_lastSeatReadies[i])
                {
                    MoveBetsToCircle(i);
                    _chipStates[i] = 1;
                }
                else if (!currentReady && _lastSeatReadies[i])
                {
                    MoveBetsToBase(i);
                    _chipStates[i] = 0;
                }
                _lastSeatReadies[i] = currentReady;
            }
            else if (state >= 2 && state <= 4)
            {
                if (_chipStates[i] == 0 && currentBet > 0)
                {
                    MoveBetsToCircle(i);
                    _chipStates[i] = 1;
                }
            }
            else if (state == 5)
            {
                if (_chipStates[i] == 1 && _state5Timer >= payoutDelay)
                {
                    ResolvePayout(i);
                    _chipStates[i] = 2;
                }
                
                if (currentConfirmed && !_lastSeatConfirmed[i] && _chipStates[i] == 2)
                {
                    ClearChipsToPlayer(i);
                    _chipStates[i] = 0;
                }
            }

            _lastOwnerIds[i] = ownerId;
            _lastSeatConfirmed[i] = currentConfirmed;
        }
        
        _lastGameState = state;
    }

    private void RebuildMoneyStack(int seat, float amount)
    {
        int startIdx = seat * 100;
        int count = _seatMoneyChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            if (_seatMoneyChips[startIdx + j] != null)
            {
                _seatMoneyChips[startIdx + j].gameObject.SetActive(false);
            }
        }
        _seatMoneyChipCounts[seat] = 0;

        if (amount <= 0) return;

        Transform moneyPos = (seatMoneyPositions != null && seatMoneyPositions.Length > seat) ? seatMoneyPositions[seat] : null;
        if (moneyPos == null) return;

        int amt = (int)amount;
        int newCount = 0;

        for (int v = 7; v >= 0; v--)
        {
            while (amt >= _chipValues[v] && newCount < 100)
            {
                amt -= _chipValues[v];
                
                ChipObject chip = GetChipFromPool();
                chip.SetColor(chipColors[v]);

                Vector3 targetPos = moneyPos.position + (moneyPos.up * (newCount * chipThickness));
                chip.PlaceAt(targetPos, moneyPos.rotation);

                _seatMoneyChips[startIdx + newCount] = chip;
                newCount++;
            }
        }
        _seatMoneyChipCounts[seat] = newCount;
    }

    private void ClearMoneyStack(int seat)
    {
        int startIdx = seat * 100;
        int count = _seatMoneyChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            if (_seatMoneyChips[startIdx + j] != null)
            {
                _seatMoneyChips[startIdx + j].gameObject.SetActive(false);
            }
        }
        _seatMoneyChipCounts[seat] = 0;
        _lastMoneyAmounts[seat] = 0;
    }

    private void RebuildBetStack(int seat, float amount)
    {
        int startIdx = seat * 100;
        int count = _seatBetChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            if (_seatBetChips[startIdx + j] != null)
            {
                _seatBetChips[startIdx + j].gameObject.SetActive(false);
            }
        }
        _seatBetChipCounts[seat] = 0;

        if (amount <= 0) return;

        Transform basePos = seatBasePositions[seat];
        if (basePos == null) return;

        int amt = (int)amount;
        int newCount = 0;

        for (int v = 7; v >= 0; v--)
        {
            while (amt >= _chipValues[v] && newCount < 100)
            {
                amt -= _chipValues[v];
                
                ChipObject chip = GetChipFromPool();
                chip.SetColor(chipColors[v]);

                Vector3 targetPos = basePos.position + (basePos.up * (newCount * chipThickness));
                chip.PlaceAt(targetPos, basePos.rotation);

                _seatBetChips[startIdx + newCount] = chip;
                newCount++;
            }
        }
        _seatBetChipCounts[seat] = newCount;
        _chipStates[seat] = 0; 
    }

    private void RebuildBetStackAtCircle(int seat, float amount)
    {
        int startIdx = seat * 100;
        int count = _seatBetChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            if (_seatBetChips[startIdx + j] != null)
            {
                _seatBetChips[startIdx + j].gameObject.SetActive(false);
            }
        }
        _seatBetChipCounts[seat] = 0;

        if (amount <= 0) return;

        Transform circlePos = seatCirclePositions[seat];
        if (circlePos == null) return;

        int amt = (int)amount;
        int newCount = 0;

        for (int v = 7; v >= 0; v--)
        {
            while (amt >= _chipValues[v] && newCount < 100)
            {
                amt -= _chipValues[v];
                
                ChipObject chip = GetChipFromPool();
                chip.SetColor(chipColors[v]);

                Vector3 targetPos = circlePos.position + (circlePos.up * (newCount * chipThickness));
                chip.PlaceAt(targetPos, circlePos.rotation);

                _seatBetChips[startIdx + newCount] = chip;
                newCount++;
            }
        }
        _seatBetChipCounts[seat] = newCount;
    }

    private void MoveBetsToCircle(int seat)
    {
        Transform circlePos = seatCirclePositions[seat];
        if (circlePos == null) return;

        int startIdx = seat * 100;
        int count = _seatBetChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            ChipObject chip = _seatBetChips[startIdx + j];
            if (chip != null)
            {
                Vector3 targetPos = circlePos.position + (circlePos.up * (j * chipThickness));
                chip.MoveTo(targetPos, circlePos.rotation, moveDuration, false);
            }
        }
    }

    private void MoveBetsToBase(int seat)
    {
        Transform basePos = seatBasePositions[seat];
        if (basePos == null) return;

        int startIdx = seat * 100;
        int count = _seatBetChipCounts[seat];
        
        for (int j = 0; j < count; j++)
        {
            ChipObject chip = _seatBetChips[startIdx + j];
            if (chip != null)
            {
                Vector3 targetPos = basePos.position + (basePos.up * (j * chipThickness));
                chip.MoveTo(targetPos, basePos.rotation, moveDuration, false);
            }
        }
    }

    private void ResolvePayout(int seat)
    {
        float bet = manager.GetSeatBet(seat);
        float payout = manager.GetSeatPayout(seat);
        
        if (bet <= 0) return;

        int startIdx = seat * 100;
        Transform circlePos = seatCirclePositions[seat];
        
        if (circlePos == null || dealerBankPosition == null) return;

        _seatIsLoss[seat] = (payout == 0);

        if (payout > bet)
        {
            float profit = payout - bet;
            int amt = (int)profit;
            int payoutCount = 0;

            for (int v = 7; v >= 0; v--)
            {
                while (amt >= _chipValues[v] && payoutCount < 100)
                {
                    amt -= _chipValues[v];
                    
                    ChipObject chip = GetChipFromPool();
                    chip.PlaceAt(dealerBankPosition.position, dealerBankPosition.rotation);
                    chip.SetColor(chipColors[v]);

                    Vector3 targetPos = circlePos.position + (circlePos.right * twoUpOffset) + (circlePos.up * (payoutCount * chipThickness));
                    chip.MoveTo(targetPos, circlePos.rotation, payoutMoveDuration, false);

                    _seatPayoutChips[startIdx + payoutCount] = chip;
                    payoutCount++;
                }
            }
            _seatPayoutChipCounts[seat] = payoutCount;
        }
    }

    private void ClearChipsToPlayer(int seat)
    {
        Transform targetPos = (seatMoneyPositions != null && seatMoneyPositions.Length > seat && seatMoneyPositions[seat] != null) 
            ? seatMoneyPositions[seat] 
            : seatBasePositions[seat];

        if (targetPos == null) return;

        Transform betTargetPos = _seatIsLoss[seat] ? dealerBankPosition : targetPos;

        int startIdx = seat * 100;

        int bCount = _seatBetChipCounts[seat];
        for (int j = 0; j < bCount; j++)
        {
            ChipObject chip = _seatBetChips[startIdx + j];
            if (chip != null)
            {
                chip.MoveTo(betTargetPos.position, betTargetPos.rotation, moveDuration, true);
            }
        }
        _seatBetChipCounts[seat] = 0;

        int pCount = _seatPayoutChipCounts[seat];
        for (int j = 0; j < pCount; j++)
        {
            ChipObject chip = _seatPayoutChips[startIdx + j];
            if (chip != null)
            {
                chip.MoveTo(targetPos.position, targetPos.rotation, moveDuration, true);
            }
        }
        _seatPayoutChipCounts[seat] = 0;

        _seatIsLoss[seat] = false;
    }
}