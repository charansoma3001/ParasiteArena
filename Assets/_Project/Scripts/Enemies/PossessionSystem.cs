using UnityEngine;

public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool IsPossessing { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    [Header("Possession Cooldown")]
    [Tooltip("Seconds the player must wait after leaving a host before possessing again.")]
    public float possessionCooldown = 3f;

    public float CooldownRemaining { get; private set; }

    private Transform player;

    public float BonusPossessionTime { get; private set; }
    public float HostDecayRate { get; private set; } = 1f;
    public float PlayerAttackDamage { get; private set; }

    public static event System.Action<bool, EnemyController> OnPossessionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go) player = go.transform;
        else Debug.LogWarning("PossessionSystem : No 'Player' tagged object found.");
    }

    private void Update()
    {
        if (CooldownRemaining > 0f)
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
    }

    public bool TryPossessTarget(EnemyController target)
    {
        if (CooldownRemaining > 0f)
        {
            return false;
        }

        if (IsPossessing) return false;
        if (target == null || target.IsDead || target.IsPossessed || target.stats.isBoss) return false;

        BeginPossession(target);
        return true;
    }

    public bool UsePossessedAbility()
    {
        if (!IsPossessing || PossessedEnemy == null) return false;
        return PossessedEnemy.UsePossessedAbility();
    }

    public void UsePossessedBlock()
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.UsePossessedBlock();
    }

    public void EndPossession()
    {
        if (!IsPossessing) return;

        var released = PossessedEnemy;
        IsPossessing = false;
        PossessedEnemy = null;

        released.SetState(EnemyController.EnemyState.Chasing);
        OnPossessionChanged?.Invoke(false, released);

        CooldownRemaining = possessionCooldown;
    }

    public void MovePossessedEnemy(Vector2 cardinalDir)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.GetComponent<EnemyAI>()?.StepInDirection(cardinalDir);
    }

    public void SetPossessionBonusTime(float bonus) => BonusPossessionTime = bonus;
    public void SetHostDecayRate(float rate) => HostDecayRate = rate;
    public void SetPlayerAttackDamage(float dmg) => PlayerAttackDamage = dmg;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing = true;
        PossessedEnemy = target;
        target.SetState(EnemyController.EnemyState.Possessed);
        player?.GetComponent<PlayerController>()?.OnPossessionStart(target);
        OnPossessionChanged?.Invoke(true, target);
    }
}