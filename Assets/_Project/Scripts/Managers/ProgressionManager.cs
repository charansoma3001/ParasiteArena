using UnityEngine;
using System;

public class ProgressionManager : MonoBehaviour
{
    // Singleton Instance
    public static ProgressionManager Instance { get; private set; }

    [Header("Progression Stats")]
    public int CurrentLevel = 1;
    public int CurrentXP = 0;
    public int XPToNextLevel = 100;

    [Header("Enemy Kill XP")]
    public int baseKillXP = 20;
    public float levelXPMultiplier = 1.5f;

    [Header("Enemy Kill Gold")]
    public int minGoldPerKill = 30;
    public int maxGoldPerKill = 50;
    
    [Header("Economy")]
    public int CurrentGold = 0;

    // Events that other scripts will listen
    public event Action<int, int> OnXPAdded;     
    public event Action<int> OnLevelUp;          
    public event Action<int> OnGoldChanged; 

    private void Update() 
    {
        if (Input.GetKeyDown(KeyCode.P)) AddXP(45);
        if (Input.GetKeyDown(KeyCode.G)) AddGold(10);
    }

    private void OnEnable()
    {
        EnemyController.OnEnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyController.OnEnemyDied -= HandleEnemyDied;
    }

    private void Awake()
    {
        // Enforce Singleton again
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // this is to keep the manager alive
    }

    // levle up logic

    public void AddXP(int amount)
    {
        CurrentXP += amount;

        if (CurrentXP >= XPToNextLevel)
        {
            LevelUp();
        }

        // announce for player to receive it
        OnXPAdded?.Invoke(CurrentXP, XPToNextLevel);
    }

    private void HandleEnemyDied(EnemyController deadEnemy)
    {
        if (deadEnemy == null) return;

        int levelIndex = Mathf.Max(0, CurrentLevel - 1);
        float scaledXP = baseKillXP * Mathf.Pow(levelXPMultiplier, levelIndex);
        AddXP(Mathf.RoundToInt(scaledXP));

        int minGold = Mathf.Min(minGoldPerKill, maxGoldPerKill);
        int maxGold = Mathf.Max(minGoldPerKill, maxGoldPerKill);
        int goldReward = UnityEngine.Random.Range(minGold, maxGold + 1);
        AddGold(goldReward);
    }

    private void LevelUp()
    {
        CurrentLevel++;
        CurrentXP -= XPToNextLevel; // Carry over XP

        // to increase next level requirement by 50%
        XPToNextLevel = Mathf.RoundToInt(XPToNextLevel * 1.5f);

        // Announce the level up
        OnLevelUp?.Invoke(CurrentLevel);

        // when player gains enough XP to level up twice instantly
        if (CurrentXP >= XPToNextLevel) 
        {
            LevelUp();
        }
    }

    public void AddGold(int amount)
    {
        CurrentGold += amount;
        OnGoldChanged?.Invoke(CurrentGold);
    }

    public bool SpendGold(int amount)
    {
        if (CurrentGold >= amount)
        {
            CurrentGold -= amount;
            OnGoldChanged?.Invoke(CurrentGold);
            return true;
        }
        return false;
    }
}