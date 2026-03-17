using UnityEngine;

/// <summary>
/// Procedurally generates the arena environment using multi-octave Perlin noise.
///
/// Coordinate system: 2D top-down, XY plane. World tiles are placed at integer
/// (x, y) positions with z = 0. The origin (0,0) is the bottom-left corner of
/// the map so the arena centre lands at (width/2, height/2).
///
/// Terrain hierarchy (noise value, low -> high):
///   [0.00 – highGrassThreshold)  → grass only  (open combat space)
///   [highGrassThreshold – treeThreshold) → high grass (soft cover, walkable)
///   [treeThreshold – 1.00]       → tree        (hard obstacle, has collider)
///
/// An open-centre radius and a clear border strip guarantee enough playable
/// space regardless of the random seed.
/// </summary>
public class MapManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Map Dimensions")]
    [Tooltip("Number of tiles along the X axis.")]
    public int width = 60;

    [Tooltip("Number of tiles along the Y axis.")]
    public int height = 60;

    [Header("Noise Settings")]
    [Tooltip("Base frequency of the Perlin noise. Larger values = more variation.")]
    public float noiseScale = 6f;

    [Tooltip("Number of noise octaves layered together. More octaves = more detail.")]
    [Range(1, 6)]
    public int octaves = 4;

    [Tooltip("How quickly amplitude decreases per octave (0-1). Higher = smoother.")]
    [Range(0f, 1f)]
    public float persistence = 0.5f;

    [Tooltip("How quickly frequency increases per octave. Standard value is 2.")]
    [Range(1f, 4f)]
    public float lacunarity = 2f;

    [Header("Terrain Prefabs")]
    [Tooltip("Base grass tile — placed everywhere as the ground layer.")]
    public GameObject grassPrefab;

    [Tooltip("High grass clump — soft cover placed in medium-density noise regions.")]
    public GameObject highGrassPrefab;

    [Tooltip("Tree — hard obstacle placed in high-density noise regions.")]
    public GameObject treePrefab;

    [Header("Terrain Thresholds (0 – 1)")]
    [Tooltip("Noise value at which high grass begins to appear.")]
    [Range(0f, 1f)]
    public float highGrassThreshold = 0.52f;

    [Tooltip("Noise value at which trees replace high grass.")]
    [Range(0f, 1f)]
    public float treeThreshold = 0.72f;

    [Header("Open Space Settings")]
    [Tooltip("Radius (in tiles) around the map centre that is kept entirely clear of " +
             "high grass and trees. This is the core combat arena.")]
    public float openCentreRadius = 10f;

    [Tooltip("Width of the clear border strip around the map edge (in tiles). " +
             "Prevents trees from blocking the playable boundary.")]
    [Range(0, 8)]
    public int borderClearWidth = 3;

    [Header("Placement Jitter")]
    [Tooltip("Max random offset applied to high-grass and tree positions so they " +
             "do not sit on a perfect grid. Trees jitter less than grass by default.")]
    public float vegetationJitter = 0.28f;

    [Header("Seed")]
    [Tooltip("Seed for reproducible maps. Set to 0 for a random seed each play.")]
    public int seed = 0;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    // Parent transforms for editor hierarchy organisation.
    private Transform _groundParent;
    private Transform _vegetationParent;

    // The raw noise map — stored so other systems (spawn points, visibility)
    // can query terrain type without re-computing noise.
    private float[,] _noiseMap;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Returns the noise value [0,1] at tile coordinate (x, y).</summary>
    public float GetNoiseValue(int x, int y)
    {
        if (_noiseMap == null) return 0f;
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return _noiseMap[x, y];
    }

    /// <summary>Returns the TerrainType at tile coordinate (x, y).</summary>
    public TerrainType GetTerrainType(int x, int y)
    {
        float v = GetNoiseValue(x, y);
        if (IsProtectedCell(x, y)) return TerrainType.Grass;
        if (v >= treeThreshold)      return TerrainType.Tree;
        if (v >= highGrassThreshold) return TerrainType.HighGrass;
        return TerrainType.Grass;
    }

    /// <summary>World-space centre of the map.</summary>
    public Vector2 MapCentre => new Vector2(width * 0.5f, height * 0.5f);

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        GenerateMap();
    }

    // -------------------------------------------------------------------------
    // Generation
    // -------------------------------------------------------------------------

    private void GenerateMap()
    {
        // Resolve seed.
        int resolvedSeed = (seed == 0) ? Random.Range(1, int.MaxValue) : seed;
        Random.InitState(resolvedSeed);

        // Build parent objects for clean hierarchy.
        _groundParent     = CreateParent("Ground");
        _vegetationParent = CreateParent("Vegetation");

        // Generate the fractal noise map.
        _noiseMap = BuildNoiseMap(resolvedSeed);

        // Place tiles.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PlaceTile(x, y);
            }
        }
    }

    private void PlaceTile(int x, int y)
    {
        // Base position on the 2D XY plane (z = 0).
        Vector3 basePos = new Vector3(x, y, 0f);

        // Always place a grass ground tile.
        if (grassPrefab != null)
            Instantiate(grassPrefab, basePos, Quaternion.identity, _groundParent);

        // Skip vegetation in protected cells.
        if (IsProtectedCell(x, y)) return;

        float noiseValue = _noiseMap[x, y];

        if (noiseValue >= treeThreshold)
        {
            if (treePrefab != null)
            {
                // Trees use tighter jitter — they are hard obstacles and
                // visually large, so less offset looks more intentional.
                Vector3 jitter = RandomJitter(vegetationJitter * 0.5f);
                Instantiate(treePrefab, basePos + jitter, Quaternion.identity, _vegetationParent);
            }
        }
        else if (noiseValue >= highGrassThreshold)
        {
            if (highGrassPrefab != null)
            {
                Vector3 jitter = RandomJitter(vegetationJitter);
                Instantiate(highGrassPrefab, basePos + jitter, Quaternion.identity, _vegetationParent);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Noise
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a normalised [0,1] fractal (fBm) noise map using Unity's
    /// Mathf.PerlinNoise as the base function.
    ///
    /// Each octave doubles the frequency (lacunarity) and halves the amplitude
    /// (persistence), giving natural-looking organic shapes rather than the
    /// single-frequency blobs that plain Perlin produces.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when a tile should be forced to plain grass regardless of
    /// noise — the open centre combat arena and the border clearance strip.
    /// </summary>
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

    /// <summary>Returns a random XY jitter vector with z = 0.</summary>
    private Vector3 RandomJitter(float amount)
    {
        return new Vector3(
            Random.Range(-amount, amount),
            Random.Range(-amount, amount),
            0f
        );
    }

    /// <summary>Creates a named, empty child transform for hierarchy grouping.</summary>
    private Transform CreateParent(string parentName)
    {
        GameObject go = new GameObject(parentName);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }
}

/// <summary>
/// Terrain classification used by external systems (spawn manager, visibility, AI).
/// Grass = fully open. HighGrass = walkable soft cover. Tree = hard obstacle.
/// </summary>
public enum TerrainType
{
    Grass,
    HighGrass,
    Tree
}
