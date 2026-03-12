using UnityEngine;

// 1. Define the types of stats we can upgrade. 
// Terry and Gagan will read from this list later.
public enum StatType
{
    MaxHealth,
    MovementSpeed,
    AttackDamage,
    DashCooldown,
    PossessionDuration,
    HostDecayRate
}

// 2. This creates a new menu option when you right-click in the Project window!
[CreateAssetMenu(fileName = "New Upgrade", menuName = "ParasiteArena/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("UI Details (For Gagan)")]
    public string upgradeName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon; 
    
    [Header("Economy")]
    public int goldCost;

    [Header("Stat Modifiers")]
    public StatType statToModify;
    
    [Tooltip("The actual number added or multiplied to the base stat.")]
    public float modifierValue; 
    
    [Tooltip("Check this if it's a multiplier (e.g. 1.1 for +10%), uncheck if it's a flat addition (e.g. +50 HP)")]
    public bool isMultiplier; 
}