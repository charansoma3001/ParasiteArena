using UnityEngine;

// Attached to every chests instance spawned by MapManager.
// State machine: Closed -> Opened (one-way, no respawn).

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class RareChest : MonoBehaviour
{
    public Sprite closedSprite;
    public Sprite openSprite;
    public float interactRadius = 1.5f;

    // Key the player must press to open the chest when in range
    public KeyCode interactKey = KeyCode.E;

    // Speed of the closed-chest glow pulse (cycles per second).")]
    public float pulseSpeed = 2f;

    // Minimum brightness multiplier during the glow pulse (0 = dark, 1 = white)
    public float pulseMin = 0.7f;

    public static event System.Action<Vector2> OnChestOpened;

    private SpriteRenderer _renderer;
    private BoxCollider2D  _solidCollider;
    private bool           _isOpen;
    private bool           _playerNearby;

    private void Awake()
    {
        _renderer      = GetComponent<SpriteRenderer>();
        _solidCollider = GetComponent<BoxCollider2D>();
        if (closedSprite == null)
            closedSprite = _renderer.sprite;
    }

    private void Update()
    {
        if (_isOpen) return;

        // Pulse the glow while the player is in range.
        if (_playerNearby)
        {
            float t     = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float bright = Mathf.Lerp(pulseMin, 1f, t);
            // Golden tint: full red, ~85% green, no blue.
            _renderer.color = new Color(bright, bright * 0.85f, 0f, 1f);

            if (Input.GetKeyDown(interactKey))
                OpenChest();
        }
        else
        {
            // Reset to normal tint when the player walks away.
            _renderer.color = Color.white;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isOpen && other.CompareTag("Player"))
            _playerNearby = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerNearby = false;
            if (!_isOpen)
                _renderer.color = Color.white;
        }
    }

    private void OpenChest()
    {
        _isOpen       = true;
        _playerNearby = false;

        // Swap sprite to open version.
        if (openSprite != null)
            _renderer.sprite = openSprite;

        // Reset colour — no more glow.
        _renderer.color = Color.white;

        // Disable solid collision so the chest no longer blocks movement.
        // Keep the trigger collider alive so other systems can still detect it.
        _solidCollider.enabled = false;

        // Notify all subscribers (loot tables, score, HUD popups, etc.).
        OnChestOpened?.Invoke(transform.position);

        Debug.Log($"[RareChest] Opened at {transform.position}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw the interact radius as a white wire circle in the Scene view.
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, interactRadius);
    }
#endif
}
