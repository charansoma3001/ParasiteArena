using UnityEngine;

public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    [Header("Detection")]
    public float     possessionRadius  = 2.5f;
    public float     backApproachAngle = 60f;
    public LayerMask enemyLayer;

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _playerTransform;
    private bool      _inPerfectDodgeWindow;
    private float     _dodgeWindowTimer;
    private float     _bonusPossessionTime; // Charan - ItemSystem (relics)

    public static event System.Action<bool, EnemyController> OnPossessionChanged; // Gagan - UIManager

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player"); // Gagan - PlayerController
        if (playerGO) _playerTransform = playerGO.transform;
    }

    private void Update()
    {
        if (!_inPerfectDodgeWindow) return;
        _dodgeWindowTimer -= Time.deltaTime;
        if (_dodgeWindowTimer <= 0f) _inPerfectDodgeWindow = false;
    }

    // Called by Gagan - InputHandler on Possess key press
    public bool TryPossess()
    {
        if (IsPossessing || _playerTransform == null) return false;
        var target = FindBestTarget();
        if (target == null) return false;
        BeginPossession(target);
        return true;
    }

    // Called by Gagan - PlayerController on confirmed perfect dodge
    public void NotifyPerfectDodge()
    {
        var nearest = FindNearestEnemy();
        _dodgeWindowTimer     = nearest != null ? nearest.stats.perfectDodgePossessionWindow : 1.2f;
        _inPerfectDodgeWindow = true;
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

    // Called every FixedUpdate by Gagan - InputHandler (movement while possessed)
    public void MovePossessedEnemy(Vector2 input)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        var rb    = PossessedEnemy.GetComponent<Rigidbody2D>();
        float spd = PossessedEnemy.stats.moveSpeed;
        rb.MovePosition(rb.position + input.normalized * spd * Time.fixedDeltaTime);
        if (input != Vector2.zero) PossessedEnemy.transform.up = input;
    }

    // Called by EnemyController when possession timer expires, or by Gagan - InputHandler (cancel key)
    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        released.SetState(EnemyController.EnemyState.Chasing);
        OnPossessionChanged?.Invoke(false, released); // Gagan - UIManager
    }

    // Charan - ItemSystem: relic that extends possession time
    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        target.stats.possessionDuration += _bonusPossessionTime;
        target.SetState(EnemyController.EnemyState.Possessed);

        // Tell PlayerController to hide/freeze the player body while possessing
        _playerTransform.GetComponent<PlayerController>()?.OnPossessionStart(target); // Gagan

        OnPossessionChanged?.Invoke(true, target); // Gagan - UIManager
    }

    private EnemyController FindBestTarget()
    {
        var nearby = Physics2D.OverlapCircleAll(_playerTransform.position, possessionRadius, enemyLayer);
        EnemyController backPick  = null;
        EnemyController frontPick = null;
        float closestBack  = float.MaxValue;
        float closestFront = float.MaxValue;

        foreach (var col in nearby)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead || ec.IsPossessed || ec.stats.isBoss) continue;
            float dist = Vector2.Distance(_playerTransform.position, ec.transform.position);

            if (IsApproachingFromBack(ec))
            {
                if (dist < closestBack) { closestBack = dist; backPick = ec; }
            }
            else if (_inPerfectDodgeWindow)
            {
                if (dist < closestFront) { closestFront = dist; frontPick = ec; }
            }
        }
        return backPick ?? frontPick;
    }

    private EnemyController FindNearestEnemy()
    {
        var nearby = Physics2D.OverlapCircleAll(_playerTransform.position, possessionRadius, enemyLayer);
        EnemyController nearest = null;
        float minDist = float.MaxValue;
        foreach (var col in nearby)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead) continue;
            float d = Vector2.Distance(_playerTransform.position, ec.transform.position);
            if (d < minDist) { minDist = d; nearest = ec; }
        }
        return nearest;
    }

    private bool IsApproachingFromBack(EnemyController ec)
    {
        Vector3 toPlayer  = (_playerTransform.position - ec.transform.position).normalized;
        float   angle     = Vector3.Angle(-ec.transform.up, toPlayer);
        return angle <= backApproachAngle * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_playerTransform.position, possessionRadius);
    }
}
