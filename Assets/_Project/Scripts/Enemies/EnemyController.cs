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
    public float      tileSize = 0.5f;

    [Header("Arrow")]
    [Tooltip("Drag your Arrow prefab here on the Archer prefab.")]
    public GameObject arrowPrefab;

    [Header("VFX")]
    public GameObject deathVFXPrefab;
    public GameObject possessedIndicatorPrefab;

    public EnemyState CurrentState     { get; private set; } = EnemyState.Idle;
    public float      CurrentHP        { get; private set; }
    public bool       IsPossessed      => CurrentState == EnemyState.Possessed;
    public bool       IsDead           => CurrentState == EnemyState.Dead;
    public bool       AttackTileActive { get; private set; }

    private EnemyAI       _ai;
    private EnemyAnimator _anim;
    private float         _attackCooldownTimer;
    private float         _possessionTimer;
    private GameObject    _possessedIndicator;
    private Collider2D    _col;

    public static event System.Action<EnemyController> OnEnemyDied;
    public static event System.Action<EnemyController> OnPossessionStart;
    public static event System.Action<EnemyController> OnPossessionEnd;

    private void Awake()
    {
        _ai   = GetComponent<EnemyAI>();
        _col  = GetComponent<Collider2D>();
        _anim = GetComponentInChildren<EnemyAnimator>();
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
                _anim?.PlayIdle();
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
            case EnemyState.Blocking:
                _ai.StopMovement();
                _anim?.PlayBlock();
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

    // ── Sword: 3-tile wide arc ────────────────────────────────────────────
    private IEnumerator SwordAttack()
    {
        _anim?.PlayAttack();

        Vector2 fwd  = _ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x);

        Vector3 centerTile = transform.position + (Vector3)(fwd  * tileSize);
        Vector3 leftTile   = centerTile          + (Vector3)(perp * tileSize);
        Vector3 rightTile  = centerTile          - (Vector3)(perp * tileSize);

        var tc = SpawnAttackTile(centerTile, tileSize);
        var tl = SpawnAttackTile(leftTile,   tileSize);
        var tr = SpawnAttackTile(rightTile,  tileSize);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.4f);

        foreach (var pos in new[] { centerTile, leftTile, rightTile })
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * tileSize * 0.85f, 0f);
            foreach (var h in hits)
                h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan
        }

        yield return new WaitForSeconds(0.2f);
        if (tc) Destroy(tc);
        if (tl) Destroy(tl);
        if (tr) Destroy(tr);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    // ── Archer: fires arrow prefab along facing axis ──────────────────────
    private IEnumerator ArcherAttack()
    {
        _anim?.PlayAttack();
        yield return new WaitForSeconds(0.35f);

        if (arrowPrefab != null)
        {
            // Instantiate the actual Arrow prefab — sprite comes with it
            GameObject arrowGO = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            var proj = arrowGO.GetComponent<ArrowProjectile>();

            if (proj == null)
                proj = arrowGO.AddComponent<ArrowProjectile>();

            proj.Init(_ai.FacingDirection, stats.arrowSpeed, stats.attackDamage, gameObject);
        }
        else
        {
            // Fallback: plain white arrow if no prefab assigned (editor warning)
            Debug.LogWarning($"{gameObject.name}: arrowPrefab not assigned on EnemyController. " +
                             "Drag your Arrow prefab into the 'Arrow Prefab' field on the Archer prefab.");
            GameObject arrowGO = new GameObject("Arrow_fallback");
            arrowGO.transform.position = transform.position;
            var sr = arrowGO.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            var proj = arrowGO.AddComponent<ArrowProjectile>();
            proj.Init(_ai.FacingDirection, stats.arrowSpeed, stats.attackDamage, gameObject);
        }

        yield return new WaitForSeconds(0.15f);
        _anim?.PlayIdle();
    }

    // ── Tank bash ─────────────────────────────────────────────────────────
    private IEnumerator TankAttack()
    {
        _anim?.PlayAttack();
        Vector2 bashDir  = _ai.FacingDirection;
        float   elapsed  = 0f;
        float   bashTime = 0.3f;
        float   speed    = (stats.bashDistance * tileSize) / bashTime;

        var bashTile     = SpawnAttackTile(transform.position + (Vector3)(bashDir * tileSize), tileSize);
        AttackTileActive = true;

        var rb = GetComponent<Rigidbody2D>();
        while (elapsed < bashTime)
        {
            rb.MovePosition(rb.position + bashDir * speed * Time.fixedDeltaTime);
            elapsed += Time.deltaTime;
            var hit = Physics2D.OverlapCircle(transform.position, tileSize * 0.4f,
                          LayerMask.GetMask("Player"));
            hit?.GetComponent<PlayerController>()?.TakeDamage(stats.bashDamage); // Gagan
            yield return null;
        }

        if (bashTile) Destroy(bashTile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    // ── Mage meteor ───────────────────────────────────────────────────────
    private IEnumerator MageAttack()
    {
        _anim?.PlayAttack();
        var player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        Vector3 targetPos = SnapToTile(player.transform.position);
        var meteorTile    = SpawnAttackTile(targetPos, stats.meteorRadius * tileSize);
        AttackTileActive  = true;
        yield return new WaitForSeconds(stats.meteorDelay);

        var hits = Physics2D.OverlapCircleAll(targetPos, stats.meteorRadius * tileSize);
        foreach (var h in hits)
            h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan

        if (meteorTile) Destroy(meteorTile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        _anim?.PlayHit();
        StartCoroutine(ResumeAfterHit());
        if (CurrentHP <= 0f) SetState(EnemyState.Dead);
    }

    private IEnumerator ResumeAfterHit()
    {
        yield return new WaitForSeconds(0.25f);
        if (!IsDead) _anim?.PlayIdle();
    }

    private void HandleDeath()
    {
        _ai.StopMovement();
        _col.enabled = false;
        _anim?.PlayDeath();
        if (deathVFXPrefab) Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        OnEnemyDied?.Invoke(this); // Charan + Gagan + Weihan
        StartCoroutine(DelayedDestroy(0.8f));
    }

    private IEnumerator DelayedDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private Vector3 SnapToTile(Vector3 pos) =>
        new Vector3(Mathf.Round(pos.x / tileSize) * tileSize,
                    Mathf.Round(pos.y / tileSize) * tileSize, pos.z);

    private GameObject SpawnAttackTile(Vector3 pos, float size)
    {
        if (attackTilePrefab == null) return null;
        var tile = Instantiate(attackTilePrefab, pos, Quaternion.identity);
        tile.transform.localScale = Vector3.one * size;
        return tile;
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

// ── Arrow projectile ──────────────────────────────────────────────────────
// Add this component to your Arrow prefab, OR it gets added automatically.
// The prefab's own SpriteRenderer provides the visual.
public class ArrowProjectile : MonoBehaviour
{
    private Vector2    _dir;
    private float      _speed;
    private float      _damage;
    private GameObject _owner;
    private bool       _initialised;

    public void Init(Vector2 direction, float speed, float damage, GameObject owner)
    {
        _dir         = direction.normalized;
        _speed       = speed;
        _damage      = damage;
        _owner       = owner;
        _initialised = true;

        // Rotate the arrow sprite to face its direction
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // Only add collider if the prefab doesn't already have one
        if (GetComponent<Collider2D>() == null)
        {
            var col       = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.1f;
        }
        else
        {
            // Make sure whatever collider is on the prefab is a trigger
            GetComponent<Collider2D>().isTrigger = true;
        }

        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        if (!_initialised) return;
        transform.Translate(Vector2.right * _speed * Time.deltaTime); // local right = facing direction
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_initialised) return;
        if (other.gameObject == _owner) return;
        if (other.isTrigger) return; // ignore attack tiles and other triggers
        other.GetComponent<PlayerController>()?.TakeDamage(_damage); // Gagan
        Destroy(gameObject);
    }
}
