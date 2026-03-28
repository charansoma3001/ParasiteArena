using UnityEngine;

// Attach to the same GameObject as the trigger collider.
// Fades out the ObjectFade component (searches self and children) when the Player enters.
public class FoliageFade : MonoBehaviour
{
    private ObjectFade fade;

    private void Awake()
    {
        fade = GetComponentInChildren<ObjectFade>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            fade?.FadeOut();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            fade?.FadeIn();
    }
}
