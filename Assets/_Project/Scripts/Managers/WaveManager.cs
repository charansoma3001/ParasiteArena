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
    public int bossWave = 5;
    public GameObject bossPrefab;
    public float bossSpawnOffset = 5f;

    public GameState CurrentState{ get; private set; }
    public int CurrentWave{ get; private set; } = 0;
    public float WaveTimer{ get; private set; }
    public float PrepCountdown{ get; private set; } = 0f;
    public bool BossSpawned{ get; private set; } = false;

    private float prepCountdownMax = 3f;

    public event Action<int> OnWaveStarted;
    public event Action OnWaveEnded;
    public event Action<float> OnTimerChanged;
    public event Action<float> OnCountdownChanged;
    public event Action OnBossSpawned;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() // begins the game in prep phase and starts the first countdown
    {
        ChangeState(GameState.PrepPhase);
        Invoke(nameof(StartNextWave), 3f);
    }

    private void Update() // keeps reducing the countdown or wave timer
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

    public void StartNextWave() //starts the countdown before a new wave
    {
        if (CurrentState != GameState.PrepPhase) return;
        if (PrepCountdown > 0) return;

        PrepCountdown = prepCountdownMax;
        OnCountdownChanged?.Invoke(PrepCountdown);
        ShopManager.Instance?.CloseShop();
    }

    private void BeginWave() //starts the next wave
    {
        CurrentWave++;
        WaveTimer = timePerWave;
        ChangeState(GameState.WaveActive);
        OnWaveStarted?.Invoke(CurrentWave);
    }

    private void EndWave() //ends the wave, opens the shop, and may spawn the boss
    {
        ChangeState(GameState.PrepPhase);
        PrepCountdown = 0f;
        OnCountdownChanged?.Invoke(0f);
        OnWaveEnded?.Invoke();

        if (CurrentWave >= bossWave && !BossSpawned)
            SpawnBoss();

        TriggerShop();
    }

    private void SpawnBoss() //spawn the boss near the player on a safe grass tile inside the playable area
    {
        if (bossPrefab == null)
        {
            return;
        }

        var player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        MapManager map = MapManager.Instance;
        Vector3 spawnPos = origin + new Vector3(bossSpawnOffset, 0f, 0f);
        if (map != null)
        {
            Bounds playable = map.GetPlayableBounds();

            // this is to set the default area by clamping them immediately
            spawnPos = new Vector3(
                Mathf.Clamp(spawnPos.x, playable.min.x, playable.max.x),
                Mathf.Clamp(spawnPos.y, playable.min.y, playable.max.y),
                0f);

            const int attempts = 20;
            bool found = false;
            for (int i = 0; i < attempts; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 candidate = origin + new Vector3(
                    Mathf.Cos(angle) * bossSpawnOffset,
                    Mathf.Sin(angle) * bossSpawnOffset,
                    0f);

                // inside playable bounds
                if (!map.IsInsidePlayableArea(new Vector2(candidate.x, candidate.y))) continue;

                // grass tile
                int tx = Mathf.RoundToInt(candidate.x);
                int ty = Mathf.RoundToInt(candidate.y);
                if (map.GetTerrainType(tx, ty) != TerrainType.Grass) continue;

                spawnPos = candidate;
                found = true;
                break;
            }
        }

        var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        boss.GetComponent<EnemyController>()?.Init();

        BossSpawned = true;
        
        AudioManager.Instance?.PlayBossWave();
        
        OnBossSpawned?.Invoke();
    }

    private void TriggerShop() //opens the shop
    {
        ShopManager.Instance?.OpenShop();
    }

    private void ChangeState(GameState newState) //changes the current game state
    {
        CurrentState = newState;
    }
}