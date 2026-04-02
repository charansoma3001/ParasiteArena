using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed { get; private set; }
    public float dashSpeed { get; private set; }
    public float dashDuration { get; private set; }
    public float dashCooldown { get; private set; }
    public int maxHealth { get; private set; }
    public float possessionRange { get; private set; }

    public float AttackDamage { get; private set; }
    public float HostDecayRate { get; private set; } = 1f;

    public int  CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsDashing { get; private set; }
    public bool IsInvincible  { get; private set; }
    public bool IsPossessing => PossessionSystem.Instance != null && PossessionSystem.Instance.IsPossessing;

    public static event System.Action<int, int> OnHealthChanged;
    public static event System.Action OnPlayerDied;

    private Rigidbody2D rigidbody;
    private Animator animation;
    private SpriteRenderer spriteRendere;
    private Collider2D collider;
    private PlayerIndicator playerIndicator;

    private Vector2 movement;
    private Vector2 lastMoveDirection = Vector2.down;
    private int facingDirection   = 1;
    private float dashCooldownTime;

    // Held-movement repeat timer for possessed enemy control
    private float possessedMoveTmer;
    private const float PossessedMoveInterval = 0.22f;

    private void Awake()
    {
        if (!CompareTag("Player"))
        {
            enabled = false;
            return;
        }

        rigidbody = GetComponent<Rigidbody2D>();
        animation = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        spriteRendere = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        collider = GetComponent<Collider2D>();
        playerIndicator = FindFirstObjectByType<PlayerIndicator>();

        var pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;
    }

    private void Start()
    {
        if (rigidbody == null) return;
        rigidbody.freezeRotation = true;
        rigidbody.gravityScale   = 0f;

        if (StatManager.Instance != null)
        {
            StatManager.Instance.ForceApplyStats();
        }

        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        rigidbody.position = new Vector2(50, 50);
    }

    private void Update()
    {
        if (IsDead) return;

        if (IsPossessing)
        {
            var possessed = PossessionSystem.Instance.PossessedEnemy;
            if (possessed != null)
                transform.position = possessed.transform.position;

            possessedMoveTmer -= Time.deltaTime;
            Vector2 heldDir = GetHeldDirection();
            if (heldDir != Vector2.zero && possessedMoveTmer <= 0f)
            {
                PossessionSystem.Instance.MovePossessedEnemy(heldDir);
                possessedMoveTmer = PossessedMoveInterval;
            }
            else if (heldDir == Vector2.zero)
            {
                possessedMoveTmer = 0f;
            }

            if (Input.GetKeyDown(KeyCode.Space))
                PossessionSystem.Instance.UsePossessedAbility();
            if (Input.GetKeyDown(KeyCode.R))
                PossessionSystem.Instance.UsePossessedBlock();
            if (Input.GetKeyDown(KeyCode.E))
                PossessionSystem.Instance.EndPossession();

            return;
        }

        if (IsDashing) return;

        dashCooldownTime -= Time.deltaTime;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        movement = new Vector2(h, v).normalized;

        if (movement != Vector2.zero) lastMoveDirection = movement;

        SetAnimFloat("Speed", movement.sqrMagnitude);

        if (movement.x != 0)
            facingDirection = movement.x > 0 ? -1 : 1;
        transform.localScale = new Vector2(facingDirection, 1);

        if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTime <= 0f)
            StartCoroutine(DashCoroutine(lastMoveDirection));

        if (Input.GetKeyDown(KeyCode.E))
            TryPossessAdjacent();

        if (Input.GetKeyDown(KeyCode.H)) Heal(20);
    }

    private Vector2 GetHeldDirection()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return Vector2.up;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return Vector2.down;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return Vector2.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return Vector2.right;
        return Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (IsDead || IsPossessing || IsDashing) return;
        rigidbody.velocity = movement * speed;
    }

    private IEnumerator DashCoroutine(Vector2 dir)
    {
        IsDashing = true;
        IsInvincible = true;
        dashCooldownTime = dashCooldown;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rigidbody.velocity = dir * dashSpeed;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rigidbody.velocity = Vector2.zero;
        IsDashing = false;
        yield return null;
        IsInvincible = false;
    }

    private void TryPossessAdjacent()
    {
        if (PossessionSystem.Instance == null)
        {
            return;
        }

        int layerIdx = LayerMask.NameToLayer("Enemy");
        if (layerIdx < 0)
        {
            return;
        }

        var cols = Physics2D.OverlapCircleAll(transform.position, possessionRange, 1 << layerIdx);
        EnemyController best = null;
        float closest = float.MaxValue;

        foreach (var col in cols)
        {
            var ec = col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead || ec.IsPossessed || ec.stats.isBoss) continue;
            float d = Vector2.Distance(transform.position, ec.transform.position);
            if (d < closest) { closest = d; best = ec; }
        }

        if (best != null)
            PossessionSystem.Instance.TryPossessTarget(best);
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
            animation.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        rigidbody.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        animation.SetTrigger("Die");
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float deathAnimLength = animation.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSecondsRealtime(deathAnimLength + 1f);

        OnPlayerDied?.Invoke();
    }

    public void OnPossessionStart(EnemyController possessed)
    {
        if (spriteRendere) spriteRendere.enabled  = false;
        if (collider) collider.enabled = false;
        if (playerIndicator) playerIndicator.gameObject.SetActive(false);
        rigidbody.velocity = Vector2.zero;
        SetAnimFloat("Speed", 0);
        transform.position = possessed.transform.position;
        possessedMoveTmer = 0f;
    }

    public void OnPossessionEnd(Vector3 returnPos)
    {
        transform.position = returnPos;
        if (spriteRendere) spriteRendere.enabled = true;
        if (collider) collider.enabled = true;
        if (playerIndicator) playerIndicator.gameObject.SetActive(true);
    }

    private void OnEnable() => PossessionSystem.OnPossessionChanged += HandlePossessionChanged;
    private void OnDisable() => PossessionSystem.OnPossessionChanged -= HandlePossessionChanged;

    private void HandlePossessionChanged(bool started, EnemyController enemy)
    {
        if (!started) OnPossessionEnd(enemy.transform.position);
    }

    public void SetDashSpeed(float value) => dashSpeed = value;
    public void SetDashDuration(float value) => dashDuration = value;
    public void SetPossessionRange(float value) => possessionRange = value;
    public void SetDashCooldown(float value) => dashCooldown = value;
    public void SetAttackDamage(float value) => AttackDamage = value;
    public void SetHostDecayRate(float value) => HostDecayRate = value;

    public void SetMaxHealth(int value)
    {
        maxHealth = value;
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

    private void SetAnimFloat(string p, float v) 
    { 
        if (animation) animation.SetFloat(p, v); 
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, possessionRange);
    }
}