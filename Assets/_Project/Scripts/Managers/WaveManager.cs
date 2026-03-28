using UnityEngine;
using System;

// 1. Define the possible states of your game
public enum GameState 
{ 
    PrepPhase,  // Player is in the shop / waiting
    WaveActive, // Enemies are spawning and attacking
    GameOver    // Player died
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Settings")]
    public float timePerWave = 60f; // 60 seconds per wave
    
    // Read-only properties so other scripts can look, but not change them
    public GameState CurrentState { get; private set; }
    public int CurrentWave { get; private set; } = 0;
    public float WaveTimer { get; private set; }
    public float PrepCountdown { get; private set; } = 0f;
    private float _prepCountdownMax = 3f; // Seconds until next wave

    // 2. The Events (Terry and Gagan will listen to these)
    public event Action<int> OnWaveStarted;      // Broadcasts: (WaveNumber)
    public event Action OnWaveEnded;             // Broadcasts when the timer hits 0
    public event Action<float> OnTimerChanged;   // Broadcasts every frame for the UI clock
    public event Action<float> OnCountdownChanged; // Broadcasts countdown during prep phase

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 1. Start in the Prep Phase so the game doesn't instantly spawn enemies on frame 1
        ChangeState(GameState.PrepPhase);
        
        // 2. Immediately start Wave 1 countdown (no initial delay)
        StartNextWave();
    }

    private void Update()
    {
        // Tick the countdown during prep phase
        if (CurrentState == GameState.PrepPhase && PrepCountdown > 0)
        {
            PrepCountdown -= Time.deltaTime;
            OnCountdownChanged?.Invoke(Mathf.Max(0, PrepCountdown));
            
            // When countdown reaches 0, actually begin the wave
            if (PrepCountdown <= 0)
            {
                BeginWave();
            }
        }
        
        // Only tick the clock if a wave is actually happening
        if (CurrentState == GameState.WaveActive)
        {
            WaveTimer -= Time.deltaTime;
            
            // Tell the UI to update the clock
            OnTimerChanged?.Invoke(WaveTimer);

            if (WaveTimer <= 0)
            {
                EndWave();
            }
        }
    }

    // --- GAME LOOP LOGIC ---

    // You will link this to a "Start Wave" button on your Shop Canvas
    public void StartNextWave()
    {
        if (CurrentState != GameState.PrepPhase) return;
        if (PrepCountdown > 0) return; // Already counting down to next wave

        // Start the countdown to wave start
        PrepCountdown = _prepCountdownMax;
        OnCountdownChanged?.Invoke(PrepCountdown);
        
        // Close the shop
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.CloseShop();
        }
    }

    private void BeginWave()
    {
        CurrentWave++;
        WaveTimer = timePerWave;
        ChangeState(GameState.WaveActive);
        
        OnWaveStarted?.Invoke(CurrentWave);
        Debug.Log($"*** WAVE {CurrentWave} STARTED ***");
    }

    private void EndWave()
    {
        ChangeState(GameState.PrepPhase);
        
        // Reset countdown so player can start next wave when ready
        PrepCountdown = 0f;
        OnCountdownChanged?.Invoke(0f);
        
        // Tell Terry's spawner to stop, and kill all remaining enemies
        OnWaveEnded?.Invoke(); 
        
        Debug.Log($"*** WAVE {CurrentWave} ENDED ***");

        TriggerShop();
    }

    private void TriggerShop()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OpenShop();
        }
    }

    private void ChangeState(GameState newState)
    {
        CurrentState = newState;
    }
}