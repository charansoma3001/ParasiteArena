using UnityEngine;
using TMPro; // Required for Unity's modern text system

public class UIManager : MonoBehaviour
{
    [Header("UI Text References")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI healthText;

    private PlayerController _player;

    private void Start()
    {
        PlayerController.OnHealthChanged += UpdateHealthUI;
        _player = FindFirstObjectByType<PlayerController>();

        if (_player != null)
            UpdateHealthUI(_player.CurrentHealth, _player.maxHealth);

        // 1. Subscribe to the events broadcasting from ProgressionManager
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp += UpdateLevelUI;
            ProgressionManager.Instance.OnXPAdded += UpdateXPUI;
            ProgressionManager.Instance.OnGoldChanged += UpdateGoldUI;

            // 2. Force an initial update so the screen doesn't say "Text" when you press Play
            UpdateLevelUI(ProgressionManager.Instance.CurrentLevel);
            UpdateXPUI(ProgressionManager.Instance.CurrentXP, ProgressionManager.Instance.XPToNextLevel);
            UpdateGoldUI(ProgressionManager.Instance.CurrentGold);
        }
        else
        {
            Debug.LogError("UIManager couldn't find the ProgressionManager!");
        }
    }

    private void OnDestroy()
    {
        PlayerController.OnHealthChanged -= UpdateHealthUI;

        // CRITICAL: Always unsubscribe when this object is destroyed to prevent memory leaks!
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp -= UpdateLevelUI;
            ProgressionManager.Instance.OnXPAdded -= UpdateXPUI;
            ProgressionManager.Instance.OnGoldChanged -= UpdateGoldUI;
        }
    }

    // --- The Event Listeners ---

    private void UpdateLevelUI(int newLevel)
    {
        levelText.text = "Level: " + newLevel.ToString();
    }

    private void UpdateXPUI(int currentXP, int xpToNextLevel)
    {
        xpText.text = "XP: " + currentXP.ToString() + " / " + xpToNextLevel.ToString();
    }

    private void UpdateGoldUI(int currentGold)
    {
        goldText.text = "Gold: " + currentGold.ToString();
    }

    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        healthText.text = "Health: " + currentHealth.ToString() + " / " + maxHealth.ToString();
    }
}