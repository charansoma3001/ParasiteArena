using System.Collections.Generic;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    // 1. Singleton Setup
    public static StatManager Instance { get; private set; }

    // 2. A mini-class to set up starting values in the Unity Inspector
    [System.Serializable]
    public class BaseStat
    {
        public StatType statType;
        public float baseValue;
    }

    [Header("Base Player Stats (Edit in Inspector)")]
    public List<BaseStat> startingStats;

    // 3. The list of all upgrades the player has collected this run
    private List<UpgradeData> activeUpgrades = new List<UpgradeData>();

    // 4. The final, calculated numbers ready to be used by the game
    private Dictionary<StatType, float> currentStats = new Dictionary<StatType, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Calculate the stats at the very start of the game
        RecalculateStats(); 
    }

    // --- UPGRADE LOGIC ---

    // Call this when the player buys something from the shop or opens a chest
    public void AddUpgrade(UpgradeData newUpgrade)
    {
        activeUpgrades.Add(newUpgrade);
        RecalculateStats(); 
    }

    // The Game Math Engine
    private void RecalculateStats()
    {
        currentStats.Clear();

        // Step 1: Set everything to their base values
        foreach (var stat in startingStats)
        {
            currentStats[stat.statType] = stat.baseValue;
        }

        // Step 2: Apply all FLAT additions first
        foreach (var upgrade in activeUpgrades)
        {
            // We added an inner loop to check every modifier on the item!
            foreach (var mod in upgrade.statModifiers)
            {
                if (!mod.isMultiplier && currentStats.ContainsKey(mod.statToModify))
                {
                    currentStats[mod.statToModify] += mod.modifierValue;
                }
            }
        }

        // Step 3: Apply all MULTIPLIERS second
        foreach (var upgrade in activeUpgrades)
        {
            foreach (var mod in upgrade.statModifiers)
            {
                if (mod.isMultiplier && currentStats.ContainsKey(mod.statToModify))
                {
                    currentStats[mod.statToModify] *= mod.modifierValue;
                }
            }
        }
    }

    // --- HOW THE REST OF THE TEAM GETS THE STATS ---

    // Gagan and Terry will call this function!
    public float GetStat(StatType stat)
    {
        if (currentStats.ContainsKey(stat))
        {
            return currentStats[stat];
        }
        
        Debug.LogWarning("Stat " + stat + " not found in StatManager!");
        return 0f; // Fallback
    }
}