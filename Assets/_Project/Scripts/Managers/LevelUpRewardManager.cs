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
        // In editor, it would be best to use AssetDatabase to find all upgrades
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
        // load from Resources folder at runtime
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
    }

    private void Start()
    {
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp += HandleLevelUp;
        }
    }

    // OnDisable will be used for safer cleanup
    private void OnDisable()
    {
        // Only unsubscribe if its an active instance
        if (Instance == this && ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        int randomIndex = Random.Range(0, allPossibleUpgrades.Count);
        UpgradeData selectedUpgrade = allPossibleUpgrades[randomIndex];

        if (StatManager.Instance != null)
        {
            StatManager.Instance.AddUpgrade(selectedUpgrade);
        }

        string levelUpMessage = $"Leveled Up: {selectedUpgrade.description}";
        
        AudioManager.Instance?.PlayLevelUp();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessageHUD(levelUpMessage, 2f);
        }
    }
}