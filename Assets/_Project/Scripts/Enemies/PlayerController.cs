using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;

    [Header("Dodge")]
    public float dodgeSpeed          = 8f;
    public float dodgeDuration       = 0.18f;
    public float dodgeCooldown       = 0.8f;
    public float perfectDodgeWindow  = 0.08f; // seconds at start of dodge that count as perfect

    [Header("Health")]
    public int maxHealth = 100;

    // ── Runtime state ──────────────────────────────────────────
    public int  CurrentHealth  { get; private set; }
    public bool IsDead         { get; private set; }
    public bool IsDodging      { get; private set; }
    public bool IsInvincible   { get; private set; }
    public bool IsPossessing   => PossessionSystem.Instance != null &&
                                  PossessionSystem.Instance.IsPossessing;

    // ── Events ─────────────────────────────────────────────────
    public static event System.Action<int, int> OnHealthChanged; // Gagan - UIManager (current, max)
    public static event System.Action           OnPlayerDied;    // Gagan - UIManager / Charan - ScoreManager

    // ── Private ────────────────────────────────────────────────
    private Rigidbody2D    _rb;
    private Animator       _anim;
    private SpriteRenderer _sr;

    private Vector2 _movement;
    private int     _facingDirection = 1;
    private float   _dodgeCooldownTimer;
    private Vector2 _dodgeDir;

    private void Start()
    {
        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _sr   = GetComponent<SpriteRenderer>();

        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (IsDead || IsPossessing) return;
        if (IsDodging) return;

        _dodgeCooldownTimer -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(h, v).normalized;

        _anim.SetFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDirection = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDirection, 1);

        if (Input.GetKeyDown(KeyCode.Space) && _dodgeCooldownTimer <= 0f && _movement != Vector2.zero)
            StartCoroutine(DodgeCoroutine());

        if (Input.GetKeyDown(KeyCode.E))
            PossessionSystem.Instance?.TryPossess(); // Terry - PossessionSystem
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing) return;
        if (IsDodging) return;

        _rb.velocity = _movement * speed; // Charan - StatManager may override speed via GetStat()
    }

    // ── Dodge ──────────────────────────────────────────────────
    private IEnumerator DodgeCoroutine()
    {
        IsDodging              = true;
        IsInvincible           = true;
        _dodgeCooldownTimer    = dodgeCooldown;
        _dodgeDir              = _movement;

        float elapsed          = 0f;
        bool  perfectNotified  = false;

        while (elapsed < dodgeDuration)
        {
            _rb.velocity = _dodgeDir * dodgeSpeed;
            elapsed     += Time.fixedDeltaTime;

            if (!perfectNotified && elapsed <= perfectDodgeWindow)
            {
                // Terry - PossessionSystem: opens front-angle possession window
                PossessionSystem.Instance?.NotifyPerfectDodge();
                perfectNotified = true;
            }

            yield return new WaitForFixedUpdate();
        }

        _rb.velocity = Vector2.zero;
        IsDodging    = false;

        // Iframes linger one frame after dodge ends so last-frame hits don't land
        yield return null;
        IsInvincible = false;
    }

    // ── Damage ─────────────────────────────────────────────────
    public void TakeDamage(float amount) // called by Terry - EnemyController attacks
    {
        if (IsDead || IsInvincible) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.RoundToInt(amount));
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth); // Gagan - UIManager

        if (CurrentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead       = true;
        _rb.velocity = Vector2.zero;
        _anim.SetFloat("Speed", 0);
        OnPlayerDied?.Invoke(); // Gagan - UIManager + Charan - ScoreManager
    }

    // ── Possession hooks ───────────────────────────────────────
    public void OnPossessionStart(EnemyController possessed) // called by Terry - PossessionSystem
    {
        _sr.enabled  = false;
        _rb.velocity = Vector2.zero;
        _anim.SetFloat("Speed", 0);
    }

    public void OnPossessionEnd() // called by Terry - PossessionSystem (subscribe below)
    {
        _sr.enabled = true;
    }

    private void OnEnable()
    {
        PossessionSystem.OnPossessionChanged += HandlePossessionChanged; // Terry
    }

    private void OnDisable()
    {
        PossessionSystem.OnPossessionChanged -= HandlePossessionChanged; // Terry
    }

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd();
    }

    // ── Public stat setters ────────────────────────────────────
    public void SetMaxHealth(int value) // Charan - StatManager
    {
        maxHealth     = value;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value; // Charan - StatManager

    public void Heal(int amount) // Charan - ItemSystem (chest relics)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
