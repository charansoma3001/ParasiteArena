using System.Collections;
using UnityEngine;


[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Roaming,
        Chasing,
        Attacking,
        Possessed,
        Stunned,
        Dead
    }

 
    [Header("Data")]
    public EnemyStats stats;

    [Header("Attack Tile")]
    [Tooltip("Prefab for the danger-tile visualisation shown before/during an attack.")]
    public GameObject attackTilePrefab;

    [Header("VFX / SFX")]
    public GameObject deathVFXPrefab;
    public GameObject possessedIndicatorPrefab; 


    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
    public float      CurrentHP    { get; private set; }
    public bool       IsPossessed  => CurrentState == EnemyState.Possessed;
    public bool       IsDead       => CurrentState == EnemyState.Dead;

    public bool AttackTileActive   { get; private set; }

    // internal dont touch
    private EnemyAI        _ai;
    private float          _attackCooldownTimer;
    private float          _possessionTimer;
    private GameObject     _activeTile;
    private GameObject     _possessedIndicator;
    private Collider2D     _col;


    public static event System.Action<EnemyController> OnEnemyDied;     // Charan Gagan
    public static event System.Action<EnemyController> OnPossessionStart;
    public static event System.Action<EnemyController> OnPossessionEnd;

   // init
    private void Awake()
    {
        _ai  = GetComponent<EnemyAI>();
        _col = GetComponent<Collider2D>();
    }

    /// called by Weihan
    public void Init(EnemyStats overrideStats = null)
    {
        if (overrideStats != null) stats = overrideStats;
        CurrentHP            = stats.maxHealth;
        _attackCooldownTimer = 0f;
        _possessionTimer     = 0f;
        AttackTileActive     = false;
        SetState(EnemyState.Idle);
    }

    private void Update()
    {
        if (CurrentState == EnemyState.Dead) return;

        _attackCooldownTimer -= Time.deltaTime;

        if (CurrentState == EnemyState.Possessed)
        {
            UpdatePossession();
            return;
        }

        if (CurrentState == EnemyState.Attacking)
            return; 
    }

    public void SetState(EnemyState newState)
    {
        if (CurrentState == newState) return;
        ExitState(CurrentState);
        CurrentState = newState;
        EnterState(newState);
    }

    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                _ai.StopMovement();
                break;

            case EnemyState.Roaming:
                _ai.BeginRoam();
                break;

            case EnemyState.Chasing:
                _ai.BeginChase();
                break;

            case EnemyState.Attacking:
                StartCoroutine(PerformAttack());
                break;

            case EnemyState.Possessed:
                _ai.StopMovement();
                _possessionTimer = stats.possessionDuration;
                SpawnPossessedIndicator();
                OnPossessionStart?.Invoke(this);
                break;

            case EnemyState.Stunned:
                _ai.StopMovement();
                break;

            case EnemyState.Dead:
                HandleDeath();
                break;
        }
    }

    private void ExitState(EnemyState state)
    {
        if (state == EnemyState.Possessed)
        {
            DestroyPossessedIndicator();
            OnPossessionEnd?.Invoke(this);
        }
    }


    public void TriggerAttack()
    {
        if (_attackCooldownTimer > 0f || CurrentState == EnemyState.Attacking) return;
        SetState(EnemyState.Attacking);
    }

    private IEnumerator PerformAttack()
    {
        _attackCooldownTimer = stats.attackCooldown;

        switch (stats.enemyType)
        {
            case EnemyStats.EnemyType.Swordsman:
                yield return StartCoroutine(SwordsmanAttack());
                break;
            case EnemyStats.EnemyType.Archer:
                yield return StartCoroutine(ArcherAttack());
                break;
            case EnemyStats.EnemyType.Tank:
                yield return StartCoroutine(TankAttack());
                break;
            case EnemyStats.EnemyType.Mage:
                yield return StartCoroutine(MageAttack());
                break;
        }

        SetState(_ai.GetResumeState());
    }

    // Swordsman: half-circle AoE in front
    private IEnumerator SwordsmanAttack()
    {
        _activeTile = SpawnAttackTile(transform.position, stats.swordArcRadius);
        AttackTileActive = true;
        yield return new WaitForSeconds(0.35f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, stats.swordArcRadius);
        foreach (var hit in hits)
        {
            if (!IsInFrontArc(hit.transform.position, stats.swordArcAngle)) continue;
            hit.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage);
        }

        yield return new WaitForSeconds(0.2f);
        DestroyActiveTile();
        AttackTileActive = false;
    }

    // Archer: fire arrow forward
    private IEnumerator ArcherAttack()
    {
        yield return new WaitForSeconds(0.3f); // draw animation
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.position = transform.position;
        var proj = arrow.AddComponent<ArrowProjectile>();
        proj.Init(transform.up, stats.arrowSpeed, stats.attackDamage, gameObject);
        yield return null;
    }

    // Tank: shield bash lunge
    private IEnumerator TankAttack()
    {
        Vector2 bashDir    = transform.up;
        float   elapsed    = 0f;
        float   bashTime   = 0.3f;
        float   speed      = stats.bashDistance / bashTime;

        _activeTile = SpawnAttackTile(transform.position + (Vector3)(bashDir * stats.bashDistance * 0.5f),
                                      1.2f);
        AttackTileActive = true;

        while (elapsed < bashTime)
        {
            transform.Translate(bashDir * speed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;

            Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.8f,
                                LayerMask.GetMask("Player"));
            hit?.GetComponent<PlayerController>()?.TakeDamage(stats.bashDamage);
            yield return null;
        }

        DestroyActiveTile();
        AttackTileActive = false;
    }

    // Mage: delayed meteor
    private IEnumerator MageAttack()
    {
        // current player position
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        Vector3 targetPos = player.transform.position;

        // warning tile
        _activeTile = SpawnAttackTile(targetPos, stats.meteorRadius);
        AttackTileActive = true;

        yield return new WaitForSeconds(stats.meteorDelay); 

        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, stats.meteorRadius);
        foreach (var hit in hits)
            hit.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage);

        DestroyActiveTile();
        AttackTileActive = false;
    }

    private void UpdatePossession()
    {
        _possessionTimer -= Time.deltaTime;
        if (_possessionTimer <= 0f)
            PossessionSystem.Instance.EndPossession();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (CurrentHP <= 0f) SetState(EnemyState.Dead);
    }

    private void HandleDeath()
    {
        _ai.StopMovement();
        _col.enabled = false;

    
        if (deathVFXPrefab)
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);

        // Charan grants XP/gold and Gagan updates HUD
        OnEnemyDied?.Invoke(this);

        //  destroy spawner if it is one so no more enemies come from it
        if (stats.enemyType == EnemyStats.EnemyType.Spawner)
            Destroy(gameObject);
        else
            StartCoroutine(DelayedDestroy(0.5f));
    }

    private IEnumerator DelayedDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }


    private bool IsInFrontArc(Vector3 targetPos, float halfAngle)
    {
        Vector3 toTarget = (targetPos - transform.position).normalized;
        float angle      = Vector3.Angle(transform.up, toTarget);
        return angle <= halfAngle;
    }

    private GameObject SpawnAttackTile(Vector3 pos, float radius)
    {
        if (attackTilePrefab == null) return null;
        var tile = Instantiate(attackTilePrefab, pos, Quaternion.identity);
        tile.transform.localScale = Vector3.one * radius * 2f;
        return tile;
    }

    private void DestroyActiveTile()
    {
        if (_activeTile) Destroy(_activeTile);
        _activeTile = null;
    }

    private void SpawnPossessedIndicator()
    {
        if (possessedIndicatorPrefab)
            _possessedIndicator = Instantiate(possessedIndicatorPrefab,
                                              transform.position, Quaternion.identity, transform);
    }

    private void DestroyPossessedIndicator()
    {
        if (_possessedIndicator) Destroy(_possessedIndicator);
    }


    public EnemyStats.EnemyType GetEnemyType() => stats.enemyType;

    // returns false if attack is on cooldown.
    public bool UsePossessedAbility()
    {
        if (_attackCooldownTimer > 0f) return false;
        SetState(EnemyState.Attacking);
        return true;
    }
}

public class ArrowProjectile : MonoBehaviour
{
    private Vector2 _dir;
    private float   _speed;
    private float   _damage;
    private GameObject _owner;
    private float   _lifetime = 4f;

    public void Init(Vector2 direction, float speed, float damage, GameObject owner)
    {
        _dir    = direction.normalized;
        _speed  = speed;
        _damage = damage;
        _owner  = owner;

        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.15f;

        Destroy(gameObject, _lifetime);
    }

    private void Update()
    {
        transform.Translate(_dir * _speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == _owner) return;
        other.GetComponent<PlayerController>()?.TakeDamage(_damage);
        Destroy(gameObject);
    }
}
