using System.Collections;
using UnityEngine;

// NO RequireComponent here — avoids Unity auto-adding this to enemy prefabs
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
    [Tooltip("Max world distance to possess (1.5 tiles = 0.75 for 0.5-unit tiles)")]
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
    private int     _facingDirection = 1;
    private float   _dashCooldownTimer;

    private void Awake()
    {
        // Guard: this script must only run on the Player — bail out if on anything else
        if (!CompareTag("Player"))
        {
            Debug.LogError($"PlayerController found on non-Player object '{gameObject.name}' — disabling.");
            enabled = false;
            return;
        }

        _rb  = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        if (_rb == null)
            Debug.LogError("PlayerController: no Rigidbody2D found on Player.");
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
        if (IsDead || IsPossessing || IsDashing) return;

        _dashCooldownTimer -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(h, v).normalized;

        SetAnimFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDirection = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDirection, 1);

        if (Input.GetKeyDown(KeyCode.Space) && _dashCooldownTimer <= 0f && _movement != Vector2.zero)
            StartCoroutine(DashCoroutine(_movement));

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
        yield return null; // one extra frame of iframes
        IsInvincible = false;
    }

    // ── Possession ────────────────────────────────────────────────────────
    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null) return;

        // Use layer index looked up at runtime — works regardless of layer order
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1)
        {
            Debug.LogWarning("PlayerController: 'Enemy' layer not found. Create it in Project Settings.");
            return;
        }
        LayerMask enemyMask = 1 << enemyLayer;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(
            transform.position, possessionRange, enemyMask);

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
            PossessionSystem.Instance.TryPossessTarget(closest); // Terry - PossessionSystem
        else
            Debug.Log("PlayerController: no possessable enemy in range.");
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount) // Terry - EnemyController
    {
        if (!CompareTag("Player")) return; // safety guard
        if (IsDead || IsInvincible) return;
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
    }

    public void OnPossessionEnd()
    {
        if (_sr != null) _sr.enabled = true;
    }

    private void OnEnable()  => PossessionSystem.OnPossessionChanged += HandlePossessionChanged; // Terry
    private void OnDisable() => PossessionSystem.OnPossessionChanged -= HandlePossessionChanged; // Terry

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd();
    }

    // ── Public stat setters ───────────────────────────────────────────────
    public void SetMaxHealth(int value) // Charan - StatManager
    {
        maxHealth     = value;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value; // Charan - StatManager

    public void Heal(int amount) // Charan - ItemSystem
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void SetAnimFloat(string param, float value)
    {
        if (_anim != null) _anim.SetFloat(param, value);
    }

    // Gizmo: always draws (not just when selected) so you can always see possession range
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && !CompareTag("Player")) return;
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, possessionRange);
    }
}
