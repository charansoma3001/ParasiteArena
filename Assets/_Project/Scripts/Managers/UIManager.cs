using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Required for Unity's modern text system

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

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

    [Header("Controls HUD")]
    public GameObject controlsHUDPanel;

    [Header("Pause Menu")]
    public GameObject gamePausedPanel;

    [Header("Game Over Screen")]
    public GameObject gameOverPanel;

    [Header("Wave Countdown")]
    public TextMeshProUGUI waveCountdownText;

    private PlayerController _player;
    private WaveManager _waveManager;
    private bool _isWaveTimerSubscribed;
    private Coroutine _messageHUDRoutine;
    private Coroutine _controlsHUDRoutine;
    private bool _isGamePaused;
    private readonly Queue<QueuedHUDMessage> _messageQueue = new Queue<QueuedHUDMessage>();

    private struct QueuedHUDMessage
    {
        public string Text;
        public float Duration;
    }

    private void Awake()
    {
        // Singleton initialization
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        PlayerController.OnHealthChanged += UpdateHealthUI;
        PlayerController.OnPlayerDied += ShowGameOverScreen;

        HideAndClearMessageHUD();
        HideControlsHUD();
        HideGamePausedPanel();
        HideGameOverScreen();
        HideWaveCountdown();

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
            Debug.LogError("UIManager couldn't find the ProgressionManager! (This is common in menu screen)");
        }
    }

    private void Update()
    {
        // Handle cases where WaveManager appears slightly later than this UI object.
        if (!_isWaveTimerSubscribed)
        {
            TryInitializeWaveTimerUI();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleGamePausedPanel();
        }
    }

    private void OnDestroy()
    {
        PlayerController.OnHealthChanged -= UpdateHealthUI;
        if (_waveManager != null && _isWaveTimerSubscribed)
        {
            _waveManager.OnTimerChanged -= UpdateTimerUI;
            _waveManager.OnCountdownChanged -= UpdateWaveCountdownUI;
        }

        if (_messageHUDRoutine != null)
        {
            StopCoroutine(_messageHUDRoutine);
            _messageHUDRoutine = null;
        }

        if (_controlsHUDRoutine != null)
        {
            StopCoroutine(_controlsHUDRoutine);
            _controlsHUDRoutine = null;
        }

        _messageQueue.Clear();

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatsChanged -= UpdateStatHUD;
        }

        if (_isGamePaused)
        {
            Time.timeScale = 1f;
            _isGamePaused = false;
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
        _waveManager.OnCountdownChanged += UpdateWaveCountdownUI;
        _isWaveTimerSubscribed = true;

        // Push an immediate value so the timer text never stays as blank/default.
        float initialTime = _waveManager.CurrentState == GameState.WaveActive
            ? _waveManager.WaveTimer
            : _waveManager.timePerWave;
        UpdateTimerUI(initialTime);
        
        // Push initial countdown value
        UpdateWaveCountdownUI(_waveManager.PrepCountdown);
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

    private void UpdateWaveCountdownUI(float countdownTime)
    {
        if (waveCountdownText != null)
        {
            waveCountdownText.text = "Wave starts in: " + Mathf.CeilToInt(countdownTime).ToString();
            
            // Show countdown when it's counting down
            if (countdownTime > 0)
                ShowWaveCountdown();
            else
                HideWaveCountdown();
        }
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

    public void ShowControlsHUD()
    {
        if (controlsHUDPanel == null)
        {
            Debug.LogWarning("UIManager controlsHUDPanel is not assigned in the Inspector.");
            return;
        }

        controlsHUDPanel.SetActive(true);
    }

    public void HideControlsHUD()
    {
        if (controlsHUDPanel != null)
            controlsHUDPanel.SetActive(false);
    }

    private bool IsControlsHUDOpen()
    {
        return controlsHUDPanel != null && controlsHUDPanel.activeSelf;
    }

    public void ToggleGamePausedPanel()
    {
        if (_isGamePaused)
            HideGamePausedPanel();
        else
            ShowGamePausedPanel();
    }

    public void ShowGamePausedPanel()
    {
        _isGamePaused = true;
        Time.timeScale = 0f;

        if (gamePausedPanel != null)
            gamePausedPanel.SetActive(true);
        else
            Debug.LogWarning("UIManager gamePausedPanel is not assigned in the Inspector.");
    }

    public void HideGamePausedPanel()
    {
        if (IsControlsHUDOpen())
            return;

        _isGamePaused = false;
        Time.timeScale = 1f;

        if (gamePausedPanel != null)
            gamePausedPanel.SetActive(false);
    }

    private void ShowWaveCountdown()
    {
        if (waveCountdownText != null)
            waveCountdownText.gameObject.SetActive(true);
    }

    private void HideWaveCountdown()
    {
        if (waveCountdownText != null)
            waveCountdownText.gameObject.SetActive(false);
    }

    public void ShowGameOverScreen()
    {
        Time.timeScale = 0f; // Freeze the game

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        else
            Debug.LogWarning("UIManager gameOverPanel is not assigned in the Inspector.");
    }

    private void HideGameOverScreen()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}