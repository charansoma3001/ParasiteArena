using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyAI))]
public class ExecutionerBossController : MonoBehaviour
{
    [Header("HP Thresholds (fraction of max HP)")]
    [Tooltip("Phase 2 begins below this fraction — boss gets faster and summons more.")]
    public float phase2Threshold = 0.60f;
    [Tooltip("Phase 3 begins below this fraction — Skill1 spam, constant summoning.")]
    public float phase3Threshold = 0.30f;

    [Header("Phase 2 — Enraged")]
    public float p2StepDurationMult    = 0.80f;
    public float p2StepCooldownMult    = 0.80f;
    public float p2AttackCooldownMult  = 0.75f;

    [Header("Phase 3 — Berserk")]
    public float p3StepDurationMult    = 0.60f;
    public float p3StepCooldownMult    = 0.60f;
    public float p3AttackCooldownMult  = 0.50f;

   
    [Header("Summoning")]
    [Tooltip("Prefab for the spirit minion (should carry its own EnemyController + EnemyAI).")]
    public GameObject spiritPrefab;
    [Tooltip("How many spirits to spawn per summon event.")]
    public int spiritsPerSummon = 2;
    [Tooltip("Max spirits alive at once — boss won't summon beyond this.")]
    public int maxSpirits = 4;

    [Tooltip("Seconds between summon events in Phase 1.")]
    public float summonIntervalP1 = 14f;
    [Tooltip("Seconds between summon events in Phase 2.")]
    public float summonIntervalP2 = 9f;
    [Tooltip("Seconds between summon events in Phase 3.")]
    public float summonIntervalP3 = 5f;

    [Tooltip("Duration of the Stunned pause while the Summoning animation plays.")]
    public float summonAnimDuration = 0.9f;

 
    [Header("Skill1 — Scythe Slam")]
    [Tooltip("Seconds between Skill1 uses in Phase 2.")]
    public float skill1IntervalP2 = 12f;
    [Tooltip("Seconds between Skill1 uses in Phase 3.")]
    public float skill1IntervalP3 = 6f;

    [Tooltip("Radius of the slam hit (in world units).")]
    public float skill1Radius = 1.4f;
    [Tooltip("Damage multiplier on top of stats.attackDamage.")]
    public float skill1DamageMult = 1.8f;
    [Tooltip("Wind-up seconds before the hit lands.")]
    public float skill1WindUp = 0.55f;

    [Tooltip("Optional danger-tile prefab shown during wind-up.")]
    public GameObject skill1TilePrefab;

    [Header("Phase Transition")]
    public GameObject phaseTransitionVFXPrefab;
    public float      transitionStunDuration = 1.2f;

   
    public int CurrentPhase { get; private set; } = 1;

    private EnemyController _ctrl;
    private EnemyAI         _ai;
    private EnemyAnimator   _anim;

    private float _baseStepDuration;
    private float _baseStepCooldown;
    private float _baseAttackCooldown;

    private bool  _transitioning;
    private int   _activeSpirits;

    private bool  _summonLoopRunning;
    private bool  _skill1LoopRunning;

    private void Awake()
    {
        _ctrl = GetComponent<EnemyController>();
        _ai   = GetComponent<EnemyAI>();
        _anim = GetComponentInChildren<EnemyAnimator>();
    }

    private void Start()
    {
        _baseStepDuration   = _ai.stepDuration;
        _baseStepCooldown   = _ai.stepCooldown;
        _baseAttackCooldown = _ctrl.stats.attackCooldown;

        StartCoroutine(SummonLoop());
    }

    private void Update()
    {
        if (_ctrl.IsDead || _transitioning) return;
        CheckPhaseEscalation();
    }


    private void CheckPhaseEscalation()
    {
        float hpPct = _ctrl.CurrentHP / _ctrl.stats.maxHealth;

        int desired = hpPct > phase2Threshold ? 1
                    : hpPct > phase3Threshold ? 2
                                              : 3;

        if (desired > CurrentPhase)
            StartCoroutine(EscalateTo(desired));
    }

    private IEnumerator EscalateTo(int phase)
    {
        _transitioning = true;
        CurrentPhase   = phase;

        _ctrl.SetState(EnemyController.EnemyState.Stunned);
        if (phaseTransitionVFXPrefab)
            Instantiate(phaseTransitionVFXPrefab, transform.position, Quaternion.identity);

        yield return new WaitForSeconds(transitionStunDuration);

        ApplyPhaseStats(phase);

        if (!_ctrl.IsDead)
            _ctrl.SetState(EnemyController.EnemyState.Chasing);

        _transitioning = false;

        if (phase >= 2 && !_skill1LoopRunning)
            StartCoroutine(Skill1Loop());
    }

    private void ApplyPhaseStats(int phase)
    {
        switch (phase)
        {
            case 1:
                _ai.stepDuration           = _baseStepDuration;
                _ai.stepCooldown           = _baseStepCooldown;
                _ctrl.stats.attackCooldown = _baseAttackCooldown;
                break;
            case 2:
                _ai.stepDuration           = _baseStepDuration   * p2StepDurationMult;
                _ai.stepCooldown           = _baseStepCooldown   * p2StepCooldownMult;
                _ctrl.stats.attackCooldown = _baseAttackCooldown * p2AttackCooldownMult;
                break;
            case 3:
                _ai.stepDuration           = _baseStepDuration   * p3StepDurationMult;
                _ai.stepCooldown           = _baseStepCooldown   * p3StepCooldownMult;
                _ctrl.stats.attackCooldown = _baseAttackCooldown * p3AttackCooldownMult;
                break;
        }
    }


    private IEnumerator SummonLoop()
    {
        _summonLoopRunning = true;

        yield return new WaitForSeconds(summonIntervalP1 * 0.5f);

        while (!_ctrl.IsDead)
        {
            float interval = CurrentPhase switch
            {
                1 => summonIntervalP1,
                2 => summonIntervalP2,
                _ => summonIntervalP3
            };
            yield return new WaitForSeconds(interval);

            if (_ctrl.IsDead) break;
            if (_activeSpirits >= maxSpirits) continue;
            if (_ctrl.CurrentState == EnemyController.EnemyState.Attacking) continue;

            yield return StartCoroutine(PerformSummon());
        }

        _summonLoopRunning = false;
    }

    private IEnumerator PerformSummon()
    {
        _ctrl.SetState(EnemyController.EnemyState.Stunned);
        _anim?.Play("summon");

        yield return new WaitForSeconds(summonAnimDuration);

        if (_ctrl.IsDead) yield break;

        int toSpawn = Mathf.Min(spiritsPerSummon, maxSpirits - _activeSpirits);
        Vector2[] offsets =
        {
            Vector2.up    * _ai.tileSize * 1.5f,
            Vector2.down  * _ai.tileSize * 1.5f,
            Vector2.left  * _ai.tileSize * 1.5f,
            Vector2.right * _ai.tileSize * 1.5f,
        };

        for (int i = 0; i < toSpawn && i < offsets.Length; i++)
        {
            if (spiritPrefab == null) break;

            Vector3 pos = transform.position + (Vector3)offsets[i];
            var spirit  = Instantiate(spiritPrefab, pos, Quaternion.identity);

            _activeSpirits++;

            var ec = spirit.GetComponent<EnemyController>();
            if (ec != null)
            {
                System.Action<EnemyController> onDied = null;
                onDied = dead =>
                {
                    if (dead != ec) return;
                    _activeSpirits = Mathf.Max(0, _activeSpirits - 1);
                    EnemyController.OnEnemyDied -= onDied;
                };
                EnemyController.OnEnemyDied += onDied;

                ec.Init();
            }
        }

        if (!_ctrl.IsDead)
            _ctrl.SetState(EnemyController.EnemyState.Chasing);
    }


    private IEnumerator Skill1Loop()
    {
        _skill1LoopRunning = true;

        yield return new WaitForSeconds(CurrentPhase >= 3 ? skill1IntervalP3 * 0.4f
                                                          : skill1IntervalP2 * 0.5f);

        while (!_ctrl.IsDead)
        {
            float interval = CurrentPhase >= 3 ? skill1IntervalP3 : skill1IntervalP2;
            yield return new WaitForSeconds(interval);

            if (_ctrl.IsDead) break;
            if (_ctrl.CurrentState == EnemyController.EnemyState.Attacking) continue;

            yield return StartCoroutine(PerformSkill1());
        }

        _skill1LoopRunning = false;
    }

    private IEnumerator PerformSkill1()
    {
        _ctrl.SetState(EnemyController.EnemyState.Attacking);
        _anim?.Play("Skill1");

        GameObject tile = null;
        if (skill1TilePrefab != null)
        {
            tile = Instantiate(skill1TilePrefab, transform.position, Quaternion.identity);
            tile.transform.localScale = Vector3.one * skill1Radius * 2f;
        }

        yield return new WaitForSeconds(skill1WindUp);

        float dmg = _ctrl.stats.attackDamage * skill1DamageMult;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, skill1Radius))
        {
            col.GetComponent<PlayerController>()?.TakeDamage(dmg);

            var ec = col.GetComponent<EnemyController>();
            if (ec != null && ec != _ctrl && ec.IsPossessed)
                ec.TakeDamage(dmg);
        }

        yield return new WaitForSeconds(0.15f);
        if (tile) Destroy(tile);

        if (!_ctrl.IsDead)
            _ctrl.SetState(EnemyController.EnemyState.Chasing);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, skill1Radius);
    }
}