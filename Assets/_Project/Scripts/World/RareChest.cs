using UnityEngine;

// Attached to every chest instance spawned by MapManager.
// State machine: Closed -> Opened (one-way, no respawn).

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class RareChest : MonoBehaviour
{
    public Sprite closedSprite;
    public Sprite openSprite;
    public KeyCode interactKey = KeyCode.O;

    public static event System.Action<Vector2> OnChestOpened;

    private new SpriteRenderer renderer;
    private BoxCollider2D solidCollider;
    private bool isOpen;
    private bool playerNearby;

    private void Awake()
    {
        renderer= GetComponent<SpriteRenderer>();
        solidCollider = GetComponent<BoxCollider2D>();
        if (closedSprite == null)
            closedSprite = renderer.sprite;
    }

    private void Update()
    {
        if (isOpen) return;

        if (playerNearby && Input.GetKeyDown(interactKey))
            OpenChest();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen && other.CompareTag("Player"))
            playerNearby = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerNearby = false;
    }

    private void OpenChest()
    {
        isOpen= true;
        playerNearby = false;

        if (openSprite != null)
            renderer.sprite = openSprite;

        solidCollider.enabled = false;

        OnChestOpened?.Invoke(transform.position);
        Debug.Log($"[RareChest] Opened at {transform.position}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, 1.5f);
    }
#endif
}
