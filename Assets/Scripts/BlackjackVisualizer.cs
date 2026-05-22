using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BlackjackVisualizer : UdonSharpBehaviour
{
    [Header("System References")]
    public BlackjackManager manager;

    [Header("Prefabs")]
    public GameObject cardPrefab;
    
    [Header("Cut Card Settings")]
    public GameObject cutCardPrefab;
    public Transform cutCardPosition;

    [Header("Positions")]
    public Transform shoeTransform;
    public Transform discardTransform;
    public Transform dealerHandPosition;
    public Transform[] playerHandPositions;

    [Header("Animation Settings")]
    public float cardSpacingX = 0.05f;
    public float moveDuration = 0.5f;
    public float stackYOffset = 0.0005f; 

    private CardObject[] _allCardObjects;
    private int _totalCards;
    private int _shoeTopIndex;
    private int _discardTopIndex;

    private CardObject[] _tableCards;
    private int _tableCardCount = 0;

    // ★修正: 7席分に拡張
    private int[] _lastPlayerHandCounts = new int[7];
    private int _lastDealerHandCount = 0;
    private int _lastGameState = -1;
    private int _lastRemainCards = -1;

    private CardObject _dealerHoleCard = null;
    private bool _dealerHoleCardRevealed = false;
    private bool _initialized = false;

    private CardObject _cutCardObject;
    private bool _lastCutCardState = false;

    void Start()
    {
        InitializeCards();
    }

    private void InitializeCards()
    {
        if (manager == null || manager.deckSystem == null || cardPrefab == null) return;

        _totalCards = manager.deckSystem.GetTotalCardsCount();
        _allCardObjects = new CardObject[_totalCards];
        _tableCards = new CardObject[_totalCards];

        Vector3 shoePos = shoeTransform != null ? shoeTransform.position : Vector3.zero;
        Quaternion shoeRot = shoeTransform != null ? shoeTransform.rotation : Quaternion.identity;
        Quaternion faceDownRot = shoeRot * Quaternion.Euler(0, 0, 180);

        for (int i = 0; i < _totalCards; i++)
        {
            Vector3 pos = shoePos + Vector3.up * (i * stackYOffset);
            GameObject obj = Instantiate(cardPrefab, pos, faceDownRot);
            CardObject card = obj.GetComponent<CardObject>();
            card.SetCard(-1, false);
            _allCardObjects[i] = card;
        }

        if (cutCardPrefab != null)
        {
            int threshold = manager.cutCardThreshold;
            Vector3 cutPos = shoePos + Vector3.up * (threshold * stackYOffset);
            GameObject cutObj = Instantiate(cutCardPrefab, cutPos, faceDownRot);
            _cutCardObject = cutObj.GetComponent<CardObject>();
        }

        _shoeTopIndex = _totalCards - 1;
        _discardTopIndex = 0;
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized || manager == null) return;

        int state = manager.GetGameState();
        int remainCards = manager.deckSystem.GetRemainingCardsCount();
        bool isShuffled = false;

        if (_lastRemainCards != -1 && remainCards > _lastRemainCards)
        {
            DoShuffleVisual();
            isShuffled = true;
        }
        _lastRemainCards = remainCards;

        if (!isShuffled && _lastGameState == 5 && state != 5)
        {
            MoveTableCardsToDiscard();
        }
        _lastGameState = state;

        bool isCutCardDrawn = manager.IsCutCardDrawn();
        if (isCutCardDrawn && !_lastCutCardState)
        {
            if (_cutCardObject != null && cutCardPosition != null)
            {
                _cutCardObject.MoveTo(cutCardPosition.position, cutCardPosition.rotation, moveDuration);
            }
        }
        else if (!isCutCardDrawn && _lastCutCardState)
        {
            if (_cutCardObject != null)
            {
                Vector3 shoePos = shoeTransform != null ? shoeTransform.position : Vector3.zero;
                Quaternion faceDownRot = shoeTransform != null ? (shoeTransform.rotation * Quaternion.Euler(0, 0, 180)) : Quaternion.identity;
                Vector3 cutPos = shoePos + Vector3.up * (manager.cutCardThreshold * stackYOffset);
                _cutCardObject.transform.position = cutPos;
                _cutCardObject.transform.rotation = faceDownRot;
            }
        }
        _lastCutCardState = isCutCardDrawn;

        for (int i = 0; i < manager.maxSeats; i++)
        {
            int currentCount = manager.GetPlayerHandCount(i);
            if (currentCount > _lastPlayerHandCounts[i])
            {
                int[] hand = manager.GetPlayerHand(i);
                for (int j = _lastPlayerHandCounts[i]; j < currentCount; j++) SpawnPlayerCard(i, j, hand[j]);
                _lastPlayerHandCounts[i] = currentCount;
            }
            else if (currentCount < _lastPlayerHandCounts[i]) _lastPlayerHandCounts[i] = currentCount;
        }

        int currentDealerCount = manager.GetDealerHandCount();
        if (currentDealerCount > _lastDealerHandCount)
        {
            int[] dHand = manager.GetDealerHand();
            for (int j = _lastDealerHandCount; j < currentDealerCount; j++) SpawnDealerCard(j, dHand[j], (state <= 3 && j == 1));
            _lastDealerHandCount = currentDealerCount;
        }
        else if (currentDealerCount < _lastDealerHandCount) _lastDealerHandCount = currentDealerCount;

        if (state >= 4 && !_dealerHoleCardRevealed && _dealerHoleCard != null) RevealDealerHoleCard();
    }

    private void DoShuffleVisual()
    {
        Vector3 shoePos = shoeTransform != null ? shoeTransform.position : Vector3.zero;
        Quaternion shoeRot = shoeTransform != null ? shoeTransform.rotation : Quaternion.identity;
        Quaternion faceDownRot = shoeRot * Quaternion.Euler(0, 0, 180);
        _tableCardCount = 0; _dealerHoleCard = null; _dealerHoleCardRevealed = false;
        
        // ★修正: ループの回数を7に変更
        for(int i=0; i<7; i++) _lastPlayerHandCounts[i] = 0;
        _lastDealerHandCount = 0;
        for (int i = 0; i < _totalCards; i++)
        {
            Vector3 pos = shoePos + Vector3.up * (i * stackYOffset);
            CardObject card = _allCardObjects[i];
            card.transform.position = pos; card.transform.rotation = faceDownRot; card.SetCard(-1, false);
        }
        if (_cutCardObject != null)
        {
            Vector3 cutPos = shoePos + Vector3.up * (manager.cutCardThreshold * stackYOffset);
            _cutCardObject.transform.position = cutPos; _cutCardObject.transform.rotation = faceDownRot;
        }
        _lastCutCardState = false; _shoeTopIndex = _totalCards - 1; _discardTopIndex = 0;
    }

    private void MoveTableCardsToDiscard()
    {
        Vector3 discardPos = discardTransform != null ? discardTransform.position : Vector3.zero;
        Quaternion discardRot = discardTransform != null ? discardTransform.rotation : Quaternion.identity;
        Quaternion faceDownRot = discardRot * Quaternion.Euler(0, 0, 180);
        for (int i = 0; i < _tableCardCount; i++)
        {
            CardObject card = _tableCards[i]; if (card == null) continue;
            Vector3 targetPos = discardPos + Vector3.up * (_discardTopIndex * stackYOffset);
            card.MoveTo(targetPos, faceDownRot, moveDuration); card.SetCard(-1, false);
            _discardTopIndex++; if (_discardTopIndex >= _totalCards) _discardTopIndex = 0;
        }
        
        // ★修正: ループの回数を7に変更
        _tableCardCount = 0; for(int i=0; i<7; i++) _lastPlayerHandCounts[i] = 0;
        _lastDealerHandCount = 0; _dealerHoleCard = null; _dealerHoleCardRevealed = false;
    }

    private void SpawnPlayerCard(int seatIndex, int cardIndex, int cardId)
    {
        if (playerHandPositions.Length <= seatIndex || playerHandPositions[seatIndex] == null || _shoeTopIndex < 0) return;
        CardObject card = _allCardObjects[_shoeTopIndex--]; _tableCards[_tableCardCount++] = card;
        Transform basePos = playerHandPositions[seatIndex];
        Vector3 targetPos = basePos.position + basePos.right * (cardIndex * cardSpacingX) + basePos.up * (cardIndex * 0.001f);
        card.SetCard(cardId, true); card.MoveTo(targetPos, basePos.rotation, moveDuration);
    }

    private void SpawnDealerCard(int cardIndex, int cardId, bool faceDown)
    {
        if (dealerHandPosition == null || _shoeTopIndex < 0) return;
        CardObject card = _allCardObjects[_shoeTopIndex--]; _tableCards[_tableCardCount++] = card;
        Transform basePos = dealerHandPosition;
        Vector3 targetPos = basePos.position + basePos.right * (cardIndex * cardSpacingX) + basePos.up * (cardIndex * 0.001f);
        Quaternion targetRot = basePos.rotation;
        if (faceDown) { targetRot *= Quaternion.Euler(0, 0, 180); card.SetCard(-1, false); _dealerHoleCard = card; }
        else card.SetCard(cardId, true);
        card.MoveTo(targetPos, targetRot, moveDuration);
    }

    private void RevealDealerHoleCard()
    {
        if (_dealerHoleCard == null) return;
        int[] dHand = manager.GetDealerHand();
        if (dHand.Length > 1)
        {
            _dealerHoleCard.SetCard(dHand[1], true);
            Transform basePos = dealerHandPosition;
            Vector3 targetPos = basePos.position + basePos.right * (1 * cardSpacingX) + basePos.up * 0.001f;
            _dealerHoleCard.MoveTo(targetPos, basePos.rotation, moveDuration);
        }
        _dealerHoleCardRevealed = true;
    }
}