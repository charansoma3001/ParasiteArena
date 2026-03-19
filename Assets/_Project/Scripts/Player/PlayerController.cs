using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
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
    [Tooltip("Max world distance to possess. Should be ~1.5 tiles = 0.75 for 0.5-unit tiles.")]
    public float possessionRange = 0.75f;

    public int  CurrentHealth { get; private set; }
    public bool IsDead        { get; private set; }
    public bool IsDashing     { get; private set; }
    public bool IsInvincible  { get; private set; }
    public bool IsPossessing  => PossessionSystem.Instance != null &&
                                 PossessionSystem.Instance.IsPossessing;

    public static event System.Action<int, int> OnHealthChanged; // Gagan
    public static event System.Action           OnPlayerDied;    // Gagan + Charan 

    private Rigidbody2D    _rb;
    private Animator       _anim;
    private SpriteRenderer _sr;

    private Vector2 _movement;
    private Vector2 _lastMoveDir = Vector2.down;
    private int     _facingDirection = 1;
    private float   _dashCooldownTimer;

    private void Start()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;
        _rb.gravityScale   = 0f;

        _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _sr   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (IsDead || IsPossessing || IsDashing) return;

        _dashCooldownTimer -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _movement = new Vector2(h, v).normalized;

        if (_movement != Vector2.zero)
            _lastMoveDir = _movement;

        SetAnimFloat("Speed", _movement.sqrMagnitude);

        if (_movement.x != 0)
            _facingDirection = _movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(_facingDirection, 1);

        // Dash : Space bar
        if (Input.GetKeyDown(KeyCode.Space) && _dashCooldownTimer <= 0f && _movement != Vector2.zero)
            StartCoroutine(DashCoroutine(_movement));

        // Possess : E key, adjacent enemy required
        if (Input.GetKeyDown(KeyCode.E))
            TryPossessAdjacent();
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing || IsDashing) return;
        _rb.velocity = _movement * speed; // Charan 
    }

    
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

    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null) return;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(
            transform.position, possessionRange, LayerMask.GetMask("Enemy"));

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
            PossessionSystem.Instance.TryPossessTarget(closest); // Terry 
    }

    public void TakeDamage(float amount) // Terry 
    {
        if (IsDead || IsInvincible) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.RoundToInt(amount));
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth); // Gagan 
        if (CurrentHealth <= 0) Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead       = true;
        _rb.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        OnPlayerDied?.Invoke(); // Gagan + Charan 
    }

    public void OnPossessionStart(EnemyController possessed) // Terry 
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

    public void SetMaxHealth(int value) // Charan 
    {
        maxHealth     = value;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetSpeed(float value) => speed = value; // Charan 

    public void Heal(int amount) // Charan 
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void SetAnimFloat(string param, float value)
    {
        if (_anim != null) _anim.SetFloat(param, value);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRange);
    }
}
