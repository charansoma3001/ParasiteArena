using UnityEngine;

public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _playerTransform;
    private float     _bonusPossessionTime; // Charan - ItemSystem (relics)

    public static event System.Action<bool, EnemyController> OnPossessionChanged; // Gagan - UIManager

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player"); // Gagan
        if (playerGO) _playerTransform = playerGO.transform;
    }

    // Called by PlayerController.TryPossessAdjacent() with the already-validated target
    public bool TryPossessTarget(EnemyController target)
    {
        if (IsPossessing) return false;
        if (target == null || target.IsDead || target.IsPossessed || target.stats.isBoss) return false;
        BeginPossession(target);
        return true;
    }

    // Called by Gagan - InputHandler (Ability button while possessed)
    public bool UsePossessedAbility()
    {
        if (!IsPossessing || PossessedEnemy == null) return false;
        return PossessedEnemy.UsePossessedAbility();
    }

    // Called by Gagan - InputHandler (Block button while possessed Swordsman)
    public void UsePossessedBlock()
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.UsePossessedBlock();
    }

    // Called every FixedUpdate by Gagan - InputHandler while IsPossessing
    public void MovePossessedEnemy(Vector2 input)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        var ai = PossessedEnemy.GetComponent<EnemyAI>();
        if (ai != null && input != Vector2.zero)
            ai.StepInDirection(input); // uses tile-based stepping
    }

    // Called by EnemyController when timer expires, or Gagan - InputHandler (cancel key)
    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        released.SetState(EnemyController.EnemyState.Chasing);
        OnPossessionChanged?.Invoke(false, released); // Gagan - UIManager
    }

    // Charan - ItemSystem: relic that extends possession duration
    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        target.stats.possessionDuration += _bonusPossessionTime;
        target.SetState(EnemyController.EnemyState.Possessed);
        _playerTransform?.GetComponent<PlayerController>()?.OnPossessionStart(target); // Gagan
        OnPossessionChanged?.Invoke(true, target); // Gagan - UIManager
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_playerTransform.position, 0.75f);
    }
}
