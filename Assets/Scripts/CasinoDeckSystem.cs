using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CasinoDeckSystem : UdonSharpBehaviour
{
    [Header("Settings")]
    [Tooltip("使用するデッキの数")]
    [Range(1, 8)]
    public int numberOfDecks = 6;

    private int[] _cards;
    private int _currentCardIndex = 0;
    private int _runningCount = 0;

    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }

    public void InitializeDeck()
    {
        _cards = new int[52 * numberOfDecks];
        _currentCardIndex = 0;

        int index = 0;
        for (int d = 0; d < numberOfDecks; d++)
        {
            for (int i = 0; i < 52; i++)
            {
                _cards[index] = i;
                index++;
            }
        }
    }

    public void ShuffleDeck()
    {
        if (_cards == null) InitializeDeck();

        for (int i = _cards.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = _cards[i];
            _cards[i] = _cards[j];
            _cards[j] = temp;
        }

        _currentCardIndex = 0;
        ResetCount();
    }

    public int DrawCard()
    {
        if (_cards == null) return -1;

        if (_currentCardIndex >= _cards.Length)
        {
            Debug.LogWarning("[CasinoDeck] Deck is empty!");
            return -1; 
        }

        int cardId = _cards[_currentCardIndex];
        _currentCardIndex++;

        UpdateCounting(cardId);

        return cardId;
    }

    public int GetRemainingCardsCount()
    {
        if (_cards == null) return 0;
        return _cards.Length - _currentCardIndex;
    }

    public int GetTotalCardsCount()
    {
        if (_cards == null) return 52 * numberOfDecks;
        return _cards.Length;
    }

    private void UpdateCounting(int cardId)
    {
        int rank = cardId % 13;
        if (rank >= 1 && rank <= 5) _runningCount++; 
        else if (rank == 0 || rank >= 9) _runningCount--; 
    }

    public float GetTrueCount()
    {
        float decksRemaining = GetRemainingCardsCount() / 52f;
        if (decksRemaining < 0.5f) decksRemaining = 0.5f; 
        return _runningCount / decksRemaining;
    }

    public void ResetCount()
    {
        _runningCount = 0;
    }
}