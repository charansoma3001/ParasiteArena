using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Required for Unity's modern text system

public class UIManager : MonoBehaviour
{
    [Header("UI Text References")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI timerText;

    [Header("Stat HUD Text References")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI attackDamageText;
    public TextMeshProUGUI dashCooldownText;
    public TextMeshProUGUI possessionTimeText;
    public TextMeshProUGUI decayRateText;

    [Header("Message HUD")]
    public GameObject messageHUDPanel;
    public TextMeshProUGUI messageHUDText;
    public float defaultMessageDuration = 1.5f;

    private PlayerController _player;
    private WaveManager _waveManager;
    private bool _isWaveTimerSubscribed;
    private Coroutine _messageHUDRoutine;
    private readonly Queue<QueuedHUDMessage> _messageQueue = new Queue<QueuedHUDMessage>();

    private struct QueuedHUDMessage
    {
        public string Text;
        public float Duration;
    }

    private void Start()
    {
        PlayerController.OnHealthChanged += UpdateHealthUI;

        HideAndClearMessageHUD();

        if (timerText == null)
        {
            Debug.LogWarning("UIManager timerText is not assigned in the Inspector.");
        }

        TryInitializeWaveTimerUI();

        _player = FindFirstObjectByType<PlayerController>();

        if (_player != null)
            UpdateHealthUI(_player.CurrentHealth, _player.maxHealth);

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatsChanged += UpdateStatHUD;
            UpdateStatHUD();
        }

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

    private void Update()
    {
        // Handle cases where WaveManager appears slightly later than this UI object.
        if (!_isWaveTimerSubscribed)
        {
            TryInitializeWaveTimerUI();
        }
    }

    private void OnDestroy()
    {
        PlayerController.OnHealthChanged -= UpdateHealthUI;
        if (_waveManager != null && _isWaveTimerSubscribed)
        {
            _waveManager.OnTimerChanged -= UpdateTimerUI;
        }

        if (_messageHUDRoutine != null)
        {
            StopCoroutine(_messageHUDRoutine);
            _messageHUDRoutine = null;
        }

        _messageQueue.Clear();

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatsChanged -= UpdateStatHUD;
        }

        // CRITICAL: Always unsubscribe when this object is destroyed to prevent memory leaks!
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp -= UpdateLevelUI;
            ProgressionManager.Instance.OnXPAdded -= UpdateXPUI;
            ProgressionManager.Instance.OnGoldChanged -= UpdateGoldUI;
        }
    }

    private void TryInitializeWaveTimerUI()
    {
        if (_isWaveTimerSubscribed)
            return;

        _waveManager = WaveManager.Instance;
        if (_waveManager == null)
            return;

        _waveManager.OnTimerChanged += UpdateTimerUI;
        _isWaveTimerSubscribed = true;

        // Push an immediate value so the timer text never stays as blank/default.
        float initialTime = _waveManager.CurrentState == GameState.WaveActive
            ? _waveManager.WaveTimer
            : _waveManager.timePerWave;
        UpdateTimerUI(initialTime);
    }

    // --- The Event Listeners ---

    private void UpdateLevelUI(int newLevel)
    {
        if (levelText != null)
            levelText.text = "Level: " + newLevel.ToString();
    }

    private void UpdateXPUI(int currentXP, int xpToNextLevel)
    {
        if (xpText != null)
            xpText.text = "XP: " + currentXP.ToString() + " / " + xpToNextLevel.ToString();
    }

    private void UpdateGoldUI(int currentGold)
    {
        if (goldText != null)
            goldText.text = "Gold: " + currentGold.ToString();
    }

    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (healthText != null)
            healthText.text = (currentHealth.ToString() + " / " + maxHealth.ToString()).Trim();
    }

    private void UpdateTimerUI(float timeLeft)
    {
        // Format the raw float into clean seconds (e.g. 59, 58, 57...)
        if (timerText != null)
            timerText.text = "Time: " + Mathf.Max(0, Mathf.CeilToInt(timeLeft)).ToString();
    }

    private void UpdateStatHUD()
    {
        if (StatManager.Instance == null)
            return;

        UpdateStatLine(speedText, "Speed", StatType.MovementSpeed);
        UpdateStatLine(attackDamageText, "Attack", StatType.AttackDamage);
        UpdateStatLine(dashCooldownText, "Dash CD", StatType.DashCooldown);
        UpdateStatLine(possessionTimeText, "Possession", StatType.PossessionDuration);
        UpdateStatLine(decayRateText, "Decay", StatType.HostDecayRate);
    }

    private void UpdateStatLine(TextMeshProUGUI targetText, string label, StatType statType)
    {
        if (targetText == null)
            return;

        float value = StatManager.Instance.GetStat(statType);
        if (value != 0f)
        {
            targetText.text = value.ToString();
        }
        else
        {
            targetText.text = "-";
        }
    }

    public void ShowMessageHUD(string message, float duration = -1f)
    {
        if (messageHUDPanel == null || messageHUDText == null)
        {
            Debug.LogWarning("UIManager MessageHUD references are not assigned in the Inspector.");
            return;
        }

        float showDuration = duration > 0f ? duration : defaultMessageDuration;
        _messageQueue.Enqueue(new QueuedHUDMessage
        {
            Text = message,
            Duration = showDuration
        });

        if (_messageHUDRoutine == null)
            _messageHUDRoutine = StartCoroutine(ProcessMessageHUDQueue());
    }

    private IEnumerator ProcessMessageHUDQueue()
    {
        while (_messageQueue.Count > 0)
        {
            QueuedHUDMessage queuedMessage = _messageQueue.Dequeue();
            messageHUDText.text = queuedMessage.Text;
            messageHUDPanel.SetActive(true);
            yield return new WaitForSeconds(queuedMessage.Duration);
            HideAndClearMessageHUD();
        }

        HideAndClearMessageHUD();
        _messageHUDRoutine = null;
    }

    private void HideAndClearMessageHUD()
    {
        if (messageHUDText != null)
            messageHUDText.text = string.Empty;

        if (messageHUDPanel != null)
            messageHUDPanel.SetActive(false);
    }
}