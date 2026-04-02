using UnityEngine;
using UnityEngine.UI;

public class HealthScript : MonoBehaviour
{
    [Header("References")]
    public EnemyController enemy;
    public Slider healthSlider; 
    public Canvas healthCanvas;

    [Header("Behavior")]
    public bool hideWhenPossessed = true;
    public bool hideWhenDead = true;

    private float lastHp = float.MinValue;
    private bool lastHp = true;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponentInParent<EnemyController>();

        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        if (healthCanvas == null)
            healthCanvas = GetComponentInChildren<Canvas>(true);
    }

    private void Start()
    {
        ForceRefresh();
    }

    private void LateUpdate()
    {
        if (enemy == null || enemy.stats == null || healthSlider == null) return;

        bool visible = true;
        if (hideWhenDead && enemy.IsDead) visible = false;
        if (hideWhenPossessed && enemy.IsPossessed) visible = false;

        if (visible != lastHp)
        {
            SetVisible(visible);
            lastHp = visible;
        }

        if (!Mathf.Approximately(lastHp, enemy.CurrentHP))
        {
            float maxHp = Mathf.Max(1f, enemy.stats.maxHealth);
            healthSlider.value = Mathf.Clamp01(enemy.CurrentHP / maxHp);
            lastHp = enemy.CurrentHP;
        }
    }

    public void ForceRefresh()
    {
        if (enemy == null || enemy.stats == null || healthSlider == null) return;
        lastHp = float.MinValue;
        lastHp = true;
        SetVisible(true);
        
        float maxHp = Mathf.Max(1f, enemy.stats.maxHealth);
        healthSlider.value = Mathf.Clamp01(enemy.CurrentHP / maxHp);
    }

    private void SetVisible(bool visible)
    {
        if (healthCanvas != null)
            healthCanvas.gameObject.SetActive(visible);
    }
}