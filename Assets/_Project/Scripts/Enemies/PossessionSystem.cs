using UnityEngine;
public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _player;
    private float     _bonusPossessionTime; 

    public static event System.Action<bool, EnemyController> OnPossessionChanged; 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go) _player = go.transform;
        else Debug.LogWarning("[PossessionSystem] No object tagged 'Player' found.");
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

    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released   = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        released.SetState(EnemyController.EnemyState.Chasing);
        
        OnPossessionChanged?.Invoke(false, released); //
    }

    public void MovePossessedEnemy(Vector2 cardinalDir)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.GetComponent<EnemyAI>()?.StepInDirection(cardinalDir);
    }

    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        target.stats.possessionDuration += _bonusPossessionTime;
        target.SetState(EnemyController.EnemyState.Possessed);
        _player?.GetComponent<PlayerController>()?.OnPossessionStart(target); 
        OnPossessionChanged?.Invoke(true, target); 
    }
}