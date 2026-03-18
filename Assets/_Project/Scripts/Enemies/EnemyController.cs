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
    [Tooltip("World-space size of one tile in your scene. Attack tile will match this.")]
    public float tileWorldSize = 1f;

    [Header("VFX")]
    public GameObject deathVFXPrefab;
    public GameObject possessedIndicatorPrefab;

    [Header("Animation")]
    [Tooltip("Drag the child GameObject that has the Animator on it.")]
    public Animator animator;

    public EnemyState CurrentState   { get; private set; } = EnemyState.Idle;
    public float      CurrentHP      { get; private set; }
    public bool       IsPossessed    => CurrentState == EnemyState.Possessed;
    public bool       IsDead         => CurrentState == EnemyState.Dead;
    public bool       AttackTileActive { get; private set; }

    private EnemyAI    _ai;
    private float      _attackCooldownTimer;
    private float      _possessionTimer;
    private GameObject _activeTile;
    private GameObject _possessedIndicator;
    private Collider2D _col;

    public static event System.Action<EnemyController> OnEnemyDied;
    public static event System.Action<EnemyController> OnPossessionStart;
    public static event System.Action<EnemyController> OnPossessionEnd;

    private void Awake()
    {
        _ai  = GetComponent<EnemyAI>();
        _col = GetComponent<Collider2D>();

        // If animator wasn't assigned in Inspector, search children only (never the root)
        if (animator == null)
            animator = GetComponentInChildren<Animator>(includeInactive: true);
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
                    _possessedIndicator = Instantiate(possessedIndicatorPrefab,
                                                      transform.position, Quaternion.identity, transform);
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

        // Tile appears one tile-length in front of the enemy, sized to exactly one tile
        Vector3 tilePos = transform.position + transform.up * tileWorldSize;
        _activeTile      = SpawnAttackTile(tilePos, tileWorldSize);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.35f);

        // Damage check — single tile in front, use a box the size of one tile
        Collider2D[] hits = Physics2D.OverlapBoxAll(tilePos, Vector2.one * tileWorldSize, 0f);
        foreach (var h in hits)
            h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan

        yield return new WaitForSeconds(0.3f);
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

        _activeTile      = SpawnAttackTile(transform.position + (Vector3)(bashDir * tileWorldSize), tileWorldSize);
        AttackTileActive = true;

        var rb = GetComponent<Rigidbody2D>();
        while (elapsed < bashTime)
        {
            rb.MovePosition(rb.position + bashDir * speed * Time.fixedDeltaTime);
            elapsed += Time.deltaTime;
            Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.8f, LayerMask.GetMask("Player"));
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
        _activeTile       = SpawnAttackTile(targetPos, stats.meteorRadius);
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

    private GameObject SpawnAttackTile(Vector3 pos, float size)
    {
        if (attackTilePrefab == null) return null;
        var tile = Instantiate(attackTilePrefab, pos, Quaternion.identity);
        tile.transform.localScale = Vector3.one * size;
        return tile;
    }

    private void DestroyActiveTile()
    {
        if (_activeTile) Destroy(_activeTile);
        _activeTile = null;
    }

    // Safe: checks animator exists AND the state exists on layer 0 before calling Play
    private void PlayAnim(string stateName)
    {
        if (animator == null) return;
        if (animator.HasState(0, Animator.StringToHash(stateName)))
            animator.Play(stateName, 0);
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
        if (stats.enemyType == EnemyStats.EnemyType.Swordsman) TriggerBlock();
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

        var col       = gameObject.AddComponent<CircleCollider2D>();
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
