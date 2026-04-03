using System.Collections.Generic;
using UnityEngine;

public class ChestSystem : MonoBehaviour
{
    [Header("Drop Probabilities")]
    [Range(0, 100)] public float goldChance = 50f;
    [Range(0, 100)] public float upgradeChance = 30f;
    [Range(0, 100)] public float healChance = 20f; 

    [Header("Gold Drop Settings")]
    public int minGold = 10;
    public int maxGold = 50;

    [Header("Available Upgrades")]
    public List<UpgradeData> possibleUpgrades;

    [Header("Audio")]
    public AudioClip openSfx;

    private bool hasBeenOpened = false;
    private PlayerController player;

    public void OpenChest()
    {
        if (hasBeenOpened) return;
        hasBeenOpened = true;

        if (openSfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXAtPos(openSfx, transform.position);

        string chestMessage;

        // pick a random number between 0.0 and 100.0
        float roll = Random.Range(0f, 100f);

        if (roll <= goldChance)
        {
            // Drop Gold
            int goldAmount = Random.Range(minGold, maxGold + 1);
            ProgressionManager.Instance.AddGold(goldAmount);
            chestMessage = $"Chest opened: Found {goldAmount} Gold!";
        }
        else if (roll <= (goldChance + upgradeChance))
        {
            if (possibleUpgrades.Count > 0)
            {
                int randomIndex = Random.Range(0, possibleUpgrades.Count);
                UpgradeData droppedUpgrade = possibleUpgrades[randomIndex];
                
                StatManager.Instance.AddUpgrade(droppedUpgrade);
                chestMessage = $"Chest opened: Found Upgrade [{droppedUpgrade.upgradeName}]!";
            }
            else
            {
                chestMessage = "Chest opened, but no upgrade was available.";
            }
        }
        else
        {
            chestMessage = "You found a Health Potion!";
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
            }
            if (player != null)
            {
                player.Heal(25);
            }
        }

        ShowMessageHUD(chestMessage);
    }

    private void ShowMessageHUD(string message)
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowMessageHUD(message);
        }
    }

}