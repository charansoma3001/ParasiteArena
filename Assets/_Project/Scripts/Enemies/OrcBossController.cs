using System.Collections;
using UnityEngine;

/// <summary>
/// Add this component to the Orc Boss GameObject alongside
/// EnemyController and EnemyAI. It monitors HP to drive the
/// three-phase progression and scales movement/attack stats
/// automatically at each threshold.
///
/// EnemyStats setup required:
///   enemyType  = Boss
///   isBoss     = true          (prevents player from possessing it)
///   maxHealth  = e.g. 600
///   attackDamage, attackCooldown, detectionRange as desired
///
/// Leave the three RuntimeAnimatorController slots empty if you
/// only have one animator — the boss will just keep using it.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyAI))]
public class OrcBossController : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Phase definition
    // ---------------------------------------------------------------
    public enum BossPhase { Phase1, Phase2, Phase3 }

    [Header("Phase HP Thresholds (fraction of max HP)")]
    [Tooltip("Phase 2 begins when HP falls below this fraction (e.g. 0.66 = 66 %).")]
    public float phase2Threshold = 0.66f;
    [Tooltip("Phase 3 begins when HP falls below this fraction (e.g. 0.33 = 33 %).")]
    public float phase3Threshold = 0.33f;

    // ---------------------------------------------------------------
    // Per-phase animator controllers
    // Assign the three Animator Controllers you create from the orc
    // spritesheet rows. Leaving a slot null keeps the current one.
    // ---------------------------------------------------------------
    [Header("Per-Phase Animator Controllers (optional)")]
    [Tooltip("Animator Controller for row 0 of the spritesheet (full health).")]
    public RuntimeAnimatorController phase1AnimController;
    [Tooltip("Animator Controller for row 1 of the spritesheet (mid health).")]
    public RuntimeAnimatorController phase2AnimController;
    [Tooltip("Animator Controller for row 2 of the spritesheet (low health).")]
    public RuntimeAnimatorController phase3AnimController;

    // ---------------------------------------------------------------
    // Speed multipliers — OrcBossController modifies EnemyAI public
    // fields directly at runtime so no extra stats asset is needed.
    // ---------------------------------------------------------------
    [Header("Phase 2 – Enraged")]
    public float phase2StepDurationMult = 0.75f;  // faster steps
    public float phase2StepCooldownMult = 0.75f;
    public float phase2AttackCooldownMult = 0.70f;

    [Header("Phase 3 – Berserker")]
    public float phase3StepDurationMult = 0.55f;  // even faster
    public float phase3StepCooldownMult = 0.55f;
    public float phase3AttackCooldownMult = 0.50f;

    // ---------------------------------------------------------------
    // Special Phase 3 shockwave — spawn a prefab at the boss position
    // on a repeating interval (e.g. a CircleCollider2D trigger that
    // deals damage to whatever is inside it).
    // ---------------------------------------------------------------
    [Header("Phase 3 – Shockwave (optional)")]
    [Tooltip("Prefab spawned periodically during Phase 3. " +
             "Should be a self-destroying trigger that deals damage.")]
    public GameObject shockwavePrefab;
    [Tooltip("Seconds between shockwave pulses.")]
    public float shockwaveInterval = 4f;

    // ---------------------------------------------------------------
    // Transition VFX
    // ---------------------------------------------------------------
    [Header("VFX")]
    [Tooltip("Instantiated at the boss position on each phase change.")]
    public GameObject phaseTransitionVFXPrefab;
    [Tooltip("Duration of the Stunned pause during a phase transition.")]
    public float transitionStunDuration = 1.5f;

    // ---------------------------------------------------------------
    // Public state
    // ---------------------------------------------------------------
    public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

    // ---------------------------------------------------------------
    // Private references
    // ---------------------------------------------------------------
    private EnemyController _ctrl;
    private EnemyAI         _ai;
    private Animator        _anim;

    // Baseline stats captured once on Start so we can scale relative to them.
    private float _baseStepDuration;
    private float _baseStepCooldown;
    private float _baseAttackCooldown;

    // Guard against triggering the same transition twice.
    private bool _transitioning;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------
    private void Awake()
    {
        _ctrl = GetComponent<EnemyController>();
        _ai   = GetComponent<EnemyAI>();
        _anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // Capture baselines AFTER the host EnemyController has been
        // initialised (it sets stats in Awake/Init).
        _baseStepDuration   = _ai.stepDuration;
        _baseStepCooldown   = _ai.stepCooldown;
        _baseAttackCooldown = _ctrl.stats.attackCooldown;

        // Apply Phase 1 animator if assigned.
        SwitchAnimator(BossPhase.Phase1);
    }

    private void Update()
    {
        if (_ctrl.IsDead || _transitioning) return;
        CheckPhaseTransition();
    }

    // ---------------------------------------------------------------
    // Phase transition logic
    // ---------------------------------------------------------------
    private void CheckPhaseTransition()
    {
        float hpPct = _ctrl.CurrentHP / _ctrl.stats.maxHealth;

        BossPhase desired = hpPct > phase2Threshold ? BossPhase.Phase1
                          : hpPct > phase3Threshold ? BossPhase.Phase2
                                                    : BossPhase.Phase3;

        if (desired != CurrentPhase)
            StartCoroutine(TransitionToPhase(desired));
    }

    private IEnumerator TransitionToPhase(BossPhase next)
    {
        _transitioning = true;
        CurrentPhase   = next;

        // Brief stun so the player has a moment to react.
        _ctrl.SetState(EnemyController.EnemyState.Stunned);

        if (phaseTransitionVFXPrefab)
            Instantiate(phaseTransitionVFXPrefab, transform.position, Quaternion.identity);

        yield return new WaitForSeconds(transitionStunDuration);

        // Apply the new phase's stat scaling.
        ApplyPhaseStats(next);

        // Switch the animator to the correct spritesheet row.
        SwitchAnimator(next);

        // Resume chasing — unless the boss somehow died during the stun.
        if (!_ctrl.IsDead)
            _ctrl.SetState(EnemyController.EnemyState.Chasing);

        _transitioning = false;

        // If this is Phase 3, kick off the shockwave loop.
        if (next == BossPhase.Phase3 && shockwavePrefab != null)
            StartCoroutine(ShockwaveLoop());
    }

    // ---------------------------------------------------------------
    // Stat scaling
    // Modifying EnemyAI public fields and EnemyStats.attackCooldown at
    // runtime is intentional — it lets the Inspector tunables act as
    // Phase 1 baselines and the multipliers do the rest.
    // ---------------------------------------------------------------
    private void ApplyPhaseStats(BossPhase phase)
    {
        switch (phase)
        {
            case BossPhase.Phase1:
                _ai.stepDuration           = _baseStepDuration;
                _ai.stepCooldown           = _baseStepCooldown;
                _ctrl.stats.attackCooldown = _baseAttackCooldown;
                break;

            case BossPhase.Phase2:
                _ai.stepDuration           = _baseStepDuration   * phase2StepDurationMult;
                _ai.stepCooldown           = _baseStepCooldown   * phase2StepCooldownMult;
                _ctrl.stats.attackCooldown = _baseAttackCooldown * phase2AttackCooldownMult;
                break;

            case BossPhase.Phase3:
                _ai.stepDuration           = _baseStepDuration   * phase3StepDurationMult;
                _ai.stepCooldown           = _baseStepCooldown   * phase3StepCooldownMult;
                _ctrl.stats.attackCooldown = _baseAttackCooldown * phase3AttackCooldownMult;
                break;
        }
    }

    // ---------------------------------------------------------------
    // Animator switching
    // Each RuntimeAnimatorController corresponds to one row of the
    // orc spritesheet (a separate Animator Controller asset you create
    // in the Editor — see OrcBossSetupGuide.md).
    // ---------------------------------------------------------------
    private void SwitchAnimator(BossPhase phase)
    {
        if (_anim == null) return;

        RuntimeAnimatorController target = phase switch
        {
            BossPhase.Phase1 => phase1AnimController,
            BossPhase.Phase2 => phase2AnimController,
            BossPhase.Phase3 => phase3AnimController,
            _                => null
        };

        if (target != null)
            _anim.runtimeAnimatorController = target;
    }

    // ---------------------------------------------------------------
    // Phase 3 shockwave loop
    // Spawns the shockwave prefab every `shockwaveInterval` seconds
    // until the boss dies or leaves Phase 3.
    // ---------------------------------------------------------------
    private IEnumerator ShockwaveLoop()
    {
        while (!_ctrl.IsDead && CurrentPhase == BossPhase.Phase3)
        {
            yield return new WaitForSeconds(shockwaveInterval);
            if (!_ctrl.IsDead && CurrentPhase == BossPhase.Phase3)
                Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
        }
    }

    // ---------------------------------------------------------------
    // Gizmo — shows phase thresholds as HP arcs in the Scene view.
    // ---------------------------------------------------------------
    private void OnDrawGizmos()
    {
        // Draw a small coloured disc above the boss to indicate phase.
        Color c = CurrentPhase switch
        {
            BossPhase.Phase1 => new Color(0f, 1f, 0f, 0.4f),
            BossPhase.Phase2 => new Color(1f, 0.6f, 0f, 0.4f),
            BossPhase.Phase3 => new Color(1f, 0f, 0f, 0.4f),
            _                => Color.white
        };
        Gizmos.color = c;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}