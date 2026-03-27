using System.Collections;
using UnityEngine;


[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyAI))]
public class OrcBossController : MonoBehaviour
{
  
    public enum BossPhase { Phase1, Phase2, Phase3 }

    [Header("Phase HP Thresholds (fraction of max HP)")]
    [Tooltip("Phase 2 begins when HP falls below this fraction (e.g. 0.66 = 66 %).")]
    public float phase2Threshold = 0.66f;
    [Tooltip("Phase 3 begins when HP falls below this fraction (e.g. 0.33 = 33 %).")]
    public float phase3Threshold = 0.33f;

    [Header("Per-Phase Animator Controllers (optional)")]
    [Tooltip("Animator Controller for row 0 of the spritesheet (full health).")]
    public RuntimeAnimatorController phase1AnimController;
    [Tooltip("Animator Controller for row 1 of the spritesheet (mid health).")]
    public RuntimeAnimatorController phase2AnimController;
    [Tooltip("Animator Controller for row 2 of the spritesheet (low health).")]
    public RuntimeAnimatorController phase3AnimController;

  
    [Header("Phase 2 – Enraged")]
    public float phase2StepDurationMult = 0.75f; 
    public float phase2StepCooldownMult = 0.75f;
    public float phase2AttackCooldownMult = 0.70f;

    [Header("Phase 3 – Berserker")]
    public float phase3StepDurationMult = 0.55f; 
    public float phase3StepCooldownMult = 0.55f;
    public float phase3AttackCooldownMult = 0.50f;

   
    [Header("Phase 3 – Shockwave (optional)")]
    [Tooltip("Prefab spawned periodically during Phase 3. " +
             "Should be a self-destroying trigger that deals damage.")]
    public GameObject shockwavePrefab;
    [Tooltip("Seconds between shockwave pulses.")]
    public float shockwaveInterval = 4f;

   
    [Header("VFX")]
    [Tooltip("Instantiated at the boss position on each phase change.")]
    public GameObject phaseTransitionVFXPrefab;
    [Tooltip("Duration of the Stunned pause during a phase transition.")]
    public float transitionStunDuration = 1.5f;

    
    public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

    private EnemyController _ctrl;
    private EnemyAI         _ai;
    private Animator        _anim;

    private float _baseStepDuration;
    private float _baseStepCooldown;
    private float _baseAttackCooldown;

    private bool _transitioning;

   
    private void Awake()
    {
        _ctrl = GetComponent<EnemyController>();
        _ai   = GetComponent<EnemyAI>();
        _anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
       
        _baseStepDuration   = _ai.stepDuration;
        _baseStepCooldown   = _ai.stepCooldown;
        _baseAttackCooldown = _ctrl.stats.attackCooldown;

        SwitchAnimator(BossPhase.Phase1);
    }

    private void Update()
    {
        if (_ctrl.IsDead || _transitioning) return;
        CheckPhaseTransition();
    }

    
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

        _ctrl.SetState(EnemyController.EnemyState.Stunned);

        if (phaseTransitionVFXPrefab)
            Instantiate(phaseTransitionVFXPrefab, transform.position, Quaternion.identity);

        yield return new WaitForSeconds(transitionStunDuration);

        ApplyPhaseStats(next);

        SwitchAnimator(next);

        if (!_ctrl.IsDead)
            _ctrl.SetState(EnemyController.EnemyState.Chasing);

        _transitioning = false;

        if (next == BossPhase.Phase3 && shockwavePrefab != null)
            StartCoroutine(ShockwaveLoop());
    }

  
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

  
    private IEnumerator ShockwaveLoop()
    {
        while (!_ctrl.IsDead && CurrentPhase == BossPhase.Phase3)
        {
            yield return new WaitForSeconds(shockwaveInterval);
            if (!_ctrl.IsDead && CurrentPhase == BossPhase.Phase3)
                Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
        }
    }


    private void OnDrawGizmos()
    {
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