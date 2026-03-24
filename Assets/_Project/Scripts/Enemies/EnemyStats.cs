using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "Game/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    public enum EnemyType { Warrior, Swordsman, Archer, Tank, Mage, Chomp, Spawner, Boss }

    [Header("Identity")]
    public EnemyType enemyType;
    public string    displayName = "Enemy";
    public bool      isBoss      = false;

    [Header("Base Stats")]
    public float maxHealth      = 100f;
    public float moveSpeed      = 3f;
    public float attackDamage   = 10f;
    public float attackRange    = 2f;
    public float attackCooldown = 1.5f;
    public float detectionRange = 8f;
    public float roamRange      = 5f;
    public int   xpReward       = 10;   
    public int   goldReward     = 5;    

    [Header("Possession")]
    public float possessionDuration           = 8f;
    public float perfectDodgePossessionWindow = 1.2f;

    [Header("Attack Shape - Warrior / Swordsman")]
    public float swordArcAngle  = 90f;
    public float swordArcRadius = 2.5f;

    [Header("Attack Shape - Archer")]
    public float arrowSpeed = 12f;

    [Header("Attack Shape - Mage")]
    public float meteorRadius = 3f;
    public float meteorDelay  = 1.5f;

    [Header("Attack Shape - Tank")]
    public float bashDistance = 4f;
    public float bashDamage   = 20f;

    [Header("Spawner")]
    public float      spawnInterval = 10f;
    public int        maxSpawnCount = 3;
    public GameObject spawnPrefab;
}