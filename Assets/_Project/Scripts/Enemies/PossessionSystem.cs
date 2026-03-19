using UnityEngine;

public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _playerTransform;
    private float     _bonusPossessionTime; // Charan 

    public static event System.Action<bool, EnemyController> OnPossessionChanged; // Gagan 

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

    public bool TryPossessTarget(EnemyController target)
    {
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

    public void MovePossessedEnemy(Vector2 input)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        var ai = PossessedEnemy.GetComponent<EnemyAI>();
        if (ai != null && input != Vector2.zero)
            ai.StepInDirection(input);
    }

    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        released.SetState(EnemyController.EnemyState.Chasing);
        OnPossessionChanged?.Invoke(false, released); // Gagan 
    }

    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        target.stats.possessionDuration += _bonusPossessionTime;
        target.SetState(EnemyController.EnemyState.Possessed);
        _playerTransform?.GetComponent<PlayerController>()?.OnPossessionStart(target); // Gagan
        OnPossessionChanged?.Invoke(true, target); // Gagan 
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_playerTransform.position, 0.75f);
    }
}
