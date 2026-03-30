using UnityEngine;

public class MapManager : MonoBehaviour
{
    public int width = 60;
    public int height = 60;

    //Larger values = more variation Standard value is 6f
    public float noiseScale = 9f;

    // Number of noise octaves layered together. More octaves = more detail. Standard value is 4
    public int octaves = 4;

    //How quickly amplitude decreases per octave. Higher = smoother. Standard value is 0.5f
    public float persistence = 0.45f;

    //How quickly frequency increases per octave. Standard value is 2f
    public float lacunarity = 2f;

    public GameObject grassPrefab;
    public GameObject highGrassPrefab;
    public GameObject treePrefab;
    public GameObject chestPrefab;
    public GameObject chestOpenPrefab;

    public int chestCount = 6;
    public float chestMinSeparation = 6f;

    [Header("Rat Spawns")]
    public GameObject ratPrefab;
    [Range(0f, 1f)]
    public float chestRatSpawnChance = 0.6f;
    public int maxRatsPerChest = 2;

    //Noise value at which high grass begins to appear. Standard = 0.5f;
    public float highGrassThreshold = 0.58f;

    //Noise value at which trees replace high grass. Standard = 0.7f
    public float treeThreshold = 0.80f;

    //Radius entirely clear of prefabs. Stardard = 10f;
    public float openCentreRadius = 12f;

    //Prevents trees from blocking the playable boundary. Standard = 0.3
    public int borderClearWidth = 3;
    public float vegetationJitter = 0.32f;

     //Set to 0 for a random seed each play
    public int seed = 0;

    //parent transforms for editor hierarchy organisation.
    private Transform groundParent;
    private Transform vegetationParent;
    private Transform chestParent;
    private Transform enemiesParent;

    //query terrain type without re-computing noise.
    private float[,] noiseMap;

    // Tracks world positions of already-placed chests so we can enforce the minimum separation constraint during generation.
    private System.Collections.Generic.List<Vector2> placedChestPositions = new System.Collections.Generic.List<Vector2>();

    //Returns the noise value [0,1] at tile coordinate (x, y).
    public float GetNoiseValue(int x, int y)
    {
        if (noiseMap == null) return 0f;
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return noiseMap[x, y];
    }

    //Returns the TerrainType at tile coordinate (x, y).
    public TerrainType GetTerrainType(int x, int y)
    {
        float v = GetNoiseValue(x, y);
        if (IsProtectedCell(x, y)) return TerrainType.Grass;
        if (v >= treeThreshold)      return TerrainType.Tree;
        if (v >= highGrassThreshold) return TerrainType.HighGrass;
        return TerrainType.Grass;
    }

    //World-space centre of the map
    public Vector2 MapCentre => new Vector2(width * 0.5f, height * 0.5f);

