using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject swordsmanPrefab;
    public GameObject archerPrefab;

    [Header("Wave Spawn Settings")]
    [Tooltip("Total enemies spawned per wave.")]
    public int enemiesPerWave = 5;
    [Tooltip("Seconds between individual spawns.")]
    public float spawnInterval  = 1f;

    [Header("Spawn Radius")]
    [Tooltip("Enemies spawn at least this far from the player . ")]
    public float spawnRadiusMin = 6f;
    [Tooltip("Enemies spawn at most this far from the player.")]
    public float spawnRadiusMax = 9f;

    [Header("Terrain Validation")]
    [Tooltip("How many random positions to try")]
    public int maxSpawnAttempts = 20;

    private Transform player;
    private float timer;
    private bool spawning;
    private int spawnedThisWave;

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) player = playerGO.transform;

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            WaveManager.Instance.OnWaveEnded += OnWaveEnded;
        }
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveEnded -= OnWaveEnded;
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        spawning = true;
        timer = 0f;
        spawnedThisWave = 0;
    }

    private void OnWaveEnded()
    {
        spawning = false;
    }

    private void Update()
    {
        if (!spawning) return;
        if (spawnedThisWave >= enemiesPerWave) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnEnemy();
            spawnedThisWave++;
        }
    }

    private void SpawnEnemy()
    {
        Vector2 spawnPos = GetSpawnPosition();
        GameObject prefab = Random.value > 0.5f ? swordsmanPrefab : archerPrefab;

        if (prefab == null)
        {
            return;
        }

        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.GetComponent<EnemyController>()?.Init();
    }

    private Vector2 GetSpawnPosition()
    {
        Vector2 origin = player != null
            ? (Vector2)player.position
            : Vector2.zero;
        MapManager map = MapManager.Instance;
        if (map == null)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
            return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        Bounds playable = map.GetPlayableBounds();

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            if (!map.IsInsidePlayableArea(candidate)) continue;

            int tx = Mathf.RoundToInt(candidate.x);
            int ty = Mathf.RoundToInt(candidate.y);
            if (map.GetTerrainType(tx, ty) != TerrainType.Grass) continue;

            return candidate;
        }

        float fallbackX = Random.Range(playable.min.x, playable.max.x);
        float fallbackY = Random.Range(playable.min.y, playable.max.y);
        return new Vector2(fallbackX, fallbackY);
    }
}