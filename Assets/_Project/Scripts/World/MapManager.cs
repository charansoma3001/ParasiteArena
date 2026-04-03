using UnityEngine;

// this will generate a grid-based map using Fractal Noise,
// https://docs.unity3d.com/6000.3/Documentation/Manual/terrain-Noise-Types.html
public class MapManager : MonoBehaviour
{
    
    public static MapManager Instance { get; private set; }// this is singleton so spawners can query terrain

    public int width = 60;
    public int height = 60;
    public float noiseScale = 9f;

    public int octaves = 4;// Number of noise octaves layered together

    public float persistence = 0.45f;//How quickly amplitude decreases per octave.

    public float lacunarity = 2f;//How quickly frequency increases per octave.

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
    
    public float highGrassThreshold = 0.58f;//Noise value at which high grass begins to appear
    
    public float treeThreshold = 0.80f;//Noise value at which trees replace high grass
    
    public float openCentreRadius = 12f;//Radius entirely clear of prefabs
    
    public int borderClearWidth = 3;//Prevents trees from blocking the playable boundary
    public float vegetationJitter = 0.32f;

    public int seed = 0;//Set to 0 for a random seed each play

    private Transform groundParent;
    private Transform vegetationParent;
    private Transform chestParent;
    private Transform enemiesParent;

    private float[,] noiseMap;//query terrain type without finding noise

    // Tracks world positions of chests
    private System.Collections.Generic.List<Vector2> placedChestPositions = new System.Collections.Generic.List<Vector2>();

    
    public float GetNoiseValue(int x, int y) //Returns the noise value [0,1] at tile  (x, y)
    {
        if (noiseMap == null) return 0f;
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            return noiseMap[x, y];
    }

    public TerrainType GetTerrainType(int x, int y)//Returns the TerrainType at tile  (x, y)
    {
        float v = GetNoiseValue(x, y);
        if (IsProtectedCell(x, y)) return TerrainType.Grass;
        if (v >= treeThreshold) return TerrainType.Tree;
        if (v >= highGrassThreshold) return TerrainType.HighGrass;
        return TerrainType.Grass;
    }

    public Vector2 MapCentre => new Vector2(width * 0.5f, height * 0.5f);//World space centre of the map

    // excludes the outer border strip and adds an extra one-tile margin, so enemies only spawn inside valid map tiles and not too close to the map’s edge
    public Bounds GetPlayableBounds()
    {
        int inset = borderClearWidth + 1;
        float minX = inset;
        float minY = inset;
        float maxX = width - inset;
        float maxY = height - inset;
        Vector3 centre = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Vector3 size   = new Vector3(maxX - minX, maxY - minY, 0f);
        return new Bounds(centre, size);
    }

    public bool IsInsidePlayableArea(Vector2 worldPos)
    {
        return GetPlayableBounds().Contains(new Vector3(worldPos.x, worldPos.y, 0f));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GenerateMap();
    }
    private void GenerateMap()
    {
        int resolvedSeed = (seed == 0) ? Random.Range(1, int.MaxValue) : seed;
        Random.InitState(resolvedSeed);

        groundParent = CreateParent("Ground");
        vegetationParent = CreateParent("Vegetation");
        chestParent= CreateParent("Chests");
        enemiesParent= CreateParent("Enemies");

        placedChestPositions.Clear();

        
        noiseMap = BuildNoiseMap(resolvedSeed);// Generate the fractal noise map

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PlaceTile(x, y);
            }
        }

        SpawnRareChests();
    }

    private void PlaceTile(int x, int y)
    {
        Vector3 basePos = new Vector3(x, y, 0f);

        if (grassPrefab != null)
            Instantiate(grassPrefab, basePos, Quaternion.identity, groundParent);

        if (IsProtectedCell(x, y)) return;// to skip vegetation in protected cells

        float noiseValue = noiseMap[x, y];

        if (noiseValue >= treeThreshold)
        {
            if (treePrefab != null)
            {
                Vector3 jitter = RandomJitter(vegetationJitter * 0.5f);// Trees require tighter jitter 
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

        // all valid grass tile positions
        var candidates = new System.Collections.Generic.List<Vector2>(512);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (GetTerrainType(x, y) == TerrainType.Grass)
                    candidates.Add(new Vector2(x, y));

        // random ordering
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // Pick the first chestCount positions that follow minimum separation.
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
    }

    private void PlaceChest(Vector2 worldPos, Sprite openSprite)
    {
        // Chest sits on the XY plane.
        Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y, 0f);
        GameObject chest = Instantiate(chestPrefab, spawnPos, Quaternion.identity, chestParent);
        chest.name = $"RareChest ({(int)worldPos.x},{(int)worldPos.y})";

        RareChest chestComp = chest.GetComponent<RareChest>();
        if (chestComp == null)
            chestComp = chest.AddComponent<RareChest>();

        if (openSprite != null)
            chestComp.openSprite = openSprite;

        // Add a body so the chest stays static but trigger events from the interaction area child collider
        if (chest.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb = chest.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Trigger collider for the interaction area detection
        AddInteractTrigger(chest, 1.5f);

        placedChestPositions.Add(worldPos);

        // to spawn Rats around the chest as ambushes
        if (ratPrefab != null && Random.value <= chestRatSpawnChance)
        {
            int ratsToSpawn = Random.Range(1, maxRatsPerChest + 1);
            for (int i = 0; i < ratsToSpawn; i++)
            {
                // to find a random adjacent tile (up, down, left, right)
                Vector2[] offsets = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
                Vector2 spawnOffset = offsets[Random.Range(0, offsets.Length)];
                Vector2 ratPos = worldPos + spawnOffset;

                // to make sure its valid (within bounds, grass)
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
        trigger.radius = radius;

        //the RareChest script reads events
        triggerChild.layer = chestGO.layer;
    }

    private float[,] BuildNoiseMap(int resolvedSeed)
    {
        float[,] map = new float[width, height];

        System.Random prng = new System.Random(resolvedSeed);// Unique per-octave offsets prevent octaves from aligning on the same features
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offX = prng.Next(-100000, 100000);
            float offY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offX, offY);
        }

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

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

                    // Mathf.PerlinNoise returns [0,1], and we can shift to [-1,1] so positive and negative cancel.
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

        float range = maxNoiseHeight - minNoiseHeight;
        if (range < Mathf.Epsilon) range = Mathf.Epsilon; // divide by zero

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, map[x, y]);

        return map;
    }

    private bool IsProtectedCell(int x, int y)// Returns true when a tile should be forced to plain grass
    {
        // Border strip
        if (x < borderClearWidth || x >= width  - borderClearWidth || y < borderClearWidth || y >= height - borderClearWidth)
            return true;

        float dx = x - width  * 0.5f;        // Open centre circle
        float dy = y - height * 0.5f;
        return (dx * dx + dy * dy) < (openCentreRadius * openCentreRadius);
    }

    private Vector3 RandomJitter(float amount) //Returns a random XY jitter with z = 0
    {
        return new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), 0f);
    }
    
    private Transform CreateParent(string parentName)//Creates a child transform for grouping
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
