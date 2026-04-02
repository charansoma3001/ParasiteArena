using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    public Image fillImage;
    public CanvasGroup canvasGroup;
    public Canvas barCanvas;

    [Header("Sizing")]
    public bool autoSizeFromOwnerSprite = true;
    public float widthMultiplier = 1.0f;
    public float heightMultiplier = 0.16f;

    private EnemyController owner;
    private Vector3 offset;
    private SpriteRenderer ownerSprite;

    public void Bind(EnemyController owner, Vector3 worldOffset)
    {
        owner = owner;
        offset = worldOffset;

        if (barCanvas == null)
            barCanvas = GetComponentInChildren<Canvas>(true);
        if (barCanvas != null)
        {
            barCanvas.renderMode = RenderMode.WorldSpace;
            barCanvas.worldCamera = Camera.main;
        }

        ownerSprite = owner.GetComponentInChildren<SpriteRenderer>();

        transform.SetParent(owner.transform, false);
        transform.localPosition = offset;
        transform.localRotation = Quaternion.identity;

        ApplyAutoSize();

        if (fillImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.type == Image.Type.Filled)
                {
                    fillImage = img;
                    break;
                }
            }

            if (fillImage == null && images.Length > 0)
                fillImage = images[0];
        }

        Refresh();
    }

    private void ApplyAutoSize()
    {
        if (!autoSizeFromOwnerSprite || barCanvas == null || ownerSprite == null) return;

        var canvasRect = barCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        float rectW = Mathf.Max(1f, canvasRect.rect.width);
        float rectH = Mathf.Max(1f, canvasRect.rect.height);

        Vector2 spriteSize = ownerSprite.bounds.size;
        float targetWorldW = Mathf.Max(0.05f, spriteSize.x * widthMultiplier);
        float targetWorldH = Mathf.Max(0.02f, spriteSize.y * heightMultiplier);

        canvasRect.localScale = new Vector3(targetWorldW / rectW, targetWorldH / rectH, 1f);
    }

    public void Refresh()
    {
        if (owner == null || fillImage == null) return;
        if (owner.stats == null) return;
        float maxHp = Mathf.Max(1f, owner.stats.maxHealth);
        fillImage.fillAmount = Mathf.Clamp01(owner.CurrentHP / maxHp);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            return;
        }

        if (barCanvas != null)
            barCanvas.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (owner == null) return;

        transform.localPosition = offset;
    }
}
