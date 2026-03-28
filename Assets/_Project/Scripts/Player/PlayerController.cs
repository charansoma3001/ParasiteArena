using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;

    [Header("Dash")]
    public float dashSpeed    = 14f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.7f;

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Possession")]
    public float possessionRange = 0.8f;

    public int  CurrentHealth { get; private set; }
    public bool IsDead        { get; private set; }
    public bool IsDashing     { get; private set; }
    public bool IsInvincible  { get; private set; }
    public bool IsPossessing  => PossessionSystem.Instance != null &&
                                 PossessionSystem.Instance.IsPossessing;

    public static event System.Action<int, int> OnHealthChanged;
    public static event System.Action           OnPlayerDied;

    private Rigidbody2D    _rb;
    private Animator       _anim;
    private SpriteRenderer _sr;
    private Collider2D     _col;

    private Vector2 _movement;
    private Vector2 _lastMoveDir = Vector2.down;
    private int     _facingDir   = 1;
    private float   _dashCooldown;

    // Held-movement repeat timer for possessed enemy control
    private float   _possessedMoveTimer;
    private const float PossessedMoveInterval = 0.22f;

    private void Awake()
    {
        if (!CompareTag("Player"))
        {
            Debug.LogError($"PlayerController on non-Player '{gameObject.name}' — disabling.");
            enabled = false;
            return;
        }

        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        _col  = GetComponent<Collider2D>();

        // PlayerMovement.FixedUpdate sets rb.velocity every frame and overwrites
        // the dash velocity, making Space do nothing. Disable it permanently.
        var pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;
    }

    private void Start()
    {
        if (_rb == null) return;
        _rb.freezeRotation = true;
        _rb.gravityScale   = 0f;
        CurrentHealth      = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        _rb.position = new Vector2(50, 50);
    }

    private void Update()
    {
        if (IsDead) return;

        if (IsPossessing)
        {
            // Track the possessed enemy every frame so the indicator and camera
            // follow smoothly through the step animation, not just on key press.
            var possessed = PossessionSystem.Instance.PossessedEnemy;
            if (possessed != null)
                transform.position = possessed.transform.position;

            // Held WASD: fires a step every PossessedMoveInterval seconds while held.
            _possessedMoveTimer -= Time.deltaTime;
            Vector2 heldDir = GetHeldDirection();
            if (heldDir != Vector2.zero && _possessedMoveTimer <= 0f)
            {
                PossessionSystem.Instance.MovePossessedEnemy(heldDir);
                _possessedMoveTimer = PossessedMoveInterval;
            }
            else if (heldDir == Vector2.zero)
            {
                _possessedMoveTimer = 0f;
            }

            if (Input.GetKeyDown(KeyCode.Space))
                PossessionSystem.Instance.UsePossessedAbility();
            if (Input.GetKeyDown(KeyCode.R))
                PossessionSystem.Instance.UsePossessedBlock();
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                PossessionSystem.Instance.EndPossession();

            return;
        }

        if (IsDashing) return;

        _dashCooldown -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(h, v).normalized;

        if (_movement != Vector2.zero)
            _lastMoveDir = _movement;

        SetAnimFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDir = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDir, 1);

        if (Input.GetKeyDown(KeyCode.Space) && _dashCooldown <= 0f)
            StartCoroutine(DashCoroutine(_lastMoveDir));

        if (Input.GetKeyDown(KeyCode.E))
            TryPossessAdjacent();

        if (Input.GetKeyDown(KeyCode.H)) Heal(20);
    }

    private Vector2 GetHeldDirection()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    return Vector2.up;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  return Vector2.down;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  return Vector2.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return Vector2.right;
        return Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing || IsDashing) return;
        _rb.velocity = _movement * speed;
    }

    private IEnumerator DashCoroutine(Vector2 dir)
    {
        IsDashing     = true;
        IsInvincible  = true;
        _dashCooldown = dashCooldown;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            _rb.velocity = dir * dashSpeed;
            elapsed     += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _rb.velocity = Vector2.zero;
        IsDashing    = false;
        yield return null;
        IsInvincible = false;
    }

    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null)
        {
            Debug.LogError("[PlayerController] No PossessionSystem in scene.");
            return;
        }

        int layerIdx = LayerMask.NameToLayer("Enemy");
        if (layerIdx < 0)
        {
            Debug.LogWarning("[PlayerController] 'Enemy' layer not found.");
            return;
        }

        var cols = Physics2D.OverlapCircleAll(transform.position, possessionRange, 1 << layerIdx);
        EnemyController best    = null;
        float           closest = float.MaxValue;

        foreach (var col in cols)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead || ec.IsPossessed || ec.stats.isBoss) continue;
            float d = Vector2.Distance(transform.position, ec.transform.position);
            if (d < closest) { closest = d; best = ec; }
        }

        if (best != null)
            PossessionSystem.Instance.TryPossessTarget(best);
        else
            Debug.Log($"[PlayerController] No enemy in range ({possessionRange}m). Colliders: {cols.Length}");
    }

    public void TakeDamage(float amount)
    {
        if (!CompareTag("Player")) return;
        if (IsDead || IsInvincible) return;
        if (IsPossessing) return;
        
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.RoundToInt(amount));
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        
        if (CurrentHealth <= 0) 
        {
            Die();
        }
        else 
        {
            _anim.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead       = true;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        _anim.SetTrigger("Die");
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float deathAnimLength = _anim.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSecondsRealtime(deathAnimLength + 1f);

        OnPlayerDied?.Invoke();
    }

    public void OnPossessionStart(EnemyController possessed)
    {
        if (_sr)  _sr.enabled  = false;
        if (_col) _col.enabled = false;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        transform.position    = possessed.transform.position;
        _possessedMoveTimer   = 0f;
    }

    public void OnPossessionEnd(Vector3 returnPos)
    {
        transform.position = returnPos;
        if (_sr)  _sr.enabled  = true;
        if (_col) _col.enabled = true;
    }

    private void OnEnable()  => PossessionSystem.OnPossessionChanged += HandlePossessionChanged;
    private void OnDisable() => PossessionSystem.OnPossessionChanged -= HandlePossessionChanged;

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd(enemy.transform.position);
    }

    public void SetMaxHealth(int value)
    {
        maxHealth     = value;
        if (!IsDead && CurrentHealth <= 0)
            CurrentHealth = maxHealth;
        else
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value;

    public void Heal(int amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void SetAnimFloat(string p, float v) { if (_anim) _anim.SetFloat(p, v); }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, possessionRange);
    }
}