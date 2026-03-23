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
    [Tooltip("Max world distance for E-key possession. 0.8 = ~1.5 tiles at 0.5 tile size.")]
    public float possessionRange = 0.8f;

    public int  CurrentHealth { get; private set; }
    public bool IsDead        { get; private set; }
    public bool IsDashing     { get; private set; }
    public bool IsInvincible  { get; private set; }
    public bool IsPossessing  => PossessionSystem.Instance != null &&
                                 PossessionSystem.Instance.IsPossessing;

    public static event System.Action<int, int> OnHealthChanged; // Gagan - UIManager
    public static event System.Action           OnPlayerDied;    // Gagan - UIManager + Charan - ScoreManager

    private Rigidbody2D    _rb;
    private Animator       _anim;
    private SpriteRenderer _sr;
    private Collider2D     _col;

    private Vector2 _movement;
    private Vector2 _lastMoveDir = Vector2.down;
    private int     _facingDir   = 1;
    private float   _dashCooldown;

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

        // PlayerMovement.FixedUpdate runs rb.velocity = movement * speed every frame,
        // which overwrites the dash velocity and makes Space do nothing.
        // Disable it here so PlayerController owns all movement.
        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.enabled = false;
            Debug.Log("[PlayerController] Disabled PlayerMovement — PlayerController handles all movement.");
        }
    }

    private void Start()
    {
        if (_rb == null) return;
        _rb.freezeRotation = true;
        _rb.gravityScale   = 0f;
        CurrentHealth      = maxHealth;
    }

    private void Update()
    {
        if (IsDead) return;

        // ── While possessing ─────────────────────────────────────────────
        if (IsPossessing)
        {
            // Track the possessed body EVERY FRAME so the player indicator
            // and camera follow smoothly, not just on key presses.
            var possessed = PossessionSystem.Instance.PossessedEnemy;
            if (possessed != null)
                transform.position = possessed.transform.position;

            // WASD / arrows → one tile step per key-down
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.up);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.down);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.left);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.right);

            // Space → fire the possessed enemy's ability
            if (Input.GetKeyDown(KeyCode.Space))
                PossessionSystem.Instance.UsePossessedAbility();

            // R → block (Swordsman only)
            if (Input.GetKeyDown(KeyCode.R))
                PossessionSystem.Instance.UsePossessedBlock();

            // E or Escape → release
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                PossessionSystem.Instance.EndPossession();

            return;
        }

        // ── Normal movement ──────────────────────────────────────────────
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

        // Space → dash (uses last direction so it works even while standing still)
        if (Input.GetKeyDown(KeyCode.Space) && _dashCooldown <= 0f)
            StartCoroutine(DashCoroutine(_lastMoveDir));

        // E → possess adjacent enemy
        if (Input.GetKeyDown(KeyCode.E))
            TryPossessAdjacent();

        if (Input.GetKeyDown(KeyCode.H)) Heal(20); // test key
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing || IsDashing) return;
        _rb.velocity = _movement * speed; // Charan - StatManager may call SetSpeed()
    }

    // ── Dash ──────────────────────────────────────────────────────────────
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
        yield return null;        // one extra frame of iframes
        IsInvincible = false;
    }

    // ── Possession ────────────────────────────────────────────────────────
    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null)
        {
            Debug.LogError("[PlayerController] No PossessionSystem in scene. " +
                           "Add it to a separate empty GameObject, NOT the Player.");
            return;
        }

        int layerIdx = LayerMask.NameToLayer("Enemy");
        if (layerIdx < 0)
        {
            Debug.LogWarning("[PlayerController] 'Enemy' layer not found in Project Settings.");
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
            Debug.Log($"[PlayerController] No enemy in range ({possessionRange}m). " +
                      $"Colliders on Enemy layer: {cols.Length}");
    }

    // ── Damage & Death ────────────────────────────────────────────────────
    public void TakeDamage(float amount) // Terry - EnemyController
    {
        if (!CompareTag("Player")) return;
        if (IsDead || IsInvincible)   return;
        if (IsPossessing)             return; // body is hidden and immune
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.RoundToInt(amount));
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth); // Gagan - UIManager
        if (CurrentHealth <= 0) Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead       = true;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        OnPlayerDied?.Invoke(); // Gagan - UIManager + Charan - ScoreManager
    }

    // ── Possession hooks (called by PossessionSystem) ─────────────────────
    public void OnPossessionStart(EnemyController possessed) // Terry - PossessionSystem
    {
        if (_sr)  _sr.enabled  = false;
        // Disable collider so enemies don't detect the hidden player body
        // and get stuck attacking empty space instead of the possessed enemy.
        if (_col) _col.enabled = false;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        transform.position = possessed.transform.position;
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

    // ── Stat setters ──────────────────────────────────────────────────────
    public void SetMaxHealth(int value) // Charan - StatManager
    {
        maxHealth     = value;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value; // Charan - StatManager

    public void Heal(int amount) // Charan - ItemSystem / ChestSystem
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