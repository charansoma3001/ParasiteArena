using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    //  Inspector
    [Header("Pathfinding / Movement")]
    [Tooltip("Layer mask for obstacles (set via Weihan's ObstacleSystem).")]
    public LayerMask obstacleLayer;

    [Tooltip("How often (s) the AI recalculates its chase target position.")]
    public float chaseUpdateInterval = 0.25f;

    [Tooltip("Distance threshold to consider a roam waypoint reached.")]
    public float waypointReachedRadius = 0.4f;


    private EnemyController _ctrl;
    private Rigidbody2D     _rb;
    private EnemyStats      _stats;
    private Transform       _player;

    private Vector2 _moveTarget;
    private bool    _isMoving;
    private float   _chaseTimer;

    // Chomp
    private Vector2 _chompDir;
    private float   _chompBounceTimer;
    private const float ChompBounceInterval = 1.8f;

    // Spawner
    private float   _spawnTimer;
    private int     _activeSpawnCount;

    //  Init
    private void Awake()
    {
        _ctrl  = GetComponent<EnemyController>();
        _rb    = GetComponent<Rigidbody2D>();
        _stats = _ctrl.stats;

        // add the player tag to the actual player #GAGAN
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) _player = playerGO.transform;
    }

    private void Start()
    {
        switch (_stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:
                InitChomp();
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
            case EnemyStats.EnemyType.Chomp:
                UpdateChomp();
                return;
            case EnemyStats.EnemyType.Spawner:
                UpdateSpawner();
                return;
        }

        // defaul enemies that hunt the player
        UpdateHunter();
    }

    private void FixedUpdate()
    {
        if (!_isMoving || _ctrl.IsDead || _ctrl.IsPossessed) return;

        Vector2 dir  = (_moveTarget - (Vector2)transform.position).normalized;
        float speed  = GetEffectiveSpeed();

        dir = SteerAroundObstacles(dir);

        _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);

        if (dir != Vector2.zero)
            transform.up = dir;
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
                _moveTarget  = _player.position;
                _chaseTimer  = chaseUpdateInterval;
            }

            if (dist <= _stats.attackRange)
            {
                StopMovement();
                _ctrl.TriggerAttack();
            }
        }
    }

    // fixed enemies
    public void BeginRoam()
    {
        StopAllCoroutines();
        StartCoroutine(RoamCoroutine());
    }

    private IEnumerator RoamCoroutine()
    {
        while (true)
        {
            Vector2 origin = transform.position;
            Vector2 target = origin + Random.insideUnitCircle * _stats.roamRange;
            _moveTarget    = target;
            _isMoving      = true;

            float timeout = 4f;
            while (Vector2.Distance(transform.position, _moveTarget) > waypointReachedRadius && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            _isMoving = false;
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
    }

    //  Chomp 
    private void InitChomp()
    {
        _chompDir         = Random.insideUnitCircle.normalized;
        _chompBounceTimer = ChompBounceInterval;
        _isMoving         = true;
        _ctrl.SetState(EnemyController.EnemyState.Roaming);
    }

    private void UpdateChomp()
    {
        _chompBounceTimer -= Time.deltaTime;

        if (_chompBounceTimer <= 0f || HitsObstacleAhead())
        {
            _chompDir         = _chompDir * -1f + Random.insideUnitCircle * 0.5f;
            _chompDir         = _chompDir.normalized;
            _chompBounceTimer = ChompBounceInterval;
        }

        _moveTarget = (Vector2)transform.position + _chompDir * 2f;

        // Bite XDDDDDDDDDDDDDD
        if (_player != null && Vector2.Distance(transform.position, _player.position) < 0.8f)
            _player.GetComponent<PlayerController>()?.TakeDamage(_stats.attackDamage);
    }

    private void UpdateSpawner()
    {
        if (_stats.spawnPrefab == null) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f && _activeSpawnCount < _stats.maxSpawnCount)
        {
            _spawnTimer = _stats.spawnInterval;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
        var go = Instantiate(_stats.spawnPrefab,
                             transform.position + (Vector3)offset,
                             Quaternion.identity);

        var ctrl = go.GetComponent<EnemyController>();
        ctrl?.Init();
        _activeSpawnCount++;

        EnemyController.OnEnemyDied += OnSpawnedChildDied;

        void OnSpawnedChildDied(EnemyController dead)
        {
            if (dead.gameObject == go)
            {
                _activeSpawnCount--;
                EnemyController.OnEnemyDied -= OnSpawnedChildDied;
            }
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
        _isMoving = false;
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

    //  just go around obstacles seriously
    private Vector2 SteerAroundObstacles(Vector2 desiredDir)
    {
        float feelerLen = 1.2f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, desiredDir, feelerLen, obstacleLayer);
        if (!hit) return desiredDir;

        Vector2 leftDir  = new Vector2(-desiredDir.y, desiredDir.x);
        Vector2 rightDir = new Vector2(desiredDir.y, -desiredDir.x);

        bool leftClear  = !Physics2D.Raycast(transform.position, leftDir,  feelerLen, obstacleLayer);
        bool rightClear = !Physics2D.Raycast(transform.position, rightDir, feelerLen, obstacleLayer);

        if (leftClear)  return leftDir;
        if (rightClear) return rightDir;

        return -desiredDir;
    }

    private bool HitsObstacleAhead()
    {
        return Physics2D.Raycast(transform.position, _chompDir, 0.8f, obstacleLayer);
    }

   
    private float GetEffectiveSpeed()
    {
        float mod = 1f;
        // Hook: ObstacleSystem.GetSpeedModifier(transform.position) - Weihan
        // mod = ObstacleSystem.Instance?.GetSpeedModifier(transform.position) ?? 1f;
        return _stats.moveSpeed * mod;
    }

    public void SetObstacleLayer(LayerMask layer)
    {
        obstacleLayer = layer;
    }
}
