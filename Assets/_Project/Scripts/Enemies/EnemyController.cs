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

    [Header("Arrow (Archer only)")]
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
    private float         _atkCooldown;
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
        CurrentHP        = stats.maxHealth;
        _atkCooldown     = 0f;
        _possessionTimer = 0f;
        AttackTileActive = false;
        SetState(EnemyState.Idle);
    }

    private void Update()
    {
        if (CurrentState == EnemyState.Dead) return;
        _atkCooldown -= Time.deltaTime;

        if (CurrentState == EnemyState.Possessed)
        {
            _possessionTimer -= Time.deltaTime;
            if (_possessionTimer <= 0f)
                PossessionSystem.Instance.EndPossession();
        }
    }

    public void SetState(EnemyState next)
    {
        if (CurrentState == next) return;
        ExitState(CurrentState);
        CurrentState = next;
        EnterState(next);
    }

    private void EnterState(EnemyState s)
    {
        switch (s)
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

    private void ExitState(EnemyState s)
    {
        if (s == EnemyState.Possessed)
        {
            if (_possessedIndicator) Destroy(_possessedIndicator);
            OnPossessionEnd?.Invoke(this);
        }
    }

    public void TriggerAttack()
    {
        if (_atkCooldown > 0f || CurrentState == EnemyState.Attacking) return;
        SetState(EnemyState.Attacking);
    }

    public void TriggerBlock()
    {
        if (stats.enemyType != EnemyStats.EnemyType.Swordsman) return;
        SetState(EnemyState.Blocking);
    }

    private IEnumerator PerformAttack()
    {
        _atkCooldown = stats.attackCooldown;
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

    // ── Sword: 3 tiles in a T-shape — centre-front, left-front, right-front ──
    private IEnumerator SwordAttack()
    {
        _anim?.PlayAttack();

        Vector2 fwd  = _ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x); // 90° rotation

        Vector3 centre = transform.position + (Vector3)(fwd  * tileSize);
        Vector3 left   = centre             + (Vector3)(perp * tileSize);
        Vector3 right  = centre             - (Vector3)(perp * tileSize);

        var tc = SpawnTile(centre, tileSize);
        var tl = SpawnTile(left,   tileSize);
        var tr = SpawnTile(right,  tileSize);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.4f);

        foreach (var pos in new[] { centre, left, right })
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

    // ── Archer: instantiates Arrow prefab along the facing cardinal axis ──
    private IEnumerator ArcherAttack()
    {
        _anim?.PlayAttack();
        yield return new WaitForSeconds(0.35f); // draw telegraph

        if (arrowPrefab != null)
        {
            var go   = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<ArrowProjectile>() ?? go.AddComponent<ArrowProjectile>();
            proj.Init(_ai.FacingDirection, stats.arrowSpeed, stats.attackDamage, gameObject);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] arrowPrefab not assigned on EnemyController. " +
                             "Drag your Arrow prefab into the 'Arrow Prefab' slot.");
            // Fallback: invisible arrow still deals damage
            var go   = new GameObject("Arrow_fallback");
            go.transform.position = transform.position;
            var proj = go.AddComponent<ArrowProjectile>();
            proj.Init(_ai.FacingDirection, stats.arrowSpeed, stats.attackDamage, gameObject);
        }

        yield return new WaitForSeconds(0.15f);
        _anim?.PlayIdle();
    }

    // ── Tank bash ─────────────────────────────────────────────────────────
    private IEnumerator TankAttack()
    {
        _anim?.PlayAttack();
        Vector2 dir      = _ai.FacingDirection;
        float   elapsed  = 0f;
        float   bashTime = 0.3f;
        float   spd      = (stats.bashDistance * tileSize) / bashTime;

        var tile = SpawnTile(transform.position + (Vector3)(dir * tileSize), tileSize);
        AttackTileActive = true;

        var rb = GetComponent<Rigidbody2D>();
        while (elapsed < bashTime)
        {
            rb.MovePosition(rb.position + dir * spd * Time.fixedDeltaTime);
            elapsed += Time.deltaTime;
            var h = Physics2D.OverlapCircle(transform.position, tileSize * 0.4f, LayerMask.GetMask("Player"));
            h?.GetComponent<PlayerController>()?.TakeDamage(stats.bashDamage); // Gagan
            yield return null;
        }

        if (tile) Destroy(tile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    // ── Mage meteor ───────────────────────────────────────────────────────
    private IEnumerator MageAttack()
    {
        _anim?.PlayAttack();
        var player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        Vector3 target = SnapToTile(player.transform.position);
        var tile = SpawnTile(target, stats.meteorRadius * tileSize);
        AttackTileActive = true;
        yield return new WaitForSeconds(stats.meteorDelay);

        var hits = Physics2D.OverlapCircleAll(target, stats.meteorRadius * tileSize);
        foreach (var h in hits)
            h.GetComponent<PlayerController>()?.TakeDamage(stats.attackDamage); // Gagan

        if (tile) Destroy(tile);
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
        OnEnemyDied?.Invoke(this); // Charan (XP/gold) + Gagan (HUD) + Weihan (spawn records)
        StartCoroutine(DelayedDestroy(0.8f));
    }

    private IEnumerator DelayedDestroy(float d) { yield return new WaitForSeconds(d); Destroy(gameObject); }

    private Vector3 SnapToTile(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize,
                    Mathf.Round(p.y / tileSize) * tileSize, p.z);

    private GameObject SpawnTile(Vector3 pos, float size)
    {
        if (!attackTilePrefab) return null;
        var t = Instantiate(attackTilePrefab, pos, Quaternion.identity);
        t.transform.localScale = Vector3.one * size;
        return t;
    }

    public EnemyStats.EnemyType GetEnemyType() => stats.enemyType;

    public bool UsePossessedAbility()
    {
        if (_atkCooldown > 0f) return false;
        SetState(EnemyState.Attacking);
        return true;
    }

    public void UsePossessedBlock()
    {
        if (stats.enemyType == EnemyStats.EnemyType.Swordsman) TriggerBlock();
    }
}

// ── Arrow projectile — attach to your Arrow prefab, or it gets added automatically ──
public class ArrowProjectile : MonoBehaviour
{
    private Vector2    _dir;
    private float      _speed;
    private float      _damage;
    private GameObject _owner;
    private bool       _ready;

    public void Init(Vector2 dir, float speed, float damage, GameObject owner)
    {
        _dir    = dir.normalized;
        _speed  = speed;
        _damage = damage;
        _owner  = owner;
        _ready  = true;

        // Rotate sprite to face travel direction
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // Ensure trigger collider exists (prefab may already have one)
        if (!GetComponent<Collider2D>())
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius    = 0.1f;
        }
        else
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        if (!_ready) return;
        // Move along local right (= facing direction after rotation above)
        transform.Translate(Vector2.right * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_ready) return;
        if (other.gameObject == _owner) return;
        if (other.isTrigger) return; // skip attack tiles and other triggers
        other.GetComponent<PlayerController>()?.TakeDamage(_damage); // Gagan
        Destroy(gameObject);
    }
}
