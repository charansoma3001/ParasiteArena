using UnityEngine;

// ── SETUP: place this on its own empty GameObject called "Systems" or "GameManager"
// Do NOT put this on the Player — the Player hides during possession and would break the singleton.
public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _playerTransform;
    private float     _bonusPossessionTime; // Charan - ItemSystem (relics)

    // Fired with (true, enemy) on start, (false, enemy) on end
    public static event System.Action<bool, EnemyController> OnPossessionChanged; // Gagan - UIManager

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) _playerTransform = playerGO.transform;
        else Debug.LogWarning("PossessionSystem: no GameObject tagged 'Player' found in scene.");
    }

    // ── Called by PlayerController.TryPossessAdjacent ────────────────────
    public bool TryPossessTarget(EnemyController target)
    {
        if (IsPossessing)
        {
            Debug.Log("PossessionSystem: already possessing.");
            return false;
        }
        if (target == null || target.IsDead || target.IsPossessed || target.stats.isBoss)
        {
            Debug.Log("PossessionSystem: target invalid.");
            return false;
        }
        BeginPossession(target);
        return true;
    }

    // ── Called by PlayerController.Update each key-press while possessing ─
    // Routes to EnemyAI.StepInDirection — one tile step per call
    public void MovePossessedEnemy(Vector2 cardinalInput)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        var ai = PossessedEnemy.GetComponent<EnemyAI>();
        ai?.StepInDirection(cardinalInput);

        // Keep player Transform co-located with the possessed enemy
        // so the possession range gizmo and camera follow correctly
        if (_playerTransform != null)
            _playerTransform.position = PossessedEnemy.transform.position;
    }

    // ── Called by PlayerController.Update (Q key) ─────────────────────────
    public bool UsePossessedAbility()
    {
        if (!IsPossessing || PossessedEnemy == null) return false;
        return PossessedEnemy.UsePossessedAbility();
    }

    // ── Called by PlayerController.Update (R key) ─────────────────────────
    public void UsePossessedBlock()
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.UsePossessedBlock();
    }

    // ── Called by PlayerController (E / Escape) or EnemyController (timer) ─
    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released   = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;

        // Return enemy to its own AI
        released.SetState(EnemyController.EnemyState.Chasing);

        // Event fires BEFORE the enemy transform moves so PlayerController.OnPossessionEnd
        // can read the released enemy's position as the player's return point
        OnPossessionChanged?.Invoke(false, released); // Gagan - UIManager
    }

    // Charan - ItemSystem: relic that extends possession duration
    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    // ─────────────────────────────────────────────────────────────────────
    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;

        // Apply relic bonus once at start (not per-frame)
        target.stats.possessionDuration += _bonusPossessionTime;

        // Put enemy into Possessed state — stops its own AI
        target.SetState(EnemyController.EnemyState.Possessed);

        // Tell player to hide and move to the enemy position
        _playerTransform?.GetComponent<PlayerController>()?.OnPossessionStart(target); // Gagan

        OnPossessionChanged?.Invoke(true, target); // Gagan - UIManager
    }
}
