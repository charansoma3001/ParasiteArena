using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Pathfinding")]
    public LayerMask obstacleLayer;
    public float chaseUpdateInterval   = 0.25f;
    public float waypointReachedRadius = 0.4f;

    private EnemyController _ctrl;
    private Rigidbody2D     _rb;
    private EnemyStats      _stats;
    private Transform       _player;

    private Vector2 _moveTarget;
    private bool    _isMoving;
    private float   _chaseTimer;

    private Vector2 _chompDir;
    private float   _chompBounceTimer;
    private const float ChompBounceInterval = 1.8f;

    private float _spawnTimer;
    private int   _activeSpawnCount;

    // Cached facing direction — used for possession back-angle check, NOT for visual rotation
    public Vector2 FacingDirection { get; private set; } = Vector2.up;

    private void Awake()
    {
        _ctrl  = GetComponent<EnemyController>();
        _rb    = GetComponent<Rigidbody2D>();
        _stats = _ctrl.stats;

        // Freeze rotation via code as well, in case the prefab setting was lost
        _rb.freezeRotation = true;

        var playerGO = GameObject.FindWithTag("Player"); // Gagan
        if (playerGO) _player = playerGO.transform;
    }

    private void Start()
    {
        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:
                _chompDir         = Random.insideUnitCircle.normalized;
                _chompBounceTimer = ChompBounceInterval;
                _isMoving         = true;
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

        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:   UpdateChomp();   return;
            case EnemyStats.EnemyType.Spawner: UpdateSpawner(); return;
        }

        UpdateHunter();
    }

    private void FixedUpdate()
    {
        if (!_isMoving || _ctrl.IsDead || _ctrl.IsPossessed) return;

        Vector2 toTarget = (_moveTarget - (Vector2)transform.position);
        if (toTarget.sqrMagnitude < 0.01f) return;

        Vector2 dir   = toTarget.normalized;
        float   speed = GetEffectiveSpeed();
        dir = SteerAroundObstacles(dir); // Weihan - ObstacleSystem

        _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);

        // Store facing direction without rotating the transform
        if (dir != Vector2.zero)
            FacingDirection = dir;

        // Flip sprite child instead of rotating root — avoids fighting the Rigidbody constraint
        FlipSpriteToDirection(dir);
    }

    // Flips the child SpriteRenderer based on horizontal movement direction
    // This replaces transform.up = dir which was causing the rotation bug
    private void FlipSpriteToDirection(Vector2 dir)
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        if (dir.x > 0.1f)       sr.flipX = false;
        else if (dir.x < -0.1f) sr.flipX = true;
    }

    private void UpdateHunter()
    {
        if (_player == null) return;
        float dist = Vector2.Distance(transform.position, _player.position);

        if (_ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            _ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= _stats.detectionRange)
                _ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        if (_ctrl.CurrentState == EnemyController.EnemyState.Chasing)
        {
            _chaseTimer -= Time.deltaTime;
            if (_chaseTimer <= 0f)
            {
                _moveTarget = _player.position;
                _chaseTimer = chaseUpdateInterval;
            }
            if (dist <= _stats.attackRange)
            {
                StopMovement();
                _ctrl.TriggerAttack();
            }
        }
    }

    public void BeginRoam()
    {
        StopAllCoroutines();
        StartCoroutine(RoamCoroutine());
    }

    private IEnumerator RoamCoroutine()
    {
        Vector2 origin = transform.position;
        while (true)
        {
            _moveTarget = origin + Random.insideUnitCircle * _stats.roamRange;
            _isMoving   = true;
            float timeout = 4f;
            while (Vector2.Distance(transform.position, _moveTarget) > waypointReachedRadius
                   && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            _isMoving = false;
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
    }

    private void UpdateChomp()
    {
        _chompBounceTimer -= Time.deltaTime;
        if (_chompBounceTimer <= 0f || HitsObstacleAhead())
        {
            _chompDir         = (_chompDir * -1f + Random.insideUnitCircle * 0.5f).normalized;
            _chompBounceTimer = ChompBounceInterval;
        }
        _moveTarget = (Vector2)transform.position + _chompDir * 2f;

        if (_player != null && Vector2.Distance(transform.position, _player.position) < 0.8f)
            _player.GetComponent<PlayerController>()?.TakeDamage(_stats.attackDamage); // Gagan
    }

    private void UpdateSpawner()
    {
        if (_stats.spawnPrefab == null || _activeSpawnCount >= _stats.maxSpawnCount) return;
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;
        _spawnTimer = _stats.spawnInterval;

        Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
        var go   = Instantiate(_stats.spawnPrefab, transform.position + (Vector3)offset, Quaternion.identity);
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

    public void BeginChase()
    {
        StopAllCoroutines();
        _isMoving   = true;
        _chaseTimer = 0f;
    }

    public void StopMovement()
    {
        _isMoving    = false;
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

    private Vector2 SteerAroundObstacles(Vector2 desired) // Weihan - ObstacleSystem
    {
        float feeler = 1.2f;
        if (!Physics2D.Raycast(transform.position, desired, feeler, obstacleLayer)) return desired;
        Vector2 left  = new Vector2(-desired.y,  desired.x);
        Vector2 right = new Vector2( desired.y, -desired.x);
        if (!Physics2D.Raycast(transform.position, left,  feeler, obstacleLayer)) return left;
        if (!Physics2D.Raycast(transform.position, right, feeler, obstacleLayer)) return right;
        return -desired;
    }

    private bool HitsObstacleAhead() =>
        Physics2D.Raycast(transform.position, _chompDir, 0.8f, obstacleLayer);

    private float GetEffectiveSpeed()
    {
        // float mod = ObstacleSystem.Instance?.GetSpeedModifier(transform.position) ?? 1f; // Weihan
        return _stats.moveSpeed;
    }

    public void SetObstacleLayer(LayerMask layer) => obstacleLayer = layer; // Weihan
}
