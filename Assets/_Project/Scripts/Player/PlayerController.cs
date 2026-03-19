using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;

    [Header("Dodge")]
    public float dodgeSpeed         = 8f;
    public float dodgeDuration      = 0.18f;
    public float dodgeCooldown      = 0.8f;
    public float perfectDodgeWindow = 0.08f;

    [Header("Health")]
    public int maxHealth = 100;

    public int  CurrentHealth { get; private set; }
    public bool IsDead        { get; private set; }
    public bool IsDodging     { get; private set; }
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
    private float   _dodgeCooldownTimer;
    private Vector2 _dodgeDir;

    private void Start()
    {
        _rb   = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;

        // Animator and SpriteRenderer may be on this object or on a child — check both
        _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (IsDead || IsPossessing || IsDodging) return;

        _dodgeCooldownTimer -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(h, v).normalized;

        SetAnimFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDirection = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDirection, 1);

        if (Input.GetKeyDown(KeyCode.Space) && _dodgeCooldownTimer <= 0f && _movement != Vector2.zero)
            StartCoroutine(DodgeCoroutine());

        if(Input.GetKeyDown(KeyCode.H)) // (temporary heal key for testing)
            Heal(20);

        if(Input.GetKeyDown(KeyCode.D)) // (temporary damage key for testing)
            TakeDamage(20);

        if (Input.GetKeyDown(KeyCode.E))
            PossessionSystem.Instance?.TryPossess(); // Terry - PossessionSystem
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing || IsDodging) return;
        _rb.velocity = _movement * speed; // Charan - StatManager may override speed
    }

    private IEnumerator DodgeCoroutine()
    {
        IsDodging           = true;
        IsInvincible        = true;
        _dodgeCooldownTimer = dodgeCooldown;
        _dodgeDir           = _movement;

        float elapsed         = 0f;
        bool  perfectNotified = false;

        while (elapsed < dodgeDuration)
        {
            _rb.velocity = _dodgeDir * dodgeSpeed;
            elapsed     += Time.fixedDeltaTime;

            if (!perfectNotified && elapsed <= perfectDodgeWindow)
            {
                PossessionSystem.Instance?.NotifyPerfectDodge(); // Terry - PossessionSystem
                perfectNotified = true;
            }

            yield return new WaitForFixedUpdate();
        }

        _rb.velocity = Vector2.zero;
        IsDodging    = false;
        yield return null;
        IsInvincible = false;
    }

    public void TakeDamage(float amount) // Terry - EnemyController
    {
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

    public void OnPossessionStart(EnemyController possessed) // Terry - PossessionSystem
    {
        if (_sr) _sr.enabled = false;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
    }

    public void OnPossessionEnd()
    {
        if (_sr) _sr.enabled = true;
    }

    private void OnEnable()  => PossessionSystem.OnPossessionChanged += HandlePossessionChanged; // Terry
    private void OnDisable() => PossessionSystem.OnPossessionChanged -= HandlePossessionChanged; // Terry

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd();
    }

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

    // Null-safe animator helper — won't throw if Animator is missing
    private void SetAnimFloat(string param, float value)
    {
        if (_anim != null) _anim.SetFloat(param, value);
    }
}
