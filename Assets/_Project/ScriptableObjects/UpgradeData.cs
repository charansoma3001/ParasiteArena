using System.Collections.Generic;
using UnityEngine;

public enum StatType
{
    MaxHealth,
    MovementSpeed,
    AttackDamage,
    DashCooldown,
    PossessionDuration,
    HostDecayRate,
    DashSpeed,
    DashDuration,
    PossessionRange
}

[System.Serializable]
public class StatModifier
{
    public StatType statToModify;
    public float modifierValue; 
    public bool isMultiplier; 
}

[CreateAssetMenu(fileName = "New Upgrade", menuName = "ParasiteArena/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("UI Details")]
    public string upgradeName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon; 
    
    [Header("Economy")]
    public int goldCost;

    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new List<StatModifier>();
}