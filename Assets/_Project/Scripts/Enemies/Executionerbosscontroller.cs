using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyAI))]
public class ExecutionerBossController : MonoBehaviour
{
    [Header("HP Thresholds (fraction of max HP)")]
    public float phase2Threshold = 0.60f;
    public float phase3Threshold = 0.30f;

    [Header("Phase 2")]
    public float p2StepDurationMult = 0.80f;
    public float p2StepCooldownMult = 0.80f;
    public float p2AttackCooldownMult = 0.75f;

    [Header("Phase 3")]
    public float p3StepDurationMult = 0.60f;
    public float p3StepCooldownMult = 0.60f;
    public float p3AttackCooldownMult = 0.50f;

   
    [Header("Summoning")]
    public GameObject spiritPrefab;
    public int spiritsPerSummon = 2;
    public int maxSpirits = 10;

    public float summonIntervalP1 = 14f;
    public float summonIntervalP2 = 9f;
    public float summonIntervalP3 = 5f;

    public float summonAnimDuration = 0.9f;

 
    [Header("Skill1")]
    public float skill1IntervalP2 = 12f;
    public float skill1IntervalP3 = 6f;

    public float skill1Radius = 1.4f;
    public float skill1DamageMult = 1.8f;
    public float skill1WindUp = 0.55f;
    public GameObject skill1TilePrefab;

    [Header("Phase Transition")]
    public AudioClip enragedSfx;
    public AudioClip berzerkedSfx;
    public float transitionStunDuration = 1.2f;

   
    public int CurrentPhase { get; private set; } = 1;

    private EnemyController ctrl;
    private EnemyAI ai;
    private EnemyAnimator anim;

    private float baseStepDuration;
    private float baseStepCooldown;
    private float baseAttackCooldown;

    private bool transitioning;
    private int activeSpirits;

    private bool summonLoopRunning;
    private bool skill1LoopRunning;

    private void Awake()
    {
        ctrl = GetComponent<EnemyController>();
        ai = GetComponent<EnemyAI>();
        anim = GetComponentInChildren<EnemyAnimator>();
    }

    private void Start()
    {
        baseStepDuration = ai.stepDuration;
        baseStepCooldown = ai.stepCooldown;
        baseAttackCooldown = ctrl.stats.attackCooldown;

        StartCoroutine(SummonLoop());
        
        EnemyController.OnEnemyDied += OnBossDied;
    }

    private void Update()
    {
        if (ctrl.IsDead || transitioning) return;
        CheckPhaseEscalation();
    }

    private void OnDisable()
    {
        EnemyController.OnEnemyDied -= OnBossDied;
    }


    private void CheckPhaseEscalation()
    {
        float hpPct = ctrl.CurrentHP / ctrl.stats.maxHealth;

        int desired = hpPct > phase2Threshold ? 1
                    : hpPct > phase3Threshold ? 2
                                              : 3;

        if (desired > CurrentPhase)
            StartCoroutine(EscalateTo(desired));
    }

    private void OnBossDied(EnemyController deadEnemy)
    {
        if (deadEnemy != ctrl) return;

        AudioManager.Instance?.PlayGameWin();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadWinScreen();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("03_GameWin");
        }
    }

    private IEnumerator EscalateTo(int phase)
    {
        transitioning = true;
        CurrentPhase = phase;

        ctrl.SetState(EnemyController.EnemyState.Stunned);
        
        if (phase == 2 && enragedSfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXAtPos(enragedSfx, transform.position);
        else if (phase == 3 && berzerkedSfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXAtPos(berzerkedSfx, transform.position);

        yield return new WaitForSeconds(transitionStunDuration);

        ApplyPhaseStats(phase);

        if (!ctrl.IsDead)
            ctrl.SetState(EnemyController.EnemyState.Chasing);

        transitioning = false;

        if (phase >= 2 && !skill1LoopRunning)
            StartCoroutine(Skill1Loop());
    }

    private void ApplyPhaseStats(int phase)
    {
        switch (phase)
        {
            case 1:
                ai.stepDuration = baseStepDuration;
                ai.stepCooldown = baseStepCooldown;
                ctrl.stats.attackCooldown = baseAttackCooldown;
                break;
            case 2:
                ai.stepDuration = baseStepDuration * p2StepDurationMult;
                ai.stepCooldown = baseStepCooldown * p2StepCooldownMult;
                ctrl.stats.attackCooldown = baseAttackCooldown * p2AttackCooldownMult;
                break;
            case 3:
                ai.stepDuration = baseStepDuration   * p3StepDurationMult;
                ai.stepCooldown = baseStepCooldown * p3StepCooldownMult;
                ctrl.stats.attackCooldown = baseAttackCooldown * p3AttackCooldownMult;
                break;
        }
    }


    private IEnumerator SummonLoop()
    {
        summonLoopRunning = true;

        yield return new WaitForSeconds(summonIntervalP1 * 0.5f);

        while (!ctrl.IsDead)
        {
            float interval = CurrentPhase switch
            {
                1 => summonIntervalP1,
                2 => summonIntervalP2,
                _ => summonIntervalP3
            };
            yield return new WaitForSeconds(interval);

            if (ctrl.IsDead) break;
            if (activeSpirits >= maxSpirits) continue;
            if (ctrl.CurrentState == EnemyController.EnemyState.Attacking) continue;

            yield return StartCoroutine(PerformSummon());
        }

        summonLoopRunning = false;
    }

    private IEnumerator PerformSummon()
    {
        ctrl.SetState(EnemyController.EnemyState.Stunned);
        anim?.Play("summon");

        yield return new WaitForSeconds(summonAnimDuration);

        if (ctrl.IsDead) yield break;

        int toSpawn = Mathf.Min(spiritsPerSummon, maxSpirits - activeSpirits);
        Vector2[] offsets =
        {
            Vector2.up * ai.tileSize * 1.5f,
            Vector2.down * ai.tileSize * 1.5f,
            Vector2.left * ai.tileSize * 1.5f,
            Vector2.right * ai.tileSize * 1.5f,
        };

        for (int i = 0; i < toSpawn && i < offsets.Length; i++)
        {
            if (spiritPrefab == null) break;

            Vector3 pos = transform.position + (Vector3)offsets[i];
            var spirit  = Instantiate(spiritPrefab, pos, Quaternion.identity);

            activeSpirits++;

            var ec = spirit.GetComponent<EnemyController>();
            if (ec != null)
            {
                System.Action<EnemyController> onDied = null;
                onDied = dead =>
                {
                    if (dead != ec) return;
                    activeSpirits = Mathf.Max(0, activeSpirits - 1);
                    EnemyController.OnEnemyDied -= onDied;
                };
                EnemyController.OnEnemyDied += onDied;

                ec.Init();
            }
        }

        if (!ctrl.IsDead)
            ctrl.SetState(EnemyController.EnemyState.Chasing);
    }


    private IEnumerator Skill1Loop()
    {
        skill1LoopRunning = true;

        yield return new WaitForSeconds(CurrentPhase >= 3 ? skill1IntervalP3 * 0.4f
                                                          : skill1IntervalP2 * 0.5f);

        while (!ctrl.IsDead)
        {
            float interval = CurrentPhase >= 3 ? skill1IntervalP3 : skill1IntervalP2;
            yield return new WaitForSeconds(interval);

            if (ctrl.IsDead) break;
            if (ctrl.CurrentState == EnemyController.EnemyState.Attacking) continue;

            yield return StartCoroutine(PerformSkill1());
        }

        skill1LoopRunning = false;
    }

    private IEnumerator PerformSkill1()
    {
        ctrl.SetState(EnemyController.EnemyState.Attacking);
        anim?.Play("Skill1");

        GameObject tile = null;
        if (skill1TilePrefab != null)
        {
            tile = Instantiate(skill1TilePrefab, transform.position, Quaternion.identity);
            tile.transform.localScale = Vector3.one * skill1Radius * 2f;
        }

        yield return new WaitForSeconds(skill1WindUp);

        float dmg = ctrl.GetActualAttackDamage() * skill1DamageMult;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, skill1Radius))
        {
            col.GetComponent<PlayerController>()?.TakeDamage(dmg);

            var ec = col.GetComponent<EnemyController>();
            if (ec != null && ec != ctrl && ec.IsPossessed)
                ec.TakeDamage(dmg);
        }

        yield return new WaitForSeconds(0.15f);
        if (tile) Destroy(tile);

        if (!ctrl.IsDead)
            ctrl.SetState(EnemyController.EnemyState.Chasing);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, skill1Radius);
    }
}