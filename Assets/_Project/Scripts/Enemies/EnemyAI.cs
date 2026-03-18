using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Tile Movement")]
    public float tileSize          = 0.5f;   // must match your scene tile size
    public float stepDuration      = 0.18f;  // seconds to slide one tile
    public float stepCooldown      = 0.35f;  // pause between steps while chasing

    [Header("Pathfinding")]
    public LayerMask obstacleLayer;          // Weihan - ObstacleSystem
    public float waypointReachedRadius = 0.15f;

    private EnemyController _ctrl;
    private Rigidbody2D     _rb;
    private EnemyStats      _stats;
    private Transform       _player;
    private EnemyAnimator   _anim;

    private bool    _isStepping;
    private float   _stepCooldownTimer;

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

        var playerGO = GameObject.FindWithTag("Player"); // Gagan
        if (playerGO) _player = playerGO.transform;
    }

    private void Start()
    {
        // Snap to nearest tile grid on spawn
        SnapToGrid();

        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:
                _chompDir         = CardinalDirection(Random.insideUnitCircle);
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
        }

        UpdateHunter();
    }

    // ── Hunter (Swordsman, Archer, Warrior, etc.) ─────────────────────────
    private void UpdateHunter()
    {
        if (_player == null) return;
        float dist = Vector2.Distance(transform.position, _player.position);

        // Detection
        if (_ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            _ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= _stats.detectionRange)
                _ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        // Chasing
        if (_ctrl.CurrentState != EnemyController.EnemyState.Chasing) return;
        if (_isStepping) return;

        // Attack if player is adjacent (one tile away)
        float attackDist = TilesFromTarget(_player.position);
        if (attackDist <= _stats.attackRange)
        {
            // Face the player before attacking
            FacingDirection = CardinalDirection((_player.position - transform.position));
            _ctrl.TriggerAttack();
            return;
        }

        // Step toward player on cooldown
        if (_stepCooldownTimer > 0f) return;
        Vector2 stepDir = BestStepToward(_player.position);
        if (stepDir != Vector2.zero)
            StartCoroutine(TakeStep(stepDir));
    }

    // ── Roaming (fixed enemies that wander) ───────────────────────────────
    public void BeginRoam()
    {
        StopAllCoroutines();
        StartCoroutine(RoamCoroutine());
    }

    private IEnumerator RoamCoroutine()
    {
        while (true)
        {
            // Pick a random adjacent tile direction
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            Vector2 dir = dirs[Random.Range(0, dirs.Length)];

            if (!WouldHitObstacle(dir))
                yield return StartCoroutine(TakeStep(dir));

            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        }
    }

    // ── Chomp (bounces around, damages on contact) ────────────────────────
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

        Vector2 offset = CardinalDirection(Random.insideUnitCircle) * tileSize;
        var go   = Instantiate(_stats.spawnPrefab,
                               SnapPosition(transform.position + (Vector3)offset),
                               Quaternion.identity);
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

    // ── Core tile step coroutine ──────────────────────────────────────────
    private IEnumerator TakeStep(Vector2 direction)
    {
        if (_isStepping) yield break;

        direction = CardinalDirection(direction); // force to cardinal
        if (WouldHitObstacle(direction)) yield break;

        _isStepping        = true;
        _stepCooldownTimer = stepCooldown;
        FacingDirection    = direction;
        FlipSprite(direction);

        Vector2 start  = _rb.position;
        Vector2 target = SnapPosition(start + direction * tileSize);
        float   elapsed = 0f;

        while (elapsed < stepDuration)
        {
            elapsed     += Time.fixedDeltaTime;
            float t      = Mathf.Clamp01(elapsed / stepDuration);
            _rb.MovePosition(Vector2.Lerp(start, target, t));
            yield return new WaitForFixedUpdate();
        }

        _rb.MovePosition(target); // guarantee exact grid snap
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

    public EnemyController.EnemyState GetResumeState()
    {
        if (_player == null) return EnemyController.EnemyState.Idle;
        float dist = Vector2.Distance(transform.position, _player.position);
        return dist <= _stats.detectionRange
            ? EnemyController.EnemyState.Chasing
            : EnemyController.EnemyState.Roaming;
    }

    public void SetObstacleLayer(LayerMask layer) => obstacleLayer = layer; // Weihan

    // ── Pathfinding helpers ───────────────────────────────────────────────

    /// Returns the best single cardinal step toward a target, avoiding obstacles.
    private Vector2 BestStepToward(Vector2 target)
    {
        Vector2 delta = target - (Vector2)transform.position;

        // Try the dominant axis first, then the minor axis, then diagonals
        Vector2 primary   = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                            ? new Vector2(Mathf.Sign(delta.x), 0)
                            : new Vector2(0, Mathf.Sign(delta.y));
        Vector2 secondary = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                            ? new Vector2(0, Mathf.Sign(delta.y))
                            : new Vector2(Mathf.Sign(delta.x), 0);

        if (!WouldHitObstacle(primary))   return primary;
        if (!WouldHitObstacle(secondary)) return secondary;
        return Vector2.zero;
    }

    private bool WouldHitObstacle(Vector2 dir)
    {
        if (obstacleLayer == 0) return false;
        Vector2 next = SnapPosition((Vector2)transform.position + dir * tileSize);
        return Physics2D.OverlapCircle(next, tileSize * 0.3f, obstacleLayer);
    }

    /// Distance in tiles between this enemy and a world position (rounded to int)
    private float TilesFromTarget(Vector3 target) =>
        Vector2.Distance(
            SnapPosition(transform.position),
            SnapPosition(target)) / tileSize;

    // ── Grid utilities ────────────────────────────────────────────────────

    private Vector2 SnapPosition(Vector3 pos) => SnapPosition((Vector2)pos);
    private Vector2 SnapPosition(Vector2 pos) =>
        new Vector2(
            Mathf.Round(pos.x / tileSize) * tileSize,
            Mathf.Round(pos.y / tileSize) * tileSize);

    private void SnapToGrid()
    {
        Vector2 snapped = SnapPosition(transform.position);
        _rb.position    = snapped;
    }

    /// Converts any direction to the nearest cardinal (up/down/left/right)
    private Vector2 CardinalDirection(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(dir.y));
    }

    // ── Sprite flipping ───────────────────────────────────────────────────
    private void FlipSprite(Vector2 dir)
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        if      (dir.x > 0.1f)  sr.flipX = false;
        else if (dir.x < -0.1f) sr.flipX = true;
    }
}
