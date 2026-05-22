/* * Canvas Name: BlackjackDTab
 * Version: 14
 */
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BlackjackDTab : UdonSharpBehaviour
{
    [Header("System")]
    public BlackjackManager manager;

    [Header("UI Panels")]
    public GameObject panelManual;

    [Header("Auto Mode UI Controls")]
    public TextMeshProUGUI autoModeButtonText;
    public TextMeshProUGUI statusText;
    public Button dealButton;
    public Button clearButton;

    [Header("Player Info UI")]
    public TextMeshProUGUI[] playerNameTexts;
    public TextMeshProUGUI[] playerBetTexts;

    [Header("Dealer Hand UI")]
    public Transform dealerHandContainer;
    public GameObject cardIconPrefab;

    private string[] _stateNames = { "WAITING", "BETTING", "DEALING", "PLAYER TURN", "DEALER TURN", "JUDGE" };

    void Update()
    {
        if (manager == null) return;

        UpdateDealerPanel();
        UpdateAutoModeUI();
    }

    private void UpdateDealerPanel()
    {
        for (int i = 0; i < manager.maxSeats; i++)
        {
            int playerId = manager.GetSeatOwnerId(i);
            string pName = "Empty";

            if (playerId != -1)
            {
                VRCPlayerApi p = VRCPlayerApi.GetPlayerById(playerId);
                pName = (p != null) ? p.displayName : "Unknown";
                
                if (manager.GetSeatReady(i))
                {
                    pName = $"<color=green>[READY]</color> {pName}";
                }
                else
                {
                    pName = $"<color=red>[WAIT]</color> {pName}";
                }
            }

            if (playerNameTexts != null && playerNameTexts.Length > i && playerNameTexts[i] != null)
            {
                playerNameTexts[i].text = pName;
            }

            if (playerBetTexts != null && playerBetTexts.Length > i && playerBetTexts[i] != null)
            {
                playerBetTexts[i].text = $"${manager.GetSeatBet(i):F0}";
            }
        }

        UpdateDealerHand();
    }

    private void UpdateDealerHand()
    {
        if (dealerHandContainer == null || cardIconPrefab == null) return;

        int[] cards = manager.GetDealerHand();
        int count = manager.GetDealerHandCount();
        int state = manager.GetGameState();

        if (dealerHandContainer.childCount == count) return;

        foreach (Transform child in dealerHandContainer)
        {
            Destroy(child.gameObject);
        }

        bool hideHoleCard = (state <= 3 && count >= 2);

        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(cardIconPrefab);
            icon.transform.SetParent(dealerHandContainer, false);
            
            CasinoCardUI ui = icon.GetComponent<CasinoCardUI>();
            if (ui != null)
            {
                if (hideHoleCard && i == 1)
                {
                    ui.SetCard(-1);
                }
                else
                {
                    ui.SetCard(cards[i]);
                }
            }
        }
    }

    private void UpdateAutoModeUI()
    {
        int state = manager.GetGameState();
        bool isAuto = manager.isAutoMode;

        bool hasPlayer = false;
        bool isAllReady = true;
        for (int i = 0; i < manager.maxSeats; i++)
        {
            if (manager.GetSeatOwnerId(i) != -1)
            {
                hasPlayer = true;
                if (!manager.GetSeatReady(i) || manager.GetSeatBet(i) <= 0)
                {
                    isAllReady = false;
                    break;
                }
            }
        }
        if (!hasPlayer)
        {
            isAllReady = false;
        }

        if (panelManual != null)
        {
            panelManual.SetActive(!isAuto);
        }

        if (autoModeButtonText != null)
        {
            if (isAuto)
            {
                autoModeButtonText.text = "AUTO";
            }
            else
            {
                autoModeButtonText.text = "MANUAL";
            }
        }

        if (dealButton != null)
        {
            dealButton.interactable = !isAuto && (state == 1) && isAllReady;
        }
        
        if (clearButton != null)
        {
            clearButton.interactable = !isAuto && (state == 5);
        }

        if (statusText != null)
        {
            string stateName = "UNKNOWN";
            if (state >= 0 && state < _stateNames.Length)
            {
                stateName = _stateNames[state];
            }

            if (isAuto)
            {
                if (state == 5)
                {
                    statusText.text = $"AUTO: {stateName}\n(Next in {manager.GetAutoTimer():F0}s)";
                }
                else if (state <= 1)
                {
                    statusText.text = $"AUTO: {stateName}\n(Waiting for All Ready)";
                }
                else
                {
                    statusText.text = $"AUTO: {stateName}";
                }
            }
            else
            {
                statusText.text = $"MANUAL: {stateName}";
            }
        }
    }

    public void OnClickToggleAuto()
    {
        if (manager != null)
        {
            manager.ToggleAutoMode();
        }
    }

    public void OnClickDeal()
    {
        if (manager != null)
        {
            manager.StartDealing();
        }
    }

    public void OnClickClear()
    {
        if (manager != null && manager.GetGameState() == 5)
        {
            manager.ClearGame();
        }
    }
}