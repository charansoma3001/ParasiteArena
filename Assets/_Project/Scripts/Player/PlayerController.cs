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
    [Tooltip("Max world distance to possess (0.75 = 1.5 tiles at 0.5 tile size)")]
    public float possessionRange = 0.75f;

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

    private Vector2 _movement;
    private Vector2 _lastMoveDir = Vector2.down; // always holds the last non-zero direction
    private int     _facingDirection = 1;
    private float   _dashCooldownTimer;

    private void Awake()
    {
        if (!CompareTag("Player"))
        {
            Debug.LogError($"PlayerController on non-Player object '{gameObject.name}' — disabling.");
            enabled = false;
            return;
        }

        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (!CompareTag("Player")) return;
        _rb.freezeRotation = true;
        _rb.gravityScale   = 0f;
        CurrentHealth      = maxHealth;
    }

    private void Update()
    {
        if (!CompareTag("Player")) return;
        if (IsDead) return;

        _dashCooldownTimer -= Time.deltaTime;

        // ── While possessing: forward movement input to possessed enemy ──
        if (IsPossessing)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 input = new Vector2(h, v);

            // Step the possessed enemy one tile per key press (not held)
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.up);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.down);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.left);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                PossessionSystem.Instance.MovePossessedEnemy(Vector2.right);

            // Q fires the possessed enemy's ability
            if (Input.GetKeyDown(KeyCode.Q))
                PossessionSystem.Instance.UsePossessedAbility();

            // R triggers block (Swordsman only)
            if (Input.GetKeyDown(KeyCode.R))
                PossessionSystem.Instance.UsePossessedBlock();

            // E or Escape releases possession
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                PossessionSystem.Instance.EndPossession();

            return; // skip normal movement while possessing
        }

        // ── Normal movement ──────────────────────────────────────────────
        if (IsDashing) return;

        float mh = Input.GetAxisRaw("Horizontal");
        float mv = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(mh, mv).normalized;

        if (_movement != Vector2.zero)
            _lastMoveDir = _movement; // always remember last real direction

        SetAnimFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDirection = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDirection, 1);

        // Dash: uses _lastMoveDir so it works even if you tap Space while standing still
        if (Input.GetKeyDown(KeyCode.Space) && _dashCooldownTimer <= 0f)
            StartCoroutine(DashCoroutine(_lastMoveDir));

        // Possess: E key
        if (Input.GetKeyDown(KeyCode.E))
            TryPossessAdjacent();
    }

    private void FixedUpdate()
    {
        if (!CompareTag("Player")) return;
        if (IsDead || IsPossessing || IsDashing) return;
        _rb.velocity = _movement * speed; // Charan - StatManager may call SetSpeed()
    }

    // ── Dash ──────────────────────────────────────────────────────────────
    private IEnumerator DashCoroutine(Vector2 direction)
    {
        IsDashing          = true;
        IsInvincible       = true;
        _dashCooldownTimer = dashCooldown;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            _rb.velocity = direction * dashSpeed;
            elapsed     += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _rb.velocity = Vector2.zero;
        IsDashing    = false;
        yield return null;
        IsInvincible = false;
    }

    // ── Possession ────────────────────────────────────────────────────────
    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null)
        {
            Debug.LogError("PossessionSystem not found in scene. " +
                           "Add a GameObject with PossessionSystem.cs — NOT on the Player.");
            return;
        }

        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex == -1)
        {
            Debug.LogWarning("'Enemy' layer not found. Create it in Project Settings > Tags and Layers.");
            return;
        }

        LayerMask enemyMask = 1 << enemyLayerIndex;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, possessionRange, enemyMask);

        EnemyController closest = null;
        float minDist = float.MaxValue;

        foreach (var col in nearby)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead || ec.IsPossessed || ec.stats.isBoss) continue;
            float d = Vector2.Distance(transform.position, ec.transform.position);
            if (d < minDist) { minDist = d; closest = ec; }
        }

        if (closest != null)
        {
            Debug.Log($"Possessing {closest.gameObject.name}");
            PossessionSystem.Instance.TryPossessTarget(closest); // Terry - PossessionSystem
        }
        else
        {
            Debug.Log($"No enemy in possession range ({possessionRange}m). " +
                      $"Nearby colliders: {nearby.Length}");
        }
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount) // Terry - EnemyController
    {
        if (!CompareTag("Player")) return;
        if (IsDead || IsInvincible) return;
        // While possessing, the player body is hidden — don't take damage
        if (IsPossessing) return;
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

    // ── Possession hooks ──────────────────────────────────────────────────
    public void OnPossessionStart(EnemyController possessed) // Terry - PossessionSystem
    {
        if (_sr != null) _sr.enabled = false;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        // Move the player Transform to sit on the possessed enemy
        // so possession-range gizmo and any future camera follow stays correct
        transform.position = possessed.transform.position;
    }

    public void OnPossessionEnd(Vector3 returnPosition)
    {
        transform.position = returnPosition;
        if (_sr != null) _sr.enabled = true;
    }

    private void OnEnable()  => PossessionSystem.OnPossessionChanged += HandlePossessionChanged;
    private void OnDisable() => PossessionSystem.OnPossessionChanged -= HandlePossessionChanged;

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd(enemy.transform.position);
    }

    // ── Public stat setters ───────────────────────────────────────────────
    public void SetMaxHealth(int value) // Charan - StatManager
    {
        maxHealth     = value;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value;        // Charan - StatManager
    public void Heal(int amount)                               // Charan - ItemSystem
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void SetAnimFloat(string param, float value)
    {
        if (_anim != null) _anim.SetFloat(param, value);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, possessionRange);
    }
}
