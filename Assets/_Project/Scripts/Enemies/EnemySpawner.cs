using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject swordsmanPrefab;
    public GameObject archerPrefab;

    [Header("Wave Spawn Settings")]
    [Tooltip("Total enemies spawned per wave.")]
    public int   enemiesPerWave = 5;
    [Tooltip("Seconds between individual spawns.")]
    public float spawnInterval  = 1f;

    [Header("Spawn Radius (player-relative)")]
    [Tooltip("Enemies spawn at least this far from the player (world units). " +
             "Keep above your camera half-height so they appear off-screen.")]
    public float spawnRadiusMin = 6f;
    [Tooltip("Enemies spawn at most this far from the player.")]
    public float spawnRadiusMax = 9f;

    private Transform _player;
    private float     _timer;
    private bool      _spawning;
    private int       _spawnedThisWave;

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) _player = playerGO.transform;

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            WaveManager.Instance.OnWaveEnded   += OnWaveEnded;
        }
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveEnded   -= OnWaveEnded;
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        _spawning        = true;
        _timer           = 0f;
        _spawnedThisWave = 0;
        Debug.Log($"[EnemySpawner] Wave {waveNumber} — spawning {enemiesPerWave} enemies.");
    }

    private void OnWaveEnded()
    {
        _spawning = false;
    }

    private void Update()
    {
        if (!_spawning) return;
        if (_spawnedThisWave >= enemiesPerWave) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemy();
            _spawnedThisWave++;
        }
    }

    private void SpawnEnemy()
    {
        Vector2 spawnPos = GetSpawnPosition();
        GameObject prefab = Random.value > 0.5f ? swordsmanPrefab : archerPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawner] Prefab not assigned in Inspector.");
            return;
        }

        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.GetComponent<EnemyController>()?.Init();
    }

    private Vector2 GetSpawnPosition()
    {
        Vector2 origin = _player != null
            ? (Vector2)_player.position
            : Vector2.zero;

        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }
}