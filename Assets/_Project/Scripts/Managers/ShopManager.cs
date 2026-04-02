using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    
    [Header("Shop Inventory Data")]
    public List<UpgradeData> allPossibleUpgrades;
    private UpgradeData[] currentShopUpgrades = new UpgradeData[3]; 

    [Header("UI Panel Reference")]
    public GameObject shopPanel;

    [Header("Wave Button")]
    public Button startNextWaveButton;

    [Header("Slot 1 References")]
    public Button button1;
    public TextMeshProUGUI nameText1;
    public TextMeshProUGUI costText1;
    public TextMeshProUGUI descText1;

    [Header("Slot 2 References")]
    public Button button2;
    public TextMeshProUGUI nameText2;
    public TextMeshProUGUI costText2;
    public TextMeshProUGUI descText2;

    [Header("Slot 3 References")]
    public Button button3;
    public TextMeshProUGUI nameText3;
    public TextMeshProUGUI costText3;
    public TextMeshProUGUI descText3;

    private void Start()
    {
        shopPanel.SetActive(false);
    }

    // the Wave Manager will be called when a wave ends
    public void OpenShop()
    {
        Time.timeScale = 0f;
        shopPanel.SetActive(true);
        if (startNextWaveButton != null) startNextWaveButton.interactable = true;
        RollShopItems();
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        if (startNextWaveButton != null) startNextWaveButton.interactable = false;
        Time.timeScale = 1f;
    }

    // Link this to the Start Next Wave button OnClick in the Inspector
    public void StartNextWave()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.StartNextWave();
    }

    private void RollShopItems()
    {
        // Arrays to make looping through the UI easier
        Button[] buttons = { button1, button2, button3 };
        TextMeshProUGUI[] names = { nameText1, nameText2, nameText3 };
        TextMeshProUGUI[] costs = { costText1, costText2, costText3 };
        TextMeshProUGUI[] descs = { descText1, descText2, descText3 };

        for (int i = 0; i < 3; i++)
        {
            // Pick a random upgrade from your master list
            int rand = Random.Range(0, allPossibleUpgrades.Count);
            UpgradeData chosenUpgrade = allPossibleUpgrades[rand];
            currentShopUpgrades[i] = chosenUpgrade;

            // Update the UI text
            names[i].text = chosenUpgrade.upgradeName;
            costs[i].text = chosenUpgrade.goldCost.ToString() + " Gold";
            descs[i].text = chosenUpgrade.description;

            // Check if player has enough gold; if not, grey out the button
            buttons[i].interactable = ProgressionManager.Instance.CurrentGold >= chosenUpgrade.goldCost;
        }
    }

    // --- TRANSACTION LOGIC ---

    // We link this function to the physical UI buttons in the Unity Inspector
    public void BuyItem(int slotIndex)
    {
        UpgradeData upgradeToBuy = currentShopUpgrades[slotIndex];

        // ProgressionManager.SpendGold() returns TRUE if we had enough money!
        if (ProgressionManager.Instance.SpendGold(upgradeToBuy.goldCost))
        {
            // Apply the math to the player
            StatManager.Instance.AddUpgrade(upgradeToBuy);

            // Mark the item as Sold and disable the button
            MarkAsSold(slotIndex);

            // Re-check the other buttons (in case buying this made the player too poor for the others)
            RefreshButtonStates();
        }
    }

    private void MarkAsSold(int slotIndex)
    {
        if (slotIndex == 0) { button1.interactable = false; costText1.text = "SOLD"; }
        if (slotIndex == 1) { button2.interactable = false; costText2.text = "SOLD"; }
        if (slotIndex == 2) { button3.interactable = false; costText3.text = "SOLD"; }
    }

    private void RefreshButtonStates()
    {
        // If they don't have enough gold for the remaining items, disable those buttons
        if (costText1.text != "SOLD") button1.interactable = ProgressionManager.Instance.CurrentGold >= currentShopUpgrades[0].goldCost;
        if (costText2.text != "SOLD") button2.interactable = ProgressionManager.Instance.CurrentGold >= currentShopUpgrades[1].goldCost;
        if (costText3.text != "SOLD") button3.interactable = ProgressionManager.Instance.CurrentGold >= currentShopUpgrades[2].goldCost;
    }

    // For testing in the sandbox: Press 'B' to open/close the shop
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (shopPanel.activeSelf) CloseShop();
            else OpenShop();
        }
    }
}
