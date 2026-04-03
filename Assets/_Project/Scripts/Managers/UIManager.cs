using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    private PlayerController player;
    private WaveManager waveManager;
    private bool isWaveTimerSubscribed;
    private Coroutine messageHUDRoutine;
    private Coroutine controlsHUDRoutine;
    private bool isGamePaused;
    private readonly Queue<QueuedHUDMessage> messageQueue = new Queue<QueuedHUDMessage>();

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

        TryInitializeWaveTimerUI();

        player = FindFirstObjectByType<PlayerController>();

        if (player != null)
            UpdateHealthUI(player.CurrentHealth, player.maxHealth);

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatsChanged += UpdateStatHUD;
            UpdateStatHUD();
        }
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp += UpdateLevelUI;
            ProgressionManager.Instance.OnXPAdded += UpdateXPUI;
            ProgressionManager.Instance.OnGoldChanged += UpdateGoldUI;

            UpdateLevelUI(ProgressionManager.Instance.CurrentLevel);
            UpdateXPUI(ProgressionManager.Instance.CurrentXP, ProgressionManager.Instance.XPToNextLevel);
            UpdateGoldUI(ProgressionManager.Instance.CurrentGold);
        }
    }

    private void Update()
    {
        // this is for WaveManager appearing later than this UI object.
        if (!isWaveTimerSubscribed)
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
        if (waveManager != null && isWaveTimerSubscribed)
        {
            waveManager.OnTimerChanged -= UpdateTimerUI;
            waveManager.OnCountdownChanged -= UpdateWaveCountdownUI;
        }

        if (messageHUDRoutine != null)
        {
            StopCoroutine(messageHUDRoutine);
            messageHUDRoutine = null;
        }

        if (controlsHUDRoutine != null)
        {
            StopCoroutine(controlsHUDRoutine);
            controlsHUDRoutine = null;
        }

        messageQueue.Clear();

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnStatsChanged -= UpdateStatHUD;
        }

        if (isGamePaused)
        {
            Time.timeScale = 1f;
            isGamePaused = false;
        }

        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnLevelUp -= UpdateLevelUI;
            ProgressionManager.Instance.OnXPAdded -= UpdateXPUI;
            ProgressionManager.Instance.OnGoldChanged -= UpdateGoldUI;
        }
    }

    private void TryInitializeWaveTimerUI()
    {
        if (isWaveTimerSubscribed)
            return;

        waveManager = WaveManager.Instance;
        if (waveManager == null)
            return;

        waveManager.OnTimerChanged += UpdateTimerUI;
        waveManager.OnCountdownChanged += UpdateWaveCountdownUI;
        isWaveTimerSubscribed = true;

        // immediate value timer doesn't stay blank
        float initialTime = waveManager.CurrentState == GameState.WaveActive
            ? waveManager.WaveTimer
            : waveManager.timePerWave;
        UpdateTimerUI(initialTime);
        
        //initial countdown value
        UpdateWaveCountdownUI(waveManager.PrepCountdown);
    }

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
        if (timerText != null)
            timerText.text = "Time: " + Mathf.Max(0, Mathf.CeilToInt(timeLeft)).ToString();
    }

    private void UpdateWaveCountdownUI(float countdownTime)
    {
        if (waveCountdownText != null)
        {
            waveCountdownText.text = "Wave starts in: " + Mathf.CeilToInt(countdownTime).ToString();
            
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
            return;
        }

        float showDuration = duration > 0f ? duration : defaultMessageDuration;
        messageQueue.Enqueue(new QueuedHUDMessage
        {
            Text = message,
            Duration = showDuration
        });

        if (messageHUDRoutine == null)
            messageHUDRoutine = StartCoroutine(ProcessMessageHUDQueue());
    }

    private IEnumerator ProcessMessageHUDQueue()
    {
        while (messageQueue.Count > 0)
        {
            QueuedHUDMessage queuedMessage = messageQueue.Dequeue();
            messageHUDText.text = queuedMessage.Text;
            messageHUDPanel.SetActive(true);
            yield return new WaitForSeconds(queuedMessage.Duration);
            HideAndClearMessageHUD();
        }

        HideAndClearMessageHUD();
        messageHUDRoutine = null;
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
        if (isGamePaused)
            HideGamePausedPanel();
        else
            ShowGamePausedPanel();
    }

    public void ShowGamePausedPanel()
    {
        isGamePaused = true;
        Time.timeScale = 0f;

        if (gamePausedPanel != null)
            gamePausedPanel.SetActive(true);
    }

    public void HideGamePausedPanel()
    {
        if (IsControlsHUDOpen())
            return;

        isGamePaused = false;
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
        Time.timeScale = 0f;
        
        AudioManager.Instance?.PlayGameOver();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    private void HideGameOverScreen()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}