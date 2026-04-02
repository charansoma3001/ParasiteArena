using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Tile Movement")]
    public float tileSize = 0.5f;
    public float stepDuration = 0.18f;
    public float stepCooldown = 0.35f;

    [Header("Archer")]
    public int archerKiteDistance = 3;

    [Header("Pathfinding")]
    public LayerMask obstacleLayer;

    private EnemyController ctrl;
    private Rigidbody2D rb;
    private EnemyStats stats;
    private Transform player;
    private EnemyAnimator anim;

    private bool isStepping;
    private float stepCooldownTimer;

    private Vector2 chompDir;
    private float chompBounceTimer;
    private const float ChompBounceInterval = 1.8f;

    private float spawnTimer;
    private int activeSpawnCount;

    private Vector2 spawnPos;
    private bool ratAlerted;
    private float ratAlertTimer;
    private bool ratReturning;

    public Vector2 FacingDirection { get; private set; } = Vector2.down;
    public bool IsMoving => isStepping;

    private void Awake()
    {
        ctrl = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f;
        stats = ctrl.stats;
        anim = GetComponentInChildren<EnemyAnimator>();
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;
    }

    private void Start()
    {
        SnapToGrid();
        spawnPos = rb.position;

        switch (stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp:
                chompDir = CardinalDir(Random.insideUnitCircle);
                chompBounceTimer = ChompBounceInterval;
                ctrl.SetState(EnemyController.EnemyState.Roaming);
                break;
            case EnemyStats.EnemyType.Spawner:
                ctrl.SetState(EnemyController.EnemyState.Idle);
                break;
            default:
                ctrl.SetState(EnemyController.EnemyState.Idle);
                break;
        }
    }

    private void Update()
    {
        if (ctrl.IsDead || ctrl.IsPossessed) return;
        anim?.TickMovement(isStepping);
        stepCooldownTimer -= Time.deltaTime;

        switch (stats.enemyType)
        {
            case EnemyStats.EnemyType.Chomp: UpdateChomp(); return;
            case EnemyStats.EnemyType.Spawner: UpdateSpawner(); return;
            case EnemyStats.EnemyType.Archer: UpdateArcher(); return;
            case EnemyStats.EnemyType.Rat: UpdateRat(); return;
        }
        UpdateHunter();
    }

    private void FixedUpdate()
    {
        if (ctrl.IsDead) return;
        if (!isStepping)
            rb.velocity = Vector2.zero;
    }

    private Vector3 GetTargetPosition() =>
        player != null ? player.position : transform.position;

    public Vector3 GetCurrentTarget() => GetTargetPosition();

    private void UpdateHunter()
    {
        Vector3 target = GetTargetPosition();
        float dist = Vector2.Distance(transform.position, target);

        if (ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= stats.detectionRange)
                ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        if (ctrl.CurrentState != EnemyController.EnemyState.Chasing) return;
        if (isStepping) return;

        float tilesDist = TilesFrom(target);
        float effectiveRange = (stats.enemyType == EnemyStats.EnemyType.Swordsman) ? stats.attackRange * 1.8f : stats.attackRange;
        if (tilesDist <= effectiveRange)
        {
            FacingDirection = CardinalDir(target - transform.position);
            anim?.SetFacing(FacingDirection);
            ctrl.TriggerAttack();
            return;
        }

        if (stepCooldownTimer > 0f) return;
        Vector2 step = BestStepToward(target);
        if (step != Vector2.zero) StartCoroutine(TakeStep(step));
    }

    private void UpdateArcher()
    {
        Vector3 target = GetTargetPosition();
        float dist = Vector2.Distance(transform.position, target);

        if (ctrl.CurrentState == EnemyController.EnemyState.Idle ||
            ctrl.CurrentState == EnemyController.EnemyState.Roaming)
        {
            if (dist <= stats.detectionRange)
                ctrl.SetState(EnemyController.EnemyState.Chasing);
        }

        if (ctrl.CurrentState != EnemyController.EnemyState.Chasing) return;
        if (ctrl.CurrentState == EnemyController.EnemyState.Attacking) return;

        float tilesDist = TilesFrom(target);
        bool aligned = IsCardinalAligned(target);
        bool inRange = tilesDist <= stats.arrowRange;

        if (aligned && inRange && tilesDist > 0)
        {
            FacingDirection = CardinalDir(target - transform.position);
            anim?.SetFacing(FacingDirection);
            ctrl.TriggerAttack();
            return;
        }

        if (isStepping || stepCooldownTimer > 0f) return;

        if (tilesDist < archerKiteDistance)
        {
            Vector2 away = CardinalDir(transform.position - target);
            if (!WouldHitObstacle(away)) { StartCoroutine(TakeStep(away)); return; }
        }

        Vector2 strafe = StrafeToAlign(target);
        if (strafe != Vector2.zero) StartCoroutine(TakeStep(strafe));
    }

    private void UpdateRat()
    {
        if (player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);
        bool inRange = dist <= stats.detectionRange;

        if (ctrl.CurrentState == EnemyController.EnemyState.Idle)
        {
            if (inRange)
            {
                ratReturning = false;
                ratAlerted = false;
                ratAlertTimer = stats.alertDelay;
                ctrl.SetState(EnemyController.EnemyState.Chasing);
            }
            return;
        }

        if (ctrl.CurrentState == EnemyController.EnemyState.Chasing)
        {
            if (!inRange)
            {
                ratReturning = true;
            }

            if (ratReturning)
            {
                float distToSpawn = Vector2.Distance(transform.position, spawnPos);
                if (distToSpawn <= tileSize * 0.6f)
                {
                    ratReturning = false;
                    ctrl.SetState(EnemyController.EnemyState.Idle);
                    return;
                }
                if (!isStepping && stepCooldownTimer <= 0f)
                {
                    Vector2 step = BestStepToward(spawnPos);
                    if (step != Vector2.zero) StartCoroutine(TakeStep(step));
                }
                return;
            }

            if (!ratAlerted)
            {
                ratAlertTimer -= Time.deltaTime;
                if (ratAlertTimer <= 0f) ratAlerted = true;
                return;
            }

            float tilesDist = TilesFrom(player.position);
            if (tilesDist <= stats.attackRange)
            {
                FacingDirection = CardinalDir(player.position - transform.position);
                anim?.SetFacing(FacingDirection);
                ctrl.TriggerAttack();
                return;
            }

            if (!isStepping && stepCooldownTimer <= 0f)
            {
                Vector2 step = BestStepToward(player.position);
                if (step != Vector2.zero) StartCoroutine(TakeStep(step));
            }
        }
    }

    private bool IsCardinalAligned(Vector3 target)
    {
        Vector2 mine = SnapPos(transform.position);
        Vector2 tgt = SnapPos(target);
        return Mathf.Approximately(mine.x, tgt.x) || Mathf.Approximately(mine.y, tgt.y);
    }

    private Vector2 StrafeToAlign(Vector3 target)
    {
        Vector2 mine = SnapPos(transform.position);
        Vector2 tgt  = SnapPos(target);
        float dx = tgt.x - mine.x;
        float dy = tgt.y - mine.y;

        if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f) return Vector2.zero;

        Vector2 h = new Vector2(Mathf.Sign(dx), 0f);
        Vector2 v = new Vector2(0f, Mathf.Sign(dy));

        Vector2 primary, secondary;
        if (Mathf.Abs(dx) < 0.01f) { primary = v; secondary = h; }
        else if (Mathf.Abs(dy) < 0.01f) { primary = h; secondary = v; }
        else
        {
            bool colFirst = Mathf.Abs(dx) <= Mathf.Abs(dy);
            primary = colFirst ? h : v;
            secondary = colFirst ? v : h;
        }

        if (!WouldHitObstacle(primary))   return primary;
        if (!WouldHitObstacle(secondary)) return secondary;
        return Vector2.zero;
    }

    public void BeginRoam()
    {
        StopAllCoroutines();
        if (stats.enemyType == EnemyStats.EnemyType.Rat) return;
        StartCoroutine(RoamCoroutine());
    }

    private IEnumerator RoamCoroutine()
    {
        while (true)
        {
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            Vector2 dir = dirs[Random.Range(0, dirs.Length)];
            if (!WouldHitObstacle(dir)) yield return StartCoroutine(TakeStep(dir));
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        }
    }

    private void UpdateChomp()
    {
        if (isStepping || stepCooldownTimer > 0f) return;
        chompBounceTimer -= Time.deltaTime;
        if (chompBounceTimer <= 0f || WouldHitObstacle(chompDir))
        {
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            chompDir = dirs[Random.Range(0, dirs.Length)];
            chompBounceTimer = ChompBounceInterval;
        }
        StartCoroutine(TakeStep(chompDir));
        if (player != null && Vector2.Distance(transform.position, player.position) < tileSize * 0.8f)
            player.GetComponent<PlayerController>()?.TakeDamage(ctrl.GetActualAttackDamage());
    }

    private void UpdateSpawner()
    {
        if (stats.spawnPrefab == null || activeSpawnCount >= stats.maxSpawnCount) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = stats.spawnInterval;

        Vector2 offset = CardinalDir(Random.insideUnitCircle) * tileSize;
        var go   = Instantiate(stats.spawnPrefab,
                               SnapPos(transform.position + (Vector3)offset), Quaternion.identity);
        var ctrl = go.GetComponent<EnemyController>();
        ctrl?.Init();
        activeSpawnCount++;
        EnemyController.OnEnemyDied += OnChildDied;
        void OnChildDied(EnemyController dead)
        {
            if (dead.gameObject != go) return;
            activeSpawnCount--;
            EnemyController.OnEnemyDied -= OnChildDied;
        }
    }

    private IEnumerator TakeStep(Vector2 dir)
    {
        if (isStepping) yield break;
        dir = CardinalDir(dir);
        if (dir == Vector2.zero || WouldHitObstacle(dir)) yield break;

        isStepping = true;
        stepCooldownTimer = stepCooldown;
        FacingDirection = dir;
        anim?.SetFacing(dir);

        rb.velocity = Vector2.zero;

        Vector2 start = rb.position;
        Vector2 end = SnapPos(start + dir * tileSize);
        float   elapsed = 0f;

        while (elapsed < stepDuration)
        {
            elapsed += Time.fixedDeltaTime;
            rb.velocity = Vector2.zero;
            rb.MovePosition(Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / stepDuration)));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(end);
        rb.velocity = Vector2.zero;
        isStepping = false;
    }

    public void BeginChase()
    {
        StopAllCoroutines();
        isStepping = false;
        stepCooldownTimer = 0f;
    }

    public void StopMovement()
    {
        StopAllCoroutines();
        isStepping = false;
        rb.velocity = Vector2.zero;
    }

    public void StepInDirection(Vector2 input)
    {
        if (isStepping) return;
        StartCoroutine(TakeStep(CardinalDir(input)));
    }

    public EnemyController.EnemyState GetResumeState()
    {
        if (player == null) return EnemyController.EnemyState.Idle;
        return Vector2.Distance(transform.position, player.position) <= stats.detectionRange
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

    private void SnapToGrid() => rb.position = SnapPos(transform.position);

    private Vector2 CardinalDir(Vector2 d)
    {
        if (d == Vector2.zero) return Vector2.zero;
        return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
            ? new Vector2(Mathf.Sign(d.x), 0f)
            : new Vector2(0f, Mathf.Sign(d.y));
    }

}