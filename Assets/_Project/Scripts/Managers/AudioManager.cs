using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("Global 2D AudioSource for background music loops.")]
    public AudioSource musicSource;
    [Tooltip("Global 2D AudioSource for non-spatialized SFX (like UI clicks).")]
    public AudioSource sfxSource;

    [Header("Global Music")]
    [Tooltip("The default background music to play upon scene start.")]
    public AudioClip backgroundMusic;

    [Header("Game State SFX")]
    public AudioClip gameOverSfx;
    public AudioClip gameWinSfx;
    public AudioClip levelUpSfx;
    public AudioClip bossWaveSfx;

    private void Awake()
    {
        // Enforce Singleton pattern and preserve across scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When a new scene loads, check if we should play/restart background music.
        // This handles cases like restarting after death or playing from the main menu.
        if (backgroundMusic != null)
        {
            if (musicSource != null && !musicSource.isPlaying)
            {
                PlayMusic(backgroundMusic);
            }
        }
    }

    private void Start()
    {
        if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    /// <summary>
    /// Plays background music as a 2D global track.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    /// <summary>
    /// Plays a sound effect globally (2D) without positional attenuation.
    /// Useful for UI, level up tones, etc.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Plays a 3D sound effect at a specific world position.
    /// Note: This instantiates a temporary AudioSource object under the hood.
    /// </summary>
    public void PlaySFXAtPos(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlayGameOver()
    {
        StopMusic();
        PlaySFX(gameOverSfx);
    }

    public void PlayGameWin() 
    {
        StopMusic();
        PlaySFX(gameWinSfx);
    }
    
    public void PlayLevelUp() => PlaySFX(levelUpSfx);
    public void PlayBossWave() => PlaySFX(bossWaveSfx);
}
