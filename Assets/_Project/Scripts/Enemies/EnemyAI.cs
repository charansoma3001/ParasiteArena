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
    public int archerKiteDistance = 3;

    [Header("Pathfinding")]
    public LayerMask obstacleLayer;

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

    private Vector2 _spawnPos;
    private bool    _ratAlerted;
    private float   _ratAlertTimer;
    private bool    _ratReturning;

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
        var p = GameObject.FindWithTag("Player");
        if (p) _player = p.transform;
    }

    private void Start()
    {
        SnapToGrid();
        _spawnPos = _rb.position;

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
            case EnemyStats.EnemyType.Rat:     UpdateRat();     return;
        }
        UpdateHunter();
    }

    // ---------------------------------------------------------------
    // FIX (infinite pushback):
    // All movement in this game uses MovePosition, but the Rigidbody2D
    // is Dynamic, so physics collision responses still write a velocity
    // that persists until someone zeroes it. Between steps nobody did,
    // so a single bump could send an entity sliding forever.
    //
    // Solution: every FixedUpdate frame, if we are NOT currently running
    // a step animation, force velocity to zero. This is safe because
    // intentional movement is driven entirely by MovePosition; velocity
    // is never the intended locomotion mechanism for enemies.
    // ---------------------------------------------------------------
    private void FixedUpdate()
    {
        if (_ctrl.IsDead) return;
        if (!_isStepping)
            _rb.velocity = Vector2.zero;
    }

    // PlayerController.Update copies transform.position to possessed enemy every frame,
    // so _player.position always equals the effective target automatically.
    private Vector3 GetTargetPosition() =>
        _player != null ? _player.position : transform.position;

    public Vector3 GetCurrentTarget() => GetTargetPosition();

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
        bool  inRange   = tilesDist <= _stats.arrowRange;

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

    // Rat AI: idle at spawn until player in range, then alert delay, then chase.
    // If player leaves range at any point, returns to spawn and goes idle.
    private void UpdateRat()
    {
        if (_player == null) return;
        float dist    = Vector2.Distance(transform.position, _player.position);
        bool  inRange = dist <= _stats.detectionRange;

        if (_ctrl.CurrentState == EnemyController.EnemyState.Idle)
        {
            if (inRange)
            {
                _ratReturning  = false;
                _ratAlerted    = false;
                _ratAlertTimer = _stats.alertDelay;
                _ctrl.SetState(EnemyController.EnemyState.Chasing);
            }
            return;
        }

        if (_ctrl.CurrentState == EnemyController.EnemyState.Chasing)
        {
            if (!inRange)
            {
                _ratReturning = true;
            }

            if (_ratReturning)
            {
                float distToSpawn = Vector2.Distance(transform.position, _spawnPos);
                if (distToSpawn <= tileSize * 0.6f)
                {
                    _ratReturning = false;
                    _ctrl.SetState(EnemyController.EnemyState.Idle);
                    return;
                }
                if (!_isStepping && _stepCooldownTimer <= 0f)
                {
                    Vector2 step = BestStepToward(_spawnPos);
                    if (step != Vector2.zero) StartCoroutine(TakeStep(step));
                }
                return;
            }

            if (!_ratAlerted)
            {
                _ratAlertTimer -= Time.deltaTime;
                if (_ratAlertTimer <= 0f) _ratAlerted = true;
                return;
            }

            float tilesDist = TilesFrom(_player.position);
            if (tilesDist <= _stats.attackRange)
            {
                FacingDirection = CardinalDir(_player.position - transform.position);
                _ctrl.TriggerAttack();
                return;
            }

            if (!_isStepping && _stepCooldownTimer <= 0f)
            {
                Vector2 step = BestStepToward(_player.position);
                if (step != Vector2.zero) StartCoroutine(TakeStep(step));
            }
        }
    }

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

    public void BeginRoam()
    {
        StopAllCoroutines();
        if (_stats.enemyType == EnemyStats.EnemyType.Rat) return;
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
            _player.GetComponent<PlayerController>()?.TakeDamage(_stats.attackDamage);
    }

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
        ctrl?.Init();
        _activeSpawnCount++;
        EnemyController.OnEnemyDied += OnChildDied;
        void OnChildDied(EnemyController dead)
        {
            if (dead.gameObject != go) return;
            _activeSpawnCount--;
            EnemyController.OnEnemyDied -= OnChildDied;
        }
    }

    private IEnumerator TakeStep(Vector2 dir)
    {
        if (_isStepping) yield break;
        dir = CardinalDir(dir);
        if (dir == Vector2.zero || WouldHitObstacle(dir)) yield break;

        _isStepping        = true;
        _stepCooldownTimer = stepCooldown;
        FacingDirection    = dir;
        FlipSprite(dir);

        // FIX (pushback): clear any residual physics velocity before we
        // start the MovePosition lerp so the two don't fight each other.
        _rb.velocity = Vector2.zero;

        Vector2 start   = _rb.position;
        Vector2 end     = SnapPos(start + dir * tileSize);
        float   elapsed = 0f;

        while (elapsed < stepDuration)
        {
            elapsed += Time.fixedDeltaTime;
            // FIX (pushback): re-zero every physics tick. A collision during
            // the lerp deposits a reaction velocity; suppressing it here
            // keeps the step animation clean and prevents the entity from
            // being deflected sideways by another body.
            _rb.velocity = Vector2.zero;
            _rb.MovePosition(Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / stepDuration)));
            yield return new WaitForFixedUpdate();
        }

        _rb.MovePosition(end);
        _rb.velocity = Vector2.zero; // guarantee a clean stop at tile boundary
        _isStepping  = false;
    }

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

    public void StepInDirection(Vector2 input)
    {
        if (_isStepping) return;
        StartCoroutine(TakeStep(CardinalDir(input)));
    }

    public EnemyController.EnemyState GetResumeState()
    {
        if (_player == null) return EnemyController.EnemyState.Idle;
        return Vector2.Distance(transform.position, _player.position) <= _stats.detectionRange
            ? EnemyController.EnemyState.Chasing
            : EnemyController.EnemyState.Roaming;
    }

    public void SetObstacleLayer(LayerMask layer) => obstacleLayer = layer;

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