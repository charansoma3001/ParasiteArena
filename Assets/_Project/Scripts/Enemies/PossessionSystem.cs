using UnityEngine;

// SETUP: place this on its own empty GameObject (e.g. "Systems").
// Do NOT put it on the Player — the player hides during possession.
public class PossessionSystem : MonoBehaviour
{
    public static PossessionSystem Instance { get; private set; }

    public bool            IsPossessing   { get; private set; }
    public EnemyController PossessedEnemy { get; private set; }

    private Transform _player;
    private float     _bonusPossessionTime; // Charan - ItemSystem

    // (true, enemy) = possession started   (false, enemy) = possession ended
    public static event System.Action<bool, EnemyController> OnPossessionChanged; // Gagan - UIManager

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go) _player = go.transform;
        else Debug.LogWarning("[PossessionSystem] No object tagged 'Player' found.");
    }

    // Called by PlayerController.TryPossessAdjacent with the validated target
    public bool TryPossessTarget(EnemyController target)
    {
        if (IsPossessing) return false;
        if (target == null || target.IsDead || target.IsPossessed || target.stats.isBoss) return false;
        BeginPossession(target);
        return true;
    }

    // Called by PlayerController.Update (Q key)
    public bool UsePossessedAbility()
    {
        if (!IsPossessing || PossessedEnemy == null) return false;
        return PossessedEnemy.UsePossessedAbility();
    }

    // Called by PlayerController.Update (R key)
    public void UsePossessedBlock()
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.UsePossessedBlock();
    }

    // Called by PlayerController.Update (E/Escape) or EnemyController (timer expired)
    public void EndPossession()
    {
        if (!IsPossessing) return;
        var released   = PossessedEnemy;
        IsPossessing   = false;
        PossessedEnemy = null;
        released.SetState(EnemyController.EnemyState.Chasing);
        // Event fires BEFORE state change so PlayerController.OnPossessionEnd
        // can read released.transform.position as the player's return point
        OnPossessionChanged?.Invoke(false, released); // Gagan - UIManager
    }

    // Called by PlayerController.Update (WASD while possessing) — one tile step per key press
    public void MovePossessedEnemy(Vector2 cardinalDir)
    {
        if (!IsPossessing || PossessedEnemy == null) return;
        PossessedEnemy.GetComponent<EnemyAI>()?.StepInDirection(cardinalDir);
        // Keep player Transform co-located so camera follow and possession range stay correct
        if (_player) _player.position = PossessedEnemy.transform.position;
    }

    // Charan - ItemSystem: relic that extends possession duration
    public void SetPossessionBonusTime(float bonus) => _bonusPossessionTime = bonus;

    private void BeginPossession(EnemyController target)
    {
        IsPossessing   = true;
        PossessedEnemy = target;
        target.stats.possessionDuration += _bonusPossessionTime;
        target.SetState(EnemyController.EnemyState.Possessed);
        _player?.GetComponent<PlayerController>()?.OnPossessionStart(target); // Gagan
        OnPossessionChanged?.Invoke(true, target); // Gagan - UIManager
    }
}
