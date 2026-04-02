using UnityEngine;

public class ObjectFade : MonoBehaviour
{
    // makes a sprite become transparent and then visible again
    [Range(0f, 1f)]
    public float fadedAlpha = 0.35f; // how see-through it becomes
    public float fadeSpeed = 8f; // how fast it fades

    private SpriteRenderer[] renderers;
    private float targetAlpha = 1f;

    private void Awake()
    {
        SpriteRenderer self = GetComponent<SpriteRenderer>();
        if (self != null)
            renderers = new SpriteRenderer[] { self };
        else
            renderers = GetComponentsInChildren<SpriteRenderer>();

        foreach (var sr in renderers)
            sr.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update() // keeps moving the current transparency toward that target
    {
        if (renderers == null) return;
        foreach (var sr in renderers)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
            sr.color = c;
        }
    }

    public void FadeOut() { targetAlpha = fadedAlpha; }
    public void FadeIn()  { targetAlpha = 1f; }
}