    private void Start()
    {
        GenerateMap();
    }
    private void GenerateMap()
    {
        // Resolve seed.
        int resolvedSeed = (seed == 0) ? Random.Range(1, int.MaxValue) : seed;
        Random.InitState(resolvedSeed);

        // Build parent objects for clean hierarchy.
        groundParent     = CreateParent("Ground");
        vegetationParent = CreateParent("Vegetation");
        chestParent      = CreateParent("Chests");
        enemiesParent    = CreateParent("Enemies");

        // Clear chest position cache for reproducible placement.
        placedChestPositions.Clear();

        // Generate the fractal noise map.
        noiseMap = BuildNoiseMap(resolvedSeed);

        // Place tiles.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PlaceTile(x, y);
            }
        }

        // Chest pass runs after all tiles are placed so terrain type queries are reliable and we have a complete picture of valid spawn cells.
        SpawnRareChests();
    }

    private void PlaceTile(int x, int y)
    {
        // Base position on the 2D XY plane (z = 0).
        Vector3 basePos = new Vector3(x, y, 0f);

        // Always place a grass ground tile.
        if (grassPrefab != null)
            Instantiate(grassPrefab, basePos, Quaternion.identity, groundParent);

        // Skip vegetation in protected cells.
        if (IsProtectedCell(x, y)) return;

        float noiseValue = noiseMap[x, y];

        if (noiseValue >= treeThreshold)
        {
            if (treePrefab != null)
            {
                // Trees use tighter jitter — they are hard obstacles and
                // visually large, so less offset looks more intentional.
                Vector3 jitter = RandomJitter(vegetationJitter * 0.5f);
                Instantiate(treePrefab, basePos + jitter, Quaternion.identity, vegetationParent);
            }
        }
        else if (noiseValue >= highGrassThreshold)
        {
            if (highGrassPrefab != null)
            {
                Vector3 jitter = RandomJitter(vegetationJitter);
                Instantiate(highGrassPrefab, basePos + jitter, Quaternion.identity, vegetationParent);
            }
        }
    }

    private void SpawnRareChests()
    {
        if (chestPrefab == null) return;

        Sprite openSprite = null;
        if (chestOpenPrefab != null)
        {
            SpriteRenderer openRenderer = chestOpenPrefab.GetComponent<SpriteRenderer>();
            if (openRenderer != null)
                openSprite = openRenderer.sprite;
        }

        // Collect all valid grass tile positions.
        var candidates = new System.Collections.Generic.List<Vector2>(512);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (GetTerrainType(x, y) == TerrainType.Grass)
                    candidates.Add(new Vector2(x, y));

        // Fisher-Yates shuffle for random ordering.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // Pick the first chestCount positions that satisfy min separation.
        float sqrMinSep = chestMinSeparation * chestMinSeparation;
        int placed = 0;
        foreach (Vector2 pos in candidates)
        {
            if (placed >= chestCount) break;

            bool tooClose = false;
            foreach (Vector2 existing in placedChestPositions)
            {
                if ((pos - existing).sqrMagnitude < sqrMinSep)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            PlaceChest(pos, openSprite);
            placed++;
        }

        if (placed < chestCount)
            Debug.LogWarning($"[MapManager] Could only place {placed}/{chestCount} chests — try reducing chestMinSeparation.");
    }

    private void PlaceChest(Vector2 worldPos, Sprite openSprite)
    {
        // Chest sits on the XY plane at z=0, matching all other props.
        Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y, 0f);
        GameObject chest = Instantiate(chestPrefab, spawnPos, Quaternion.identity, chestParent);
        chest.name = $"RareChest ({(int)worldPos.x},{(int)worldPos.y})";

        // Wire the RareChest component.  Add one automatically if the designer
        // forgot to put it on the prefab — keeps the spawn system self-healing.
        RareChest chestComp = chest.GetComponent<RareChest>();
        if (chestComp == null)
            chestComp = chest.AddComponent<RareChest>();

        if (openSprite != null)
            chestComp.openSprite = openSprite;

        // Add a body so the chest stays static but trigger events from the interact-radius child collider
        if (chest.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb   = chest.AddComponent<Rigidbody2D>();
            rb.bodyType      = RigidbodyType2D.Kinematic;
            rb.gravityScale  = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Trigger collider for the interact-radius detection
        AddInteractTrigger(chest, 1.5f);

        // Record position for separation checks.
        placedChestPositions.Add(worldPos);

        // Spawn Rats around the chest as ambushes
        if (ratPrefab != null && Random.value <= chestRatSpawnChance)
        {
            int ratsToSpawn = Random.Range(1, maxRatsPerChest + 1);
            for (int i = 0; i < ratsToSpawn; i++)
            {
                // Find a random adjacent tile (up, down, left, right)
                Vector2[] offsets = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
                Vector2 spawnOffset = offsets[Random.Range(0, offsets.Length)];
                Vector2 ratPos = worldPos + spawnOffset;

                // Make sure it's valid (within bounds, grass)
                if (ratPos.x >= 0 && ratPos.x < width && ratPos.y >= 0 && ratPos.y < height)
                {
                    if (GetTerrainType((int)ratPos.x, (int)ratPos.y) == TerrainType.Grass)
                    {
                        var spawnedRat = Instantiate(ratPrefab, new Vector3(ratPos.x, ratPos.y, 0f), Quaternion.identity, enemiesParent);
                        spawnedRat.GetComponent<EnemyController>()?.Init();
                    }
                }
            }
        }
    }
    private void AddInteractTrigger(GameObject chestGO, float radius)
    {
        GameObject triggerChild = new GameObject("InteractTrigger");
        triggerChild.transform.SetParent(chestGO.transform, false);
        triggerChild.transform.localPosition = Vector3.zero;

        CircleCollider2D trigger = triggerChild.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius    = radius;

        // The RareChest script reads OnTriggerEnter2D/Exit2D events.
        // Forward them from the child to the parent by copying the layer.
        triggerChild.layer = chestGO.layer;
    }

    private float[,] BuildNoiseMap(int resolvedSeed)
    {
        float[,] map = new float[width, height];

        // Unique per-octave offsets prevent octaves from aligning on the same
        // features, which would create obvious repetition.
        System.Random prng = new System.Random(resolvedSeed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offX = prng.Next(-100000, 100000);
            float offY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offX, offY);
        }

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        // First pass: accumulate raw fBm values.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float amplitude  = 1f;
                float frequency  = 1f;
                float noiseHeight = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float sampleX = (x / (float)width)  * noiseScale * frequency + octaveOffsets[o].x;
                    float sampleY = (y / (float)height)  * noiseScale * frequency + octaveOffsets[o].y;

                    // Mathf.PerlinNoise returns [0,1]; shift to [-1,1] so
                    // positive and negative contributions can cancel, giving
                    // genuine fBm character.
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                map[x, y] = noiseHeight;

                if (noiseHeight > maxNoiseHeight) maxNoiseHeight = noiseHeight;
                if (noiseHeight < minNoiseHeight) minNoiseHeight = noiseHeight;
            }
        }

        // Second pass: normalise to [0,1].
        float range = maxNoiseHeight - minNoiseHeight;
        if (range < Mathf.Epsilon) range = Mathf.Epsilon; // guard divide-by-zero

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, map[x, y]);

        return map;
    }


    // Returns true when a tile should be forced to plain grass regardless of noise — the open centre combat arena and the border clearance strip.
    private bool IsProtectedCell(int x, int y)
    {
        // Border strip.
        if (x < borderClearWidth || x >= width  - borderClearWidth ||
            y < borderClearWidth || y >= height - borderClearWidth)
            return true;

        // Open centre circle.
        float dx = x - width  * 0.5f;
        float dy = y - height * 0.5f;
        return (dx * dx + dy * dy) < (openCentreRadius * openCentreRadius);
    }

    //Returns a random XY jitter vector with z = 0
    private Vector3 RandomJitter(float amount)
    {
        return new Vector3(
            Random.Range(-amount, amount),
            Random.Range(-amount, amount),
            0f
        );
    }

    //Creates a named, empty child transform for hierarchy grouping
    private Transform CreateParent(string parentName)
    {
        GameObject go = new GameObject(parentName);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }
}

public enum TerrainType
{
    Grass,
    HighGrass,
    Tree
}
