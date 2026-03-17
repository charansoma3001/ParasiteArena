using System.Collections;
using UnityEngine;


// Possession can happen in two ways:
// 1. BACK APPROACH  - player walks behind the enemy (angle check) and presses Possess.
//  2. POST-DODGE     -after a perfect dodge, a time window opens, pressing Possess during it works from any angle.

/// </summary>
///   • Gagan InputHandler calls:
///       PossessionSystem.Instance.TryPossess()
///       PossessionSystem.Instance.NotifyPerfectDodge() 
///       PossessionSystem.Instance.UsePossessedAbility() 
///       PossessionSystem.Instance.MovePossessedEnemy(Vector2) 
///   • Gagan UIManager subscribes to OnPossessionChanged to show/hide the possession timer bar.
///   • Charan StatManager can call SetPossessionBonusTime() to extend possession duration via relics.
/// </summary>
public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

  
    [Header("Detection")]
    [Tooltip("Radius around the player checked for possessable enemies.")]
    public float possessionRadius = 2.5f;

    [Tooltip("Angle (degrees) from the enemy's BACK within which a back-approach counts.")]
    public float backApproachAngle = 60f;

    [Header("Layers")]
    public LayerMask enemyLayer;


    public bool             IsPossessing      { get; private set; }
    public EnemyController  PossessedEnemy    { get; private set; }

    private Transform       _player;
    private Rigidbody2D     _possessedRb;
    private bool            _inPerfectDodgeWindow;
    private float           _dodgeWindowTimer;
    private float           _bonusPossessionTime; 


    public static event System.Action<bool, EnemyController> OnPossessionChanged;

  
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) _player = playerGO.transform;
    }

    private void Update()
    {
        if (_inPerfectDodgeWindow)
        {
            _dodgeWindowTimer -= Time.deltaTime;
            if (_dodgeWindowTimer <= 0f)
                _inPerfectDodgeWindow = false;
        }
    }


    // returns true if successeful
    public bool TryPossess()
    {
        if (IsPossessing || _player == null) return false;

        EnemyController target = FindBestTarget();
        if (target == null) return false;

        BeginPossession(target);
        return true;
    }

    // call when perfect dodge is made please
    public void NotifyPerfectDodge()
    {
        EnemyController nearest = FindNearestEnemy();
        float window = nearest != null
            ? nearest.stats.perfectDodgePossessionWindow
            : 1.2f;

        _inPerfectDodgeWindow = true;
        _dodgeWindowTimer     = window;
    }


    public bool UsePossessedAbility()
    {
        if (!IsPossessing || PossessedEnemy == null) return false;
        return PossessedEnemy.UsePossessedAbility();
    }


    public void MovePossessedEnemy(Vector2 input)
    {
        if (!IsPossessing || _possessedRb == null) return;

        float speed = PossessedEnemy.stats.moveSpeed;
        _possessedRb.MovePosition(_possessedRb.position + input.normalized * speed * Time.fixedDeltaTime);

        if (input != Vector2.zero)
            PossessedEnemy.transform.up = input;
    }

    // you can call this to end the possession early btw
    public void EndPossession()
    {
        if (!IsPossessing) return;

        var released = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        _possessedRb   = null;

        released.SetState(EnemyController.EnemyState.Chasing);

        OnPossessionChanged?.Invoke(false, released);
    }


    public void SetPossessionBonusTime(float bonus)
    {
        _bonusPossessionTime = bonus;
    }

  
    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        _possessedRb   = target.GetComponent<Rigidbody2D>();

        target.stats.possessionDuration += _bonusPossessionTime;

        target.SetState(EnemyController.EnemyState.Possessed);

        _player.GetComponent<PlayerController>()?.OnPossessionStart(target);

        OnPossessionChanged?.Invoke(true, target);
    }


    private EnemyController FindBestTarget()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(_player.position, possessionRadius, enemyLayer);

        EnemyController backCandidate  = null;
        EnemyController frontCandidate = null;
        float closestBack  = float.MaxValue;
        float closestFront = float.MaxValue;

        foreach (var col in nearby)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead || ec.IsPossessed) continue;
            if (ec.stats.isBoss)                           continue; 

            float dist = Vector2.Distance(_player.position, ec.transform.position);

            if (IsApproachingFromBack(ec))
            {
                if (dist < closestBack) { closestBack = dist; backCandidate = ec; }
            }
            else if (_inPerfectDodgeWindow)
            {
                if (dist < closestFront) { closestFront = dist; frontCandidate = ec; }
            }
        }

        return backCandidate ?? frontCandidate;
    }

    private EnemyController FindNearestEnemy()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(_player.position, possessionRadius, enemyLayer);
        EnemyController nearest = null;
        float minDist = float.MaxValue;
        foreach (var col in nearby)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead) continue;
            float d = Vector2.Distance(_player.position, ec.transform.position);
            if (d < minDist) { minDist = d; nearest = ec; }
        }
        return nearest;
    }

    private bool IsApproachingFromBack(EnemyController ec)
    {
        Vector3 toPlayer   = (_player.position - ec.transform.position).normalized;
        Vector3 enemyBack  = -ec.transform.up;
        float angle        = Vector3.Angle(enemyBack, toPlayer);
        return angle <= backApproachAngle * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_player.position, possessionRadius);
    }
}
