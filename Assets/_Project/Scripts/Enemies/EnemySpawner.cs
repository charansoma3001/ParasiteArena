using UnityEngine;

// Listens to WaveManager events and spawns Swordsmen/Archers outside the camera.
// Attach to an empty GameObject in the scene.
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject swordsmanPrefab;
    public GameObject archerPrefab;

    [Header("Spawn Settings")]
    public int enemiesPerWave = 5;     // total enemies to spawn per wave
    public float spawnInterval = 1f;   // seconds between each spawn
    public float spawnDistance = 2f;   // extra distance past the camera edge

    private Camera _cam;
    private float _timer;
    private bool _spawning;
    private int _spawnedThisWave;

    private void Start()
    {
        _cam = Camera.main;

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
        Debug.Log("[EnemySpawner] Wave ended — spawning stopped.");
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
        Vector2 spawnPos = GetSpawnPositionOutsideCamera();
        GameObject prefab = Random.value > 0.5f ? swordsmanPrefab : archerPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawner] Prefab not assigned in Inspector!");
            return;
        }

        Instantiate(prefab, spawnPos, Quaternion.identity);
    }

    private Vector2 GetSpawnPositionOutsideCamera()
    {
        float camHeight = _cam.orthographicSize;
        float camWidth  = camHeight * _cam.aspect;
        Vector3 camPos  = _cam.transform.position;

        int edge = Random.Range(0, 4);

        return edge switch
        {
            0 => new Vector2(Random.Range(camPos.x - camWidth, camPos.x + camWidth), camPos.y + camHeight + spawnDistance),
            1 => new Vector2(Random.Range(camPos.x - camWidth, camPos.x + camWidth), camPos.y - camHeight - spawnDistance),
            2 => new Vector2(camPos.x - camWidth - spawnDistance, Random.Range(camPos.y - camHeight, camPos.y + camHeight)),
            _ => new Vector2(camPos.x + camWidth + spawnDistance, Random.Range(camPos.y - camHeight, camPos.y + camHeight)),
        };
    }
}
