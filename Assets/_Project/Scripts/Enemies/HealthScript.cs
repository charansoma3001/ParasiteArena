using UnityEngine;
using UnityEngine.UI;

public class HealthScript : MonoBehaviour
{
    [Header("References")]
    public EnemyController enemy;
    public Image fillImage;
    public Canvas healthCanvas;

    [Header("Behavior")]
    public bool hideWhenPossessed = true;
    public bool hideWhenDead = true;

    private float _lastHp = float.MinValue;
    private bool _lastVisible = true;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponentInParent<EnemyController>();

        if (fillImage == null)
            fillImage = FindFilledImage();

        if (healthCanvas == null)
            healthCanvas = GetComponentInChildren<Canvas>(true);
    }

    private void Start()
    {
        ForceRefresh();
    }

    private void LateUpdate()
    {
        if (enemy == null || enemy.stats == null || fillImage == null) return;

        bool visible = true;
        if (hideWhenDead && enemy.IsDead) visible = false;
        if (hideWhenPossessed && enemy.IsPossessed) visible = false;

        if (visible != _lastVisible)
        {
            SetVisible(visible);
            _lastVisible = visible;
        }

        if (!Mathf.Approximately(_lastHp, enemy.CurrentHP))
        {
            float maxHp = Mathf.Max(1f, enemy.stats.maxHealth);
            fillImage.fillAmount = Mathf.Clamp01(enemy.CurrentHP / maxHp);
            _lastHp = enemy.CurrentHP;
        }
    }

    public void ForceRefresh()
    {
        if (enemy == null || enemy.stats == null || fillImage == null) return;
        _lastHp = float.MinValue;
        _lastVisible = true;
        SetVisible(true);
        float maxHp = Mathf.Max(1f, enemy.stats.maxHealth);
        fillImage.fillAmount = Mathf.Clamp01(enemy.CurrentHP / maxHp);
    }

    private void SetVisible(bool visible)
    {
        if (healthCanvas != null)
            healthCanvas.gameObject.SetActive(visible);
    }

    private Image FindFilledImage()
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.type == Image.Type.Filled)
                return img;
        }

        return images.Length > 0 ? images[0] : null;
    }
}