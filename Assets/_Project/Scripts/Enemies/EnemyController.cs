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
    public float tileSize = 0.5f;

    [Header("Arrow (Archer only)")]
    public GameObject arrowPrefab;

    [Header("SFX")]
    public AudioClip attackSfx;
    public AudioClip hitSfx;
    public AudioClip deathSfx;

    [Header("VFX")]
    public GameObject possessedIndicatorPrefab;

    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
    public float CurrentHP { get; private set; }
    public bool IsPossessed => CurrentState == EnemyState.Possessed;
    public bool IsDead => CurrentState == EnemyState.Dead;
    public bool AttackTileActive { get; private set; }

    private EnemyAI ai;
    private EnemyAnimator anim;
    private float atkCooldown;
    private float possessionTimer;
    private GameObject possessedIndicator;
    private Collider2D col;

    private readonly List<GameObject> activeTiles = new();

    public static event System.Action<EnemyController> OnEnemyDied;
    public static event System.Action<EnemyController> OnPossessionStart;
    public static event System.Action<EnemyController> OnPossessionEnd;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        col = GetComponent<Collider2D>();
        anim = GetComponentInChildren<EnemyAnimator>();
    }

    private void Start()
    {
        if (stats != null && CurrentHP == 0f)
            Init();
    }

    public void Init(EnemyStats overrideStats = null)
    {
        if (overrideStats != null) stats = overrideStats;
        CurrentHP = stats.maxHealth;
        atkCooldown = 0f;
        possessionTimer = 0f;
        AttackTileActive = false;
        SetState(EnemyState.Idle);
    }

    private void Update()
    {
        if (CurrentState == EnemyState.Dead) return;
        atkCooldown -= Time.deltaTime;

        if (CurrentState == EnemyState.Possessed)
        {
            float decayRate = PossessionSystem.Instance != null ? PossessionSystem.Instance.HostDecayRate : 1f;
            possessionTimer -= Time.deltaTime * decayRate;
            if (possessionTimer <= 0f)
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
                ai.StopMovement();
                anim?.PlayIdle();
                break;

            case EnemyState.Roaming:
                ai.BeginRoam();
                break;

            case EnemyState.Chasing:
                ai.BeginChase();
                break;

            case EnemyState.Attacking:
                StartCoroutine(PerformAttack());
                break;

            case EnemyState.Possessed:
                StopAllCoroutines();
                ClearActiveTiles();
                atkCooldown = 0f;
                ai.StopMovement();
                anim?.PlayIdle();
                possessionTimer = stats.possessionDuration + (PossessionSystem.Instance != null ? PossessionSystem.Instance.BonusPossessionTime : 0f);
                if (possessedIndicatorPrefab)
                {
                    possessedIndicator = Instantiate(possessedIndicatorPrefab,
                                                      transform.position, Quaternion.identity, transform);
                    possessedIndicator.transform.localPosition = Vector3.up * (tileSize * 2.5f);
                }
                OnPossessionStart?.Invoke(this);
                break;

            case EnemyState.Stunned:
                ai.StopMovement();
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
            if (possessedIndicator) Destroy(possessedIndicator);
            OnPossessionEnd?.Invoke(this);
        }
    }

    public void TriggerAttack()
    {
        if (atkCooldown > 0f || CurrentState == EnemyState.Attacking) return;
        SetState(EnemyState.Attacking);
    }

    public void TriggerBlock()
    {
        if (stats.enemyType != EnemyStats.EnemyType.Swordsman) return;
        SetState(EnemyState.Blocking);
    }

    private IEnumerator PerformAttack()
    {
        atkCooldown = stats.attackCooldown;
        yield return StartCoroutine(RunAttackForType(stats.enemyType));
        if (CurrentState == EnemyState.Attacking)
            SetState(ai.GetResumeState());
    }

    public float GetActualAttackDamage(float baseDamage = -1f)
    {
        float dmg = baseDamage < 0f ? stats.attackDamage : baseDamage;
        if (IsPossessed && PossessionSystem.Instance != null)
        {
            dmg += PossessionSystem.Instance.PlayerAttackDamage;
        }
        return dmg;
    }

    public bool UsePossessedAbility()
    {
        if (atkCooldown > 0f) return false;
        StartCoroutine(PossessedAttack());
        return true;
    }

    private IEnumerator PossessedAttack()
    {
        atkCooldown = stats.attackCooldown;
        yield return StartCoroutine(RunAttackForType(stats.enemyType));
    }

    private IEnumerator RunAttackForType(EnemyStats.EnemyType type)
    {
        if (attackSfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXAtPos(attackSfx, transform.position);

        switch (type)
        {
            case EnemyStats.EnemyType.Swordsman:
                yield return StartCoroutine(SwordAttack());
                break;
            case EnemyStats.EnemyType.Archer:
                yield return StartCoroutine(ArcherAttack());
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
        anim?.PlayAttack();
        Vector2 fwd = ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x);

        Vector3 centre = transform.position + (Vector3)(fwd  * tileSize * 1.8f);
        Vector3 left = centre + (Vector3)(perp * tileSize);
        Vector3 right = centre - (Vector3)(perp * tileSize);

        var tc = SpawnTile(centre, tileSize);
        var tl = SpawnTile(left, tileSize);
        var tr = SpawnTile(right, tileSize);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.4f);

        var hitSet = new HashSet<Collider2D>();
        foreach (var pos in new[] { centre, left, right })
            foreach (var h in Physics2D.OverlapBoxAll(pos, Vector2.one * tileSize * 0.85f, 0f))
                hitSet.Add(h);

        float dmg = GetActualAttackDamage();
        foreach (var h in hitSet)
            HitTarget(h, dmg, dmg);

        yield return new WaitForSeconds(0.2f);
        if (tc) Destroy(tc); if (tl) Destroy(tl); if (tr) Destroy(tr);
        AttackTileActive = false;
        anim?.PlayIdle();
    }

    private IEnumerator ArcherAttack()
    {
        anim?.PlayAttack();

        Vector2 fwd = ai.FacingDirection;
        bool wasPossessed = IsPossessed;
        int range = stats.arrowRange > 0 ? stats.arrowRange : 6;

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
            var go = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<ArrowProjectile>() ?? go.AddComponent<ArrowProjectile>();
            proj.Init(fwd, stats.arrowSpeed, GetActualAttackDamage(), GetActualAttackDamage(), gameObject, wasPossessed);
        }
        else
        {
            var go = new GameObject("Arrow_fallback");
            go.transform.position = transform.position;
            var proj = go.AddComponent<ArrowProjectile>();
            proj.Init(fwd, stats.arrowSpeed, GetActualAttackDamage(), GetActualAttackDamage(), gameObject, wasPossessed);
        }

        yield return new WaitForSeconds(0.15f);
        anim?.PlayIdle();
    }

    private IEnumerator BiteAttack()
    {
        anim?.PlayAttack();
        Vector3 bitePos = transform.position + (Vector3)(ai.FacingDirection * tileSize * 1.0f);
        var tile = SpawnTile(bitePos, tileSize * 0.9f);
        AttackTileActive = true;

        yield return new WaitForSeconds(0.2f);

        foreach (var h in Physics2D.OverlapCircleAll(bitePos, tileSize * 0.5f))
            HitTarget(h, GetActualAttackDamage(), GetActualAttackDamage());

        yield return new WaitForSeconds(0.1f);
        if (tile) Destroy(tile);
        AttackTileActive = false;
        anim?.PlayIdle();
    }

    private IEnumerator BossComboAttack()
    {
        anim?.PlayAttack();
        Vector2 fwd = ai.FacingDirection;
        Vector2 perp = new Vector2(-fwd.y, fwd.x);

        yield return new WaitForSeconds(0.25f); 

        var t1 = SpawnTile(transform.position + (Vector3)(fwd * tileSize * 1.5f), tileSize * 1.1f);
        AttackTileActive = true;
        yield return new WaitForSeconds(0.2f);

        float dmg1 = GetActualAttackDamage();
        foreach (var h in Physics2D.OverlapBoxAll(
            transform.position + (Vector3)(fwd * tileSize * 1.5f),
            Vector2.one * tileSize, 0f))
            HitTarget(h, dmg1, dmg1);

        if (t1) Destroy(t1);
        yield return new WaitForSeconds(0.1f);

        var t2 = SpawnTile(transform.position + (Vector3)(fwd   * tileSize), tileSize);
        var t3 = SpawnTile(transform.position + (Vector3)(perp  * tileSize), tileSize);
        var t4 = SpawnTile(transform.position - (Vector3)(perp  * tileSize), tileSize);
        yield return new WaitForSeconds(0.2f);


        var beat2Set = new HashSet<Collider2D>();
        foreach (var p in new[]
        {
            transform.position + (Vector3)(fwd  * tileSize),
            transform.position + (Vector3)(perp * tileSize),
            transform.position - (Vector3)(perp * tileSize),
        })
        {
            foreach (var h in Physics2D.OverlapBoxAll(p, Vector2.one * tileSize * 0.85f, 0f))
                beat2Set.Add(h);
        }

        float dmg2 = GetActualAttackDamage() * 0.75f;
        foreach (var h in beat2Set)
            HitTarget(h, dmg2, dmg2);

        if (t2) Destroy(t2); if (t3) Destroy(t3); if (t4) Destroy(t4);
        AttackTileActive = false;
        anim?.PlayIdle();
    }


    private void HitTarget(Collider2D h, float playerDmg, float enemyDmg)
    {
        if (h == null) return;
        h.GetComponent<PlayerController>()?.TakeDamage(playerDmg);

        var ec = h.GetComponent<EnemyController>();
        if (ec == null || ec == this) return;

        if (IsPossessed && !ec.IsPossessed)
            ec.TakeDamage(enemyDmg);
        else if (!IsPossessed && ec.IsPossessed)
            ec.TakeDamage(enemyDmg);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        
        if (hitSfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXAtPos(hitSfx, transform.position);
            
        anim?.PlayHit();
        StartCoroutine(ResumeAfterHit());
        if (CurrentHP <= 0f) SetState(EnemyState.Dead);
    }

    private IEnumerator ResumeAfterHit()
    {
        yield return new WaitForSeconds(0.25f);
        if (!IsDead && !IsPossessed) anim?.PlayIdle();
    }

    private void HandleDeath()
    {
        if (PossessionSystem.Instance != null &&
            PossessionSystem.Instance.IsPossessing &&
            PossessionSystem.Instance.PossessedEnemy == this)
            PossessionSystem.Instance.EndPossession();

        ai.StopMovement();
        col.enabled = false;
        anim?.PlayDeath();
        if (deathSfx != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXAtPos(deathSfx, transform.position);
            else
                AudioSource.PlayClipAtPoint(deathSfx, transform.position);
        }
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
        activeTiles.Add(t);
        return t;
    }

    private void ClearActiveTiles()
    {
        foreach (var t in activeTiles)
            if (t) Destroy(t); 
        activeTiles.Clear();
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
    private Vector2 direction;
    private float speed;
    private float damage;   
    private float enemyDamage; 
    private GameObject owner;
    private bool ready;
    private bool firedByPossessed;

    public void Init(Vector2 dir, float speed, float damage, GameObject owner)
        => Init(dir, speed, damage, damage, owner, false);

    public void Init(Vector2 dir, float speed, float playerDamage, float enemyDamage, GameObject owner, bool firedByPossessed)
    {
        direction = dir.normalized;
        speed = speed;
        damage = playerDamage;
        enemyDamage = enemyDamage;
        owner = owner;
        ready = true;
        firedByPossessed = firedByPossessed;

        transform.rotation = Quaternion.AngleAxis(
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, Vector3.forward);

        if (!GetComponent<Collider2D>())
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.1f;
        }
        else GetComponent<Collider2D>().isTrigger = true;

        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        if (!ready) return;
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!ready || other.gameObject == owner || other.isTrigger) return;

        bool hit = false;
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) { pc.TakeDamage(damage); hit = true; }

        var ec = other.GetComponent<EnemyController>();
        if (ec != null && ec.gameObject != owner)
        {
            if (ec.IsPossessed || firedByPossessed)
            { ec.TakeDamage(enemyDamage); hit = true; }
        }

        if (hit) Destroy(gameObject);
    }
}