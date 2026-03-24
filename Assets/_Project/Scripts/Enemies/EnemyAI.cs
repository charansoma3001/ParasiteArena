using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Tile Movement")]
    public float tileSize     = 0.5f;
    public float stepDuration = 0.18f;
    public float stepCooldown = 0.35f;

    [Header("Archer")]
    public int archerShootRange   = 6;
    public int archerKiteDistance = 3;

    [Header("Pathfinding")]
    public LayerMask obstacleLayer; // Weihan - ObstacleSystem

    private EnemyController _ctrl;
    private Rigidbody2D     _rb;
    private EnemyStats      _stats;
    private Transform       _player;
    private EnemyAnimator   _anim;

    private bool  _isStepping;
    private float _stepCooldownTimer;

    private Vector2 _chompDir;
    private float   _chompBounceTimer;
    private const float ChompBounceInterval = 1.8f;

    private float _spawnTimer;
    private int   _activeSpawnCount;

    public Vector2 FacingDirection { get; private set; } = Vector2.down;
    public bool    IsMoving        => _isStepping;

    private void Awake()
    {
        _ctrl = GetComponent<EnemyController>();
        _rb   = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;
        _rb.gravityScale   = 0f;
        _stats = _ctrl.stats;
        _anim  = GetComponentInChildren<EnemyAnimator>();
        var p = GameObject.FindWithTag("Player"); // Gagan
        if (p) _player = p.transform;
    }

    private void Start()
    {
        SnapToGrid();
        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:
                _chompDir         = CardinalDir(Random.insideUnitCircle);
                _chompBounceTimer = ChompBounceInterval;
                _ctrl.SetState(EnemyController.EnemyState.Roaming);
                break;
            case EnemyStats.EnemyType.Spawner:
                _ctrl.SetState(EnemyController.EnemyState.Idle);
                break;
            default:
                _ctrl.SetState(EnemyController.EnemyState.Idle);
                break;
        }
    }

    private void Update()
    {
        if (_ctrl.IsDead || _ctrl.IsPossessed) return;
        _anim?.TickMovement(_isStepping);
        _stepCooldownTimer -= Time.deltaTime;

        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:   UpdateChomp();   return;
            case EnemyStats.EnemyType.Spawner: UpdateSpawner(); return;
            case EnemyStats.EnemyType.Archer:  UpdateArcher();  return;
        }
        UpdateHunter();
    }

    // PlayerController.Update() sets transform.position = possessed.transform.position
    // every frame, so _player.position always equals the effective target — whether
    // that's the real player or the currently-possessed enemy.
    private Vector3 GetTargetPosition() =>
        _player != null ? _player.position : transform.position;

    // Public so EnemyController.MageAttack can target the correct position
    public Vector3 GetCurrentTarget() => GetTargetPosition();

    // ── Melee hunter (Swordsman / Warrior) ────────────────────────────────
    private void UpdateHunter()
    {
        Vector3 target = GetTargetPosition();
        float   dist   = Vector2.Distance(transform.position, target);

        if (_ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            _ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= _stats.detectionRange)
                _ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        if (_ctrl.CurrentState != EnemyController.EnemyState.Chasing) return;
        if (_isStepping) return;

        float tilesDist = TilesFrom(target);
        if (tilesDist <= _stats.attackRange)
        {
            FacingDirection = CardinalDir(target - transform.position);
            _ctrl.TriggerAttack();
            return;
        }

        if (_stepCooldownTimer > 0f) return;
        Vector2 step = BestStepToward(target);
        if (step != Vector2.zero) StartCoroutine(TakeStep(step));
    }

    // ── Archer ────────────────────────────────────────────────────────────
    private void UpdateArcher()
    {
        Vector3 target = GetTargetPosition();
        float   dist   = Vector2.Distance(transform.position, target);

        if (_ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            _ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= _stats.detectionRange)
                _ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        if (_ctrl.CurrentState != EnemyController.EnemyState.Chasing) return;
        if (_ctrl.CurrentState == EnemyController.EnemyState.Attacking) return;

        float tilesDist = TilesFrom(target);
        bool  aligned   = IsCardinalAligned(target);
        bool  inRange   = tilesDist <= archerShootRange;

        if (aligned && inRange && tilesDist > 0)
        {
            FacingDirection = CardinalDir(target - transform.position);
            _ctrl.TriggerAttack();
            return;
        }

        if (_isStepping || _stepCooldownTimer > 0f) return;

        if (tilesDist < archerKiteDistance)
        {
            Vector2 away = CardinalDir(transform.position - target);
            if (!WouldHitObstacle(away)) { StartCoroutine(TakeStep(away)); return; }
        }

        Vector2 strafe = StrafeToAlign(target);
        if (strafe != Vector2.zero) StartCoroutine(TakeStep(strafe));
    }

    // Snaps both positions to the tile grid before comparing so a player
    // at world X=5.3 and an archer at X=5.0 are correctly seen as column-aligned
    // (both snap to X=5.0 or 5.5). Without snapping the float delta oscillates
    // left-right and the archer never moves vertically.
    private bool IsCardinalAligned(Vector3 target)
    {
        Vector2 mine = SnapPos(transform.position);
        Vector2 tgt  = SnapPos(target);
        return Mathf.Approximately(mine.x, tgt.x) || Mathf.Approximately(mine.y, tgt.y);
    }

    private Vector2 StrafeToAlign(Vector3 target)
    {
        Vector2 mine = SnapPos(transform.position);
        Vector2 tgt  = SnapPos(target);
        float   dx   = tgt.x - mine.x;
        float   dy   = tgt.y - mine.y;

        if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f) return Vector2.zero;

        Vector2 h = new Vector2(Mathf.Sign(dx), 0f);
        Vector2 v = new Vector2(0f, Mathf.Sign(dy));

        Vector2 primary, secondary;
        if      (Mathf.Abs(dx) < 0.01f) { primary = v; secondary = h; }
        else if (Mathf.Abs(dy) < 0.01f) { primary = h; secondary = v; }
        else
        {
            bool colFirst = Mathf.Abs(dx) <= Mathf.Abs(dy);
            primary   = colFirst ? h : v;
            secondary = colFirst ? v : h;
        }

        if (!WouldHitObstacle(primary))   return primary;
        if (!WouldHitObstacle(secondary)) return secondary;
        return Vector2.zero;
    }

    // ── Roaming ───────────────────────────────────────────────────────────
    public void BeginRoam()
    {
        StopAllCoroutines();
        StartCoroutine(RoamCoroutine());
    }

    private IEnumerator RoamCoroutine()
    {
        while (true)
        {
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            Vector2   dir  = dirs[Random.Range(0, dirs.Length)];
            if (!WouldHitObstacle(dir)) yield return StartCoroutine(TakeStep(dir));
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        }
    }

    // ── Chomp ─────────────────────────────────────────────────────────────
    private void UpdateChomp()
    {
        if (_isStepping || _stepCooldownTimer > 0f) return;
        _chompBounceTimer -= Time.deltaTime;
        if (_chompBounceTimer <= 0f || WouldHitObstacle(_chompDir))
        {
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            _chompDir         = dirs[Random.Range(0, dirs.Length)];
            _chompBounceTimer = ChompBounceInterval;
        }
        StartCoroutine(TakeStep(_chompDir));
        if (_player != null && Vector2.Distance(transform.position, _player.position) < tileSize * 0.8f)
            _player.GetComponent<PlayerController>()?.TakeDamage(_stats.attackDamage); // Gagan
    }

    // ── Spawner ───────────────────────────────────────────────────────────
    private void UpdateSpawner()
    {
        if (_stats.spawnPrefab == null || _activeSpawnCount >= _stats.maxSpawnCount) return;
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;
        _spawnTimer = _stats.spawnInterval;

        Vector2 offset = CardinalDir(Random.insideUnitCircle) * tileSize;
        var go   = Instantiate(_stats.spawnPrefab,
                               SnapPos(transform.position + (Vector3)offset), Quaternion.identity);
        var ctrl = go.GetComponent<EnemyController>();
        ctrl?.Init(); // Weihan - SpawnManager also calls this
        _activeSpawnCount++;
        EnemyController.OnEnemyDied += OnChildDied;
        void OnChildDied(EnemyController dead)
        {
            if (dead.gameObject != go) return;
            _activeSpawnCount--;
            EnemyController.OnEnemyDied -= OnChildDied;
        }
    }

    // ── Core tile step ────────────────────────────────────────────────────
    private IEnumerator TakeStep(Vector2 dir)
    {
        if (_isStepping) yield break;
        dir = CardinalDir(dir);
        if (dir == Vector2.zero || WouldHitObstacle(dir)) yield break;

        _isStepping        = true;
        _stepCooldownTimer = stepCooldown;
        FacingDirection    = dir;
        FlipSprite(dir);

        Vector2 start   = _rb.position;
        Vector2 end     = SnapPos(start + dir * tileSize);
        float   elapsed = 0f;

        while (elapsed < stepDuration)
        {
            elapsed += Time.fixedDeltaTime;
            _rb.MovePosition(Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / stepDuration)));
            yield return new WaitForFixedUpdate();
        }

        _rb.MovePosition(end);
        _isStepping = false;
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void BeginChase()
    {
        StopAllCoroutines();
        _isStepping        = false;
        _stepCooldownTimer = 0f;
    }

    public void StopMovement()
    {
        StopAllCoroutines();
        _isStepping  = false;
        _rb.velocity = Vector2.zero;
    }

    // Called by PossessionSystem.MovePossessedEnemy — one tile per key-press
    public void StepInDirection(Vector2 input)
    {
        if (_isStepping) return;
        StartCoroutine(TakeStep(CardinalDir(input)));
    }

    // Lets possessed enemy set facing direction explicitly (e.g. on possession start)
    public void SetFacingDirection(Vector2 dir)
    {
        if (dir != Vector2.zero) FacingDirection = CardinalDir(dir);
    }

    public EnemyController.EnemyState GetResumeState()
    {
        if (_player == null) return EnemyController.EnemyState.Idle;
        return Vector2.Distance(transform.position, _player.position) <= _stats.detectionRange
            ? EnemyController.EnemyState.Chasing
            : EnemyController.EnemyState.Roaming;
    }

    public void SetObstacleLayer(LayerMask layer) => obstacleLayer = layer; // Weihan

    // ── Pathfinding helpers ───────────────────────────────────────────────
    private Vector2 BestStepToward(Vector3 target)
    {
        Vector2 delta = (Vector2)target - (Vector2)transform.position;
        Vector2 pri   = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                        ? new Vector2(Mathf.Sign(delta.x), 0)
                        : new Vector2(0, Mathf.Sign(delta.y));
        Vector2 sec   = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                        ? new Vector2(0, Mathf.Sign(delta.y))
                        : new Vector2(Mathf.Sign(delta.x), 0);
        if (!WouldHitObstacle(pri)) return pri;
        if (!WouldHitObstacle(sec)) return sec;
        return Vector2.zero;
    }

    private bool WouldHitObstacle(Vector2 dir)
    {
        if (obstacleLayer == 0) return false;
        return Physics2D.OverlapCircle(SnapPos((Vector2)transform.position + dir * tileSize),
                                       tileSize * 0.3f, obstacleLayer);
    }

    private float TilesFrom(Vector3 target) =>
        Vector2.Distance(SnapPos(transform.position), SnapPos(target)) / tileSize;

    // ── Grid utilities ────────────────────────────────────────────────────
    private Vector2 SnapPos(Vector3 p) => SnapPos((Vector2)p);
    private Vector2 SnapPos(Vector2 p) =>
        new Vector2(Mathf.Round(p.x / tileSize) * tileSize,
                    Mathf.Round(p.y / tileSize) * tileSize);

    private void SnapToGrid() => _rb.position = SnapPos(transform.position);

    private Vector2 CardinalDir(Vector2 d)
    {
        if (d == Vector2.zero) return Vector2.zero;
        return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
            ? new Vector2(Mathf.Sign(d.x), 0f)
            : new Vector2(0f, Mathf.Sign(d.y));
    }

    private void FlipSprite(Vector2 dir)
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        if      (dir.x > 0.1f)  sr.flipX = false;
        else if (dir.x < -0.1f) sr.flipX = true;
    }
}