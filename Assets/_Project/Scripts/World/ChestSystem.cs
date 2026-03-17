using System.Collections.Generic;
using UnityEngine;

public class ChestSystem : MonoBehaviour
{
    [Header("Drop Probabilities (Must sum to 100)")]
    [Range(0, 100)] public float goldChance = 50f;
    [Range(0, 100)] public float upgradeChance = 30f;
    [Range(0, 100)] public float healChance = 20f; 

    [Header("Gold Drop Settings")]
    public int minGold = 10;
    public int maxGold = 50;

    [Header("Available Upgrades (Drag ScriptableObjects Here)")]
    public List<UpgradeData> possibleUpgrades;

    private bool hasBeenOpened = false;

    // --- INTEGRATION: Gagan will call this function ---
    // When the player walks up to the chest and presses 'E', Gagan's script 
    // will look for this ChestSystem component and trigger OpenChest().
    public void OpenChest()
    {
        if (hasBeenOpened) return;
        hasBeenOpened = true;

        // The RNG Roll (Pick a random number between 0.0 and 100.0)
        float roll = Random.Range(0f, 100f);

        if (roll <= goldChance)
        {
            // Drop Gold
            int goldAmount = Random.Range(minGold, maxGold + 1);
            ProgressionManager.Instance.AddGold(goldAmount);
            Debug.Log($"Chest opened: Found {goldAmount} Gold!");
        }
        else if (roll <= (goldChance + upgradeChance))
        {
            // Drop an Upgrade
            if (possibleUpgrades.Count > 0)
            {
                int randomIndex = Random.Range(0, possibleUpgrades.Count);
                UpgradeData droppedUpgrade = possibleUpgrades[randomIndex];
                
                // Send it directly to your StatManager!
                StatManager.Instance.AddUpgrade(droppedUpgrade);
                Debug.Log($"Chest opened: Found Upgrade [{droppedUpgrade.upgradeName}]!");
            }
            else
            {
                Debug.LogWarning("Chest rolled an upgrade, but the 'possibleUpgrades' list is empty!");
            }
        }
        else
        {
            // Drop a Heal (Placeholder until Gagan builds the Player Health system)
            Debug.Log("Chest opened: Found a Health Potion! (Player healed)");
            // Future code: PlayerHealth.Instance.Heal(25);
        }

        // Optional: Change the sprite to an "open chest" image or destroy it
        // Destroy(gameObject, 0.5f); 
    }

    // --- FOR YOUR SANDBOX TESTING ---
    private void Update()
    {
        // Press 'O' to simulate Gagan's player opening the chest
        if (Input.GetKeyDown(KeyCode.O))
        {
            OpenChest();
        }
    }
}