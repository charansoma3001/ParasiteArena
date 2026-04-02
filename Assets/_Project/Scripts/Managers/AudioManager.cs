using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource for background music.")]
    public AudioSource musicSource;
    [Tooltip("AudioSource for SFX (like UI clicks).")]
    public AudioSource sfxSource;

    [Header("Global Music")]
    [Tooltip("The default background music to play.")]
    public AudioClip backgroundMusic;

    [Header("Game State SFX")]
    public AudioClip gameOverSfx;
    public AudioClip gameWinSfx;
    public AudioClip levelUpSfx;
    public AudioClip bossWaveSfx;

    private void Awake()
    {
        // This would be used to enforce a singleton pattern
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
        // When a new scene loads.
        // This is to fix the issue of restarting music after death or playing from the main menu.
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

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

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
