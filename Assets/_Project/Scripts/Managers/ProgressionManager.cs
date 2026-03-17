using UnityEngine;
using System;

public class ProgressionManager : MonoBehaviour
{
    // 1. The Singleton Instance
    public static ProgressionManager Instance { get; private set; }

    [Header("Progression Stats")]
    public int CurrentLevel = 1;
    public int CurrentXP = 0;
    public int XPToNextLevel = 100; // Base XP needed for level 2
    
    [Header("Economy")]
    public int CurrentGold = 0;

    // 2. The Events (Other scripts will listen to these)
    public event Action<int, int> OnXPAdded;     // Broadcasts: (CurrentXP, XPToNextLevel)
    public event Action<int> OnLevelUp;          // Broadcasts: (NewLevel)
    public event Action<int> OnGoldChanged;      // Broadcasts: (CurrentGold)

    private void Update() 
    {
        if (Input.GetKeyDown(KeyCode.Space)) AddXP(45);
        if (Input.GetKeyDown(KeyCode.G)) AddGold(10);
    }

    private void Awake()
    {
        // Enforce the Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Keeps your manager alive when loading the Main Arena
    }

    // --- XP & LEVELING LOGIC ---

    public void AddXP(int amount)
    {
        CurrentXP += amount;
        
        // Announce to the rest of the game that XP changed (Gagan's UI will listen for this)
        OnXPAdded?.Invoke(CurrentXP, XPToNextLevel);

        if (CurrentXP >= XPToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        CurrentLevel++;
        CurrentXP -= XPToNextLevel; // Carry over any excess XP

        // The Math Curve: Increase next level requirement by 50%
        XPToNextLevel = Mathf.RoundToInt(XPToNextLevel * 1.5f);

        // Announce the level up! (Gagan's UI and Terry's enemy spawner will listen for this)
        OnLevelUp?.Invoke(CurrentLevel);

        // Safety check: Did they gain enough XP to level up twice instantly?
        if (CurrentXP >= XPToNextLevel) 
        {
            LevelUp();
        }
    }

    // --- ECONOMY LOGIC ---

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
            return true; // Purchase successful
        }
        
        Debug.Log("Not enough gold!");
        return false; // Purchase failed
    }
}