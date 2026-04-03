using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatManager : MonoBehaviour
{
    // Singleton Setup
    public static StatManager Instance { get; private set; }
    private PlayerController playerController;
    public event System.Action OnStatsChanged;

    // A class to set up starting values
    [System.Serializable]
    public class BaseStat
    {
        public StatType statType;
        public float baseValue;
    }

    [Header("Base Player Stats")]
    public List<BaseStat> startingStats;
    private List<UpgradeData> activeUpgrades = new List<UpgradeData>();

    private Dictionary<StatType, float> currentStats = new Dictionary<StatType, float>();

    private void Awake() //sets up the singleton for starting stats
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RecalculateStats(); 
    }

    public void AddUpgrade(UpgradeData newUpgrade)
    {
        activeUpgrades.Add(newUpgrade);
        RecalculateStats(); 
    }

    private void RecalculateStats()
    {
        currentStats.Clear();

        foreach (var stat in startingStats)
        {
            currentStats[stat.statType] = stat.baseValue;
        }

        foreach (var upgrade in activeUpgrades)
        {
            foreach (var mod in upgrade.statModifiers)
            {
                if (!mod.isMultiplier && currentStats.ContainsKey(mod.statToModify))
                {
                    currentStats[mod.statToModify] += mod.modifierValue;
                }
            }
        }

        // multipliers
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

    public float GetStat(StatType stat)
    {
        if (currentStats.ContainsKey(stat))
        {
            return currentStats[stat];
        }
        
        return 0f;
    }
}