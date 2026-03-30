using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelUpRewardManager : MonoBehaviour
{
    public static LevelUpRewardManager Instance { get; private set; }

    private List<UpgradeData> allPossibleUpgrades;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadAllUpgrades();
    }

    private void LoadAllUpgrades()
    {
        #if UNITY_EDITOR
        // In editor, use AssetDatabase to find all upgrades
        string[] guids = AssetDatabase.FindAssets("t:UpgradeData", new[] { "Assets/_Project/ScriptableObjects/Upgrades" });
        var allUpgrades = new List<UpgradeData>();
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(assetPath);
            if (upgrade != null)
            {
                allUpgrades.Add(upgrade);
            }
        }
        #else
        // At runtime, load from Resources folder
        var allUpgrades = new List<UpgradeData>(Resources.LoadAll<UpgradeData>("ScriptableObjects/Upgrades"));
        #endif
        
        allPossibleUpgrades = new List<UpgradeData>();
        
        // Keep only upgrades with exactly 1 stat modifier
        foreach (var upgrade in allUpgrades)
        {
            if (upgrade.statModifiers.Count == 1)
            {
                allPossibleUpgrades.Add(upgrade);
            }
        }
        
        if (allPossibleUpgrades.Count == 0)
        {
            Debug.LogWarning("[LevelUpRewardManager] No upgrades with 1 stat modifier found!");
        }
        else
        {
            Debug.Log($"[LevelUpRewardManager] Loaded {allPossibleUpgrades.Count} upgrades with 1 stat modifier");
        }
    }

    private void Start()
    {
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp += HandleLevelUp;
        }
        else
        {
            Debug.LogError("[LevelUpRewardManager] ProgressionManager not found!");
        }
    }

    // 3. Move unsubscription to OnDisable for safer cleanup
    private void OnDisable()
    {
        // Only unsubscribe if THIS is the active instance (ignores duplicates destroyed in Awake)
        if (Instance == this && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    private void HandleLevelUp(int newLevel)
    {

        if (allPossibleUpgrades == null || allPossibleUpgrades.Count == 0)
        {
            Debug.LogError("[LevelUpRewardManager] No upgrades available!");
            return;
        }

        int randomIndex = Random.Range(0, allPossibleUpgrades.Count);
        UpgradeData selectedUpgrade = allPossibleUpgrades[randomIndex];

        if (StatManager.Instance != null)
        {
            StatManager.Instance.AddUpgrade(selectedUpgrade);
        }
        else
        {
            Debug.LogError("[LevelUpRewardManager] StatManager not found!");
            return;
        }

        string levelUpMessage = $"Level Up: {selectedUpgrade.upgradeName}";
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessageHUD(levelUpMessage, 2f);
        }
        else
        {
            Debug.Log(levelUpMessage);
        }
    }
}