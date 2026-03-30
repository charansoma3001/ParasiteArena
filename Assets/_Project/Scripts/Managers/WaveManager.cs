using UnityEngine;
using System;

public enum GameState
{
    PrepPhase,
    WaveActive,
    GameOver
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Settings")]
    public float timePerWave = 60f;

    [Header("Boss Settings")]
    [Tooltip("Boss spawns at the END of this wave. Waves continue after boss appears.")]
    public int bossWave = 5;
    [Tooltip("Prefab for the boss. Assign in Inspector.")]
    public GameObject bossPrefab;
    [Tooltip("World-space offset from the player where the boss spawns.")]
    public float bossSpawnOffset = 5f;

    public GameState CurrentState  { get; private set; }
    public int       CurrentWave   { get; private set; } = 0;
    public float     WaveTimer     { get; private set; }
    public float     PrepCountdown { get; private set; } = 0f;
    public bool      BossSpawned   { get; private set; } = false;

    private float _prepCountdownMax = 3f;

    public event Action<int>   OnWaveStarted;
    public event Action        OnWaveEnded;
    public event Action<float> OnTimerChanged;
    public event Action<float> OnCountdownChanged;
    public event Action        OnBossSpawned;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ChangeState(GameState.PrepPhase);
        Invoke(nameof(StartNextWave), 3f);
    }

    private void Update()
    {
        if (CurrentState == GameState.PrepPhase && PrepCountdown > 0)
        {
            PrepCountdown -= Time.deltaTime;
            OnCountdownChanged?.Invoke(Mathf.Max(0, PrepCountdown));
            if (PrepCountdown <= 0) BeginWave();
        }

        if (CurrentState == GameState.WaveActive)
        {
            WaveTimer -= Time.deltaTime;
            OnTimerChanged?.Invoke(WaveTimer);
            if (WaveTimer <= 0) EndWave();
        }
    }

    public void StartNextWave()
    {
        if (CurrentState != GameState.PrepPhase) return;
        if (PrepCountdown > 0) return;

        PrepCountdown = _prepCountdownMax;
        OnCountdownChanged?.Invoke(PrepCountdown);
        ShopManager.Instance?.CloseShop();
    }

    private void BeginWave()
    {
        CurrentWave++;
        WaveTimer = timePerWave;
        ChangeState(GameState.WaveActive);
        OnWaveStarted?.Invoke(CurrentWave);
        Debug.Log($"[WaveManager] Wave {CurrentWave} started.");
    }

    private void EndWave()
    {
        ChangeState(GameState.PrepPhase);
        PrepCountdown = 0f;
        OnCountdownChanged?.Invoke(0f);
        OnWaveEnded?.Invoke();
        Debug.Log($"[WaveManager] Wave {CurrentWave} ended.");

        if (CurrentWave >= bossWave && !BossSpawned)
            SpawnBoss();

        TriggerShop();
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("[WaveManager] bossPrefab not assigned — skipping boss spawn.");
            return;
        }

        var player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        Vector3 spawnPos = origin + new Vector3(bossSpawnOffset, 0f, 0f);

        var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        boss.GetComponent<EnemyController>()?.Init();

        BossSpawned = true;
        
        AudioManager.Instance?.PlayBossWave();
        
        OnBossSpawned?.Invoke();
        Debug.Log($"[WaveManager] Boss spawned after Wave {CurrentWave}.");
    }

    private void TriggerShop()
    {
        ShopManager.Instance?.OpenShop();
    }

    private void ChangeState(GameState newState)
    {
        CurrentState = newState;
    }
}