using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    public enum EnemyState { Idle, Roaming, Chasing, Attacking, Blocking, Possessed, Stunned, Dead }

    [Header("Data")]
    public EnemyStats stats;

    [Header("Attack Tile")]
    public GameObject attackTilePrefab;

    [Header("VFX")]
    public GameObject deathVFXPrefab;
    public GameObject possessedIndicatorPrefab;

    [Header("Animation")]
    public Animator animator;

    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
    public float      CurrentHP    { get; private set; }
    public bool       IsPossessed  => CurrentState == EnemyState.Possessed;
    public bool       IsDead       => CurrentState == EnemyState.Dead;
    public bool       AttackTileActive { get; private set; }

    private EnemyAI    _ai;
    private float      _attackCooldownTimer;
    private float      _possessionTimer;
    private GameObject _activeTile;
    private GameObject _possessedIndicator;
    private Collider2D _col;

    public static event System.Action<EnemyController> OnEnemyDied;       // Charan + Gagan (UIManager)
    public static event System.Action<EnemyController> OnPossessionStart; // Gagan (UIManager)
    public static event System.Action<EnemyController> OnPossessionEnd;   // Gagan (UIManager)

    private void Awake()
    {
        _ai       = GetComponent<EnemyAI>();
        _col      = GetComponent<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

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
            _possessionTimer -= Time.deltaTime;
            if (_possessionTimer <= 0f)
                PossessionSystem.Instance.EndPossession();
        }
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
                PlayAnim("Idle");
                break;
            case EnemyState.Roaming:
                _ai.BeginRoam();
                PlayAnim("Walk");
                break;
            case EnemyState.Chasing:
                _ai.BeginChase();
                PlayAnim("Walk");
                break;
            case EnemyState.Attacking:
                StartCoroutine(PerformAttack());
                break;
            case EnemyState.Blocking:
                _ai.StopMovement();
                PlayAnim("Block");
                break;
            case EnemyState.Possessed:
                _ai.StopMovement();
                _possessionTimer = stats.possessionDuration;
                if (possessedIndicatorPrefab)
                    _possessedIndicator = Instantiate(possessedIndicatorPrefab, transform.position,
                                                      Quaternion.identity, transform);
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
            if (_possessedIndicator) Destroy(_possessedIndicator);
            OnPossessionEnd?.Invoke(this);
        }
        if (state == EnemyState.Blocking)
            PlayAnim("Idle");
    }

    public void TriggerAttack()
    {
        if (_attackCooldownTimer > 0f || CurrentState == EnemyState.Attacking) return;
        SetState(EnemyState.Attacking);
    }

    public void TriggerBlock()
    {
        if (stats.enemyType != EnemyStats.EnemyType.Swordsman) return;
        SetState(EnemyState.Blocking);
    }

    private IEnumerator PerformAttack()
    {
        _attackCooldownTimer = stats.attackCooldown;

        switch (stats.enemyType)
        {
            case EnemyStats.EnemyType.Warrior:
            case EnemyStats.EnemyType.Swordsman:
                yield return StartCoroutine(SwordAttack());
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

    private IEnumerator SwordAttack()
    {
        PlayAnim("Attack");
        _activeTile = SpawnAttackTile(transform.position + transform.up * (stats.swordArcRadius * 0.5f),
                                      stats.swordArcRadius);
        AttackTileActive = true;
        yield return new WaitForSeconds(0.35f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, stats.swordArcRadius);
        foreach (var h in hits)
        {
            if (!IsInFrontArc(h.transform.position, stats.swordArcAngle)) continue;
            h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan
        }

        yield return new WaitForSeconds(0.2f);
        DestroyActiveTile();
        AttackTileActive = false;
    }

    private IEnumerator ArcherAttack()
    {
        PlayAnim("Attack");
        yield return new WaitForSeconds(0.3f);

        GameObject arrow = new GameObject("Arrow");
        arrow.transform.position = transform.position;
        var proj = arrow.AddComponent<ArrowProjectile>();
        proj.Init(transform.up, stats.arrowSpeed, stats.attackDamage, gameObject);
        yield return null;
    }

    private IEnumerator TankAttack()
    {
        PlayAnim("Attack");
        Vector2 bashDir  = transform.up;
        float   elapsed  = 0f;
        float   bashTime = 0.3f;
        float   speed    = stats.bashDistance / bashTime;

        _activeTile = SpawnAttackTile(
            transform.position + (Vector3)(bashDir * stats.bashDistance * 0.5f), 1.2f);
        AttackTileActive = true;

        var rb = GetComponent<Rigidbody2D>();
        while (elapsed < bashTime)
        {
            rb.MovePosition(rb.position + bashDir * speed * Time.fixedDeltaTime);
            elapsed += Time.deltaTime;

            Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.8f,
                                 LayerMask.GetMask("Player"));
            hit?.GetComponent<PlayerController>()?.TakeDamage(stats.bashDamage); // Gagan
            yield return null;
        }

        DestroyActiveTile();
        AttackTileActive = false;
    }

    private IEnumerator MageAttack()
    {
        PlayAnim("Attack");
        var player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        Vector3 targetPos = player.transform.position;
        _activeTile      = SpawnAttackTile(targetPos, stats.meteorRadius);
        AttackTileActive  = true;
        yield return new WaitForSeconds(stats.meteorDelay);

        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, stats.meteorRadius);
        foreach (var h in hits)
            h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan

        DestroyActiveTile();
        AttackTileActive = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        PlayAnim("Hit");
        if (CurrentHP <= 0f) SetState(EnemyState.Dead);
    }

    private void HandleDeath()
    {
        _ai.StopMovement();
        _col.enabled = false;
        PlayAnim("Death");
        if (deathVFXPrefab) Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        OnEnemyDied?.Invoke(this); // Charan (XP/gold) + Gagan (HUD) + Weihan (spawn records)
        StartCoroutine(DelayedDestroy(0.8f));
    }

    private IEnumerator DelayedDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private bool IsInFrontArc(Vector3 targetPos, float halfAngle)
    {
        Vector3 toTarget = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.up, toTarget) <= halfAngle;
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

    private void PlayAnim(string stateName)
    {
        if (animator) animator.Play(stateName);
    }

    public EnemyStats.EnemyType GetEnemyType() => stats.enemyType;

    public bool UsePossessedAbility()
    {
        if (_attackCooldownTimer > 0f) return false;
        SetState(EnemyState.Attacking);
        return true;
    }

    public void UsePossessedBlock()
    {
        if (stats.enemyType == EnemyStats.EnemyType.Swordsman)
            TriggerBlock();
    }
}

public class ArrowProjectile : MonoBehaviour
{
    private Vector2    _dir;
    private float      _speed;
    private float      _damage;
    private GameObject _owner;

    public void Init(Vector2 direction, float speed, float damage, GameObject owner)
    {
        _dir    = direction.normalized;
        _speed  = speed;
        _damage = damage;
        _owner  = owner;

        var sr  = gameObject.AddComponent<SpriteRenderer>();
        // Assign Arrow sprite in prefab instead if available
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.15f;

        Destroy(gameObject, 4f);
    }

    private void Update() => transform.Translate(_dir * _speed * Time.deltaTime, Space.World);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == _owner) return;
        other.GetComponent<PlayerController>()?.TakeDamage(_damage); // Gagan
        Destroy(gameObject);
    }
}
