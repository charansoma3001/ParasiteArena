using UnityEngine;

public class ObjectFade : MonoBehaviour
{
    [Range(0f, 1f)]
    public float fadedAlpha = 0.35f;
    public float fadeSpeed = 8f;

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

    private void Update()
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
