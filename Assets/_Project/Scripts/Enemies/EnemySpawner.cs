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
    [Tooltip("How many random positions to try before giving up and using the last candidate.")]
    public int maxSpawnAttempts = 20;

    private Transform _player;
    private float _timer;
    private bool _spawning;
    private int _spawnedThisWave;

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
            WaveManager.Instance.OnWaveEnded -= OnWaveEnded;
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        _spawning = true;
        _timer = 0f;
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

        // Attempt to land on a walkable grass tile that is also inside the
        // playable area.  The bounds check is done FIRST: GetTerrainType clamps
        // out-of-range tile coords to the edge of the noise map, so without the
        // bounds guard it would silently return Grass for any world position
        // beyond the map grid and allow enemies to spawn off the background.
        MapManager map = MapManager.Instance;

        // If MapManager is absent we have no bounds to validate against — just
        // return the raw position and let the caller deal with it.
        if (map == null)
        {
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
            return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        Bounds playable = map.GetPlayableBounds();

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            // Gate 1: must be inside the playable map bounds.
            if (!map.IsInsidePlayableArea(candidate)) continue;

            // Gate 2: tile must be walkable grass (not tree / high grass).
            int tx = Mathf.RoundToInt(candidate.x);
            int ty = Mathf.RoundToInt(candidate.y);
            if (map.GetTerrainType(tx, ty) != TerrainType.Grass) continue;

            return candidate;
        }

        // No valid position found in the ring — fall back to a random point
        // sampled directly inside the playable bounds, biased toward the centre
        // so it is most likely to be open terrain.
        Debug.LogWarning($"[EnemySpawner] Could not find a valid grass tile inside the playable area after {maxSpawnAttempts} attempts — falling back to a bounds-clamped position.");

        float fallbackX = Random.Range(playable.min.x, playable.max.x);
        float fallbackY = Random.Range(playable.min.y, playable.max.y);
        return new Vector2(fallbackX, fallbackY);
    }
}