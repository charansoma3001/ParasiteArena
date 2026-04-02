using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatManager : MonoBehaviour
{
    // Singleton Setup
    public static StatManager Instance { get; private set; }
    private PlayerController playerController;
    public event System.Action OnStatsChanged;

    // A mini-class to set up starting values in the Unity Inspector
    [System.Serializable]
    public class BaseStat
    {
        public StatType statType;
        public float baseValue;
    }

    [Header("Base Player Stats (Edit in Inspector)")]
    public List<BaseStat> startingStats;

    // The list of all upgrades the player has collected this run
    private List<UpgradeData> activeUpgrades = new List<UpgradeData>();

    // The final, calculated numbers ready to be used by the game
    private Dictionary<StatType, float> currentStats = new Dictionary<StatType, float>();

    private void Awake() //sets up the singleton and calculates starting stats
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

    public void AddUpgrade(UpgradeData newUpgrade) // saves a new upgrade and updates stats.
    {
        activeUpgrades.Add(newUpgrade);
        RecalculateStats(); 
    }

    // The Game Math Engine
    private void RecalculateStats()
    {
        currentStats.Clear();

        // Set everything to their base values
        foreach (var stat in startingStats)
        {
            currentStats[stat.statType] = stat.baseValue;
        }

        // Apply all FLAT additions first
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

        // Apply all MULTIPLIERS second
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

        ApplyStatsToPlayer();
        OnStatsChanged?.Invoke();
    }

    public void ForceApplyStats()
    {
        ApplyStatsToPlayer();
    }

    private float GetStatOr(StatType stat, float fallback)
    {
        return currentStats.ContainsKey(stat) ? currentStats[stat] : fallback;
    }

    private void ApplyStatsToPlayer()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.SetMaxHealth((int)GetStatOr(StatType.MaxHealth, 100f));
            playerController.SetSpeed(GetStatOr(StatType.MovementSpeed, 5f));
            playerController.SetDashCooldown(GetStatOr(StatType.DashCooldown, 0.7f));
            playerController.SetDashSpeed(GetStatOr(StatType.DashSpeed, 14f));
            playerController.SetDashDuration(GetStatOr(StatType.DashDuration, 0.12f));
            playerController.SetPossessionRange(GetStatOr(StatType.PossessionRange, 0.8f));
            playerController.SetAttackDamage(GetStatOr(StatType.AttackDamage, 0f));
            playerController.SetHostDecayRate(GetStatOr(StatType.HostDecayRate, 1f));
        }

        if (PossessionSystem.Instance != null)
        {
            PossessionSystem.Instance.SetPossessionBonusTime(GetStatOr(StatType.PossessionDuration, 0f));
            PossessionSystem.Instance.SetHostDecayRate(GetStatOr(StatType.HostDecayRate, 1f));
            PossessionSystem.Instance.SetPlayerAttackDamage(GetStatOr(StatType.AttackDamage, 0f));
        }
    }

    // Gagan and Terry will call this function!
    public float GetStat(StatType stat) // lets other scripts ask for a stat value
    {
        if (currentStats.ContainsKey(stat))
        {
            return currentStats[stat];
        }
        
        return 0f; // Fallback
    }
}