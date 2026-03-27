using System.Collections;
using System.Collections.Generic;
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

    private readonly List<GameObject> _activeTiles = new();

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
                PossessionSystem.Instance?.EndPossession();
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
                StopAllCoroutines();
                ClearActiveTiles();
                _atkCooldown = 0f;
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
        yield return StartCoroutine(RunAttackForType(stats.enemyType));
        if (CurrentState == EnemyState.Attacking)
            SetState(_ai.GetResumeState());
    }

  
    public bool UsePossessedAbility()
    {
        if (_atkCooldown > 0f) return false;
        StartCoroutine(PossessedAttack());
        return true;
    }

    private IEnumerator PossessedAttack()
    {
        _atkCooldown = stats.attackCooldown;
        yield return StartCoroutine(RunAttackForType(stats.enemyType));
    }

    private IEnumerator RunAttackForType(EnemyStats.EnemyType type)
    {
        switch (type)
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
            case EnemyStats.EnemyType.Rat:
            case EnemyStats.EnemyType.Chomp:
                yield return StartCoroutine(BiteAttack());
                break;
            case EnemyStats.EnemyType.Boss:
                yield return StartCoroutine(BossComboAttack());
                break;
        }
    }

  

    private IEnumerator SwordAttack()
    {
        _anim?.PlayAttack();
        Vector2 fwd  = _ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x);

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
            foreach (var h in Physics2D.OverlapBoxAll(pos, Vector2.one * tileSize * 0.85f, 0f))
                HitTarget(h, stats.attackDamage);
        }

        yield return new WaitForSeconds(0.2f);
        if (tc) Destroy(tc); if (tl) Destroy(tl); if (tr) Destroy(tr);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    private IEnumerator ArcherAttack()
    {
        _anim?.PlayAttack();

        Vector2 fwd         = _ai.FacingDirection;
        bool    wasPossessed = IsPossessed;
        int     range        = stats.arrowRange > 0 ? stats.arrowRange : 6;

        var preview = new List<GameObject>();
        for (int i = 1; i <= range; i++)
        {
            Vector3 pos = transform.position + (Vector3)(fwd * tileSize * i);
            var t = SpawnTile(pos, tileSize * 0.5f);
            if (t != null) preview.Add(t);
        }

        yield return new WaitForSeconds(0.35f);

        foreach (var t in preview) if (t) Destroy(t);
        preview.Clear();

        if (arrowPrefab != null)
        {
            var go   = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<ArrowProjectile>() ?? go.AddComponent<ArrowProjectile>();
            proj.Init(fwd, stats.arrowSpeed, stats.attackDamage, gameObject, wasPossessed);
        }
        else
        {
            var go   = new GameObject("Arrow_fallback");
            go.transform.position = transform.position;
            var proj = go.AddComponent<ArrowProjectile>();
            proj.Init(fwd, stats.arrowSpeed, stats.attackDamage, gameObject, wasPossessed);
        }

        yield return new WaitForSeconds(0.15f);
        _anim?.PlayIdle();
    }

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
    
            rb.velocity = Vector2.zero;
            rb.MovePosition(rb.position + dir * spd * Time.fixedDeltaTime);
            elapsed += Time.deltaTime;
            foreach (var h in Physics2D.OverlapCircleAll(transform.position, tileSize * 0.4f))
                HitTarget(h, stats.bashDamage);
            yield return null;
        }

        rb.velocity = Vector2.zero; 
        if (tile) Destroy(tile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    private IEnumerator MageAttack()
    {
        _anim?.PlayAttack();
        var player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        Vector3 targetPos = SnapToTile(player.transform.position);
        var tile = SpawnTile(targetPos, stats.meteorRadius * tileSize);
        AttackTileActive = true;
        yield return new WaitForSeconds(stats.meteorDelay);

        foreach (var h in Physics2D.OverlapCircleAll(targetPos, stats.meteorRadius * tileSize))
            HitTarget(h, stats.attackDamage);

        if (tile) Destroy(tile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }

    private IEnumerator BiteAttack()
    {
        _anim?.PlayAttack();
        Vector3 bitePos = transform.position + (Vector3)(_ai.FacingDirection * tileSize * 0.6f);
        var tile = SpawnTile(bitePos, tileSize * 0.9f);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.2f);

        foreach (var h in Physics2D.OverlapCircleAll(bitePos, tileSize * 0.5f))
            HitTarget(h, stats.attackDamage);

        yield return new WaitForSeconds(0.1f);
        if (tile) Destroy(tile);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }


    private IEnumerator BossComboAttack()
    {
        _anim?.PlayAttack();
        Vector2 fwd  = _ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x);

        yield return new WaitForSeconds(0.25f); 

        var t1 = SpawnTile(transform.position + (Vector3)(fwd * tileSize * 1.5f), tileSize * 1.1f);
        AttackTileActive = true;
        yield return new WaitForSeconds(0.2f);

        foreach (var h in Physics2D.OverlapBoxAll(
            transform.position + (Vector3)(fwd * tileSize * 1.5f),
            Vector2.one * tileSize, 0f))
            HitTarget(h, stats.attackDamage);

        if (t1) Destroy(t1);
        yield return new WaitForSeconds(0.1f);

        var t2 = SpawnTile(transform.position + (Vector3)(fwd   * tileSize), tileSize);
        var t3 = SpawnTile(transform.position + (Vector3)(perp  * tileSize), tileSize);
        var t4 = SpawnTile(transform.position - (Vector3)(perp  * tileSize), tileSize);
        yield return new WaitForSeconds(0.2f);

        foreach (var p in new[]
        {
            transform.position + (Vector3)(fwd  * tileSize),
            transform.position + (Vector3)(perp * tileSize),
            transform.position - (Vector3)(perp * tileSize),
        })
        {
            foreach (var h in Physics2D.OverlapBoxAll(p, Vector2.one * tileSize * 0.85f, 0f))
                HitTarget(h, stats.attackDamage * 0.75f);
        }

        if (t2) Destroy(t2); if (t3) Destroy(t3); if (t4) Destroy(t4);
        AttackTileActive = false;
        _anim?.PlayIdle();
    }


    private void HitTarget(Collider2D h, float dmg)
    {
        if (h == null) return;
        h.GetComponent<PlayerController>()?.TakeDamage(dmg);

        var ec = h.GetComponent<EnemyController>();
        if (ec == null || ec == this) return;

        if (IsPossessed && !ec.IsPossessed)
            ec.TakeDamage(dmg);
        else if (!IsPossessed && ec.IsPossessed)
            ec.TakeDamage(dmg);
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
        if (!IsDead && !IsPossessed) _anim?.PlayIdle();
    }

    private void HandleDeath()
    {
        if (PossessionSystem.Instance != null &&
            PossessionSystem.Instance.IsPossessing &&
            PossessionSystem.Instance.PossessedEnemy == this)
            PossessionSystem.Instance.EndPossession();

        _ai.StopMovement();
        _col.enabled = false;
        _anim?.PlayDeath();
        if (deathVFXPrefab) Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        OnEnemyDied?.Invoke(this);
        StartCoroutine(DelayedDestroy(0.8f));
    }

    private IEnumerator DelayedDestroy(float d)
    {
        yield return new WaitForSeconds(d);
        Destroy(gameObject);
    }


    private GameObject SpawnTile(Vector3 pos, float size)
    {
        if (!attackTilePrefab) return null;
        var t = Instantiate(attackTilePrefab, pos, Quaternion.identity);
        t.transform.localScale = Vector3.one * size;
        _activeTiles.Add(t);
        return t;
    }

    private void ClearActiveTiles()
    {
        foreach (var t in _activeTiles)
            if (t) Destroy(t); 
        _activeTiles.Clear();
        AttackTileActive = false;
    }

    private Vector3 SnapToTile(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize,
                    Mathf.Round(p.y / tileSize) * tileSize, p.z);

    public EnemyStats.EnemyType GetEnemyType() => stats.enemyType;

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
    private bool       _ready;
    private bool       _firedByPossessed;

    public void Init(Vector2 dir, float speed, float damage, GameObject owner)
        => Init(dir, speed, damage, owner, false);

    public void Init(Vector2 dir, float speed, float damage, GameObject owner, bool firedByPossessed)
    {
        _dir              = dir.normalized;
        _speed            = speed;
        _damage           = damage;
        _owner            = owner;
        _ready            = true;
        _firedByPossessed = firedByPossessed;

        transform.rotation = Quaternion.AngleAxis(
            Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg, Vector3.forward);

        if (!GetComponent<Collider2D>())
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius    = 0.1f;
        }
        else GetComponent<Collider2D>().isTrigger = true;

        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        if (!_ready) return;
        transform.Translate(Vector2.right * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_ready || other.gameObject == _owner || other.isTrigger) return;

        bool hit = false;
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) { pc.TakeDamage(_damage); hit = true; }

        var ec = other.GetComponent<EnemyController>();
        if (ec != null && ec.gameObject != _owner)
        {
            if (ec.IsPossessed || _firedByPossessed)
            { ec.TakeDamage(_damage); hit = true; }
        }

        if (hit) Destroy(gameObject);
    }
}