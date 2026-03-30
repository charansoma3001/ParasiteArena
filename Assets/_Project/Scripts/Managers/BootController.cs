using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The slider representing the loading progress (0 to 1).")]
    public Slider progressBar;

    [Header("Settings")]
    public string mainMenuSceneName = "01_MainMenu";
    [Tooltip("Minimum time in seconds to show the boot screen to avoid a 'flicker' on fast loads.")]
    public float minimumBootTime = 2.0f;

    private void Start()
    {
        if (progressBar != null)
            progressBar.value = 0f;

        StartCoroutine(LoadMainMenuAsync());
    }

    private IEnumerator LoadMainMenuAsync()
    {
        float startTime = Time.time;
        
        // Start loading the Main Menu in the background
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mainMenuSceneName);
        
        // Prevent the scene from switching until we finish our 'artificial' minimum wait time
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float elapsed = Time.time - startTime;
            
            // 1. Calculate progress based on TIME (how far through the 2s we are)
            float timeProgress = Mathf.Clamp01(elapsed / minimumBootTime);
            
            // 2. Calculate progress based on ASSET LOADING (0.0 to 0.9)
            float sceneProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // Use the lower of the two to ensure the bar fills smoothly and doesn't finish early
            float finalProgress = Mathf.Min(timeProgress, sceneProgress);

            if (progressBar != null)
                progressBar.value = finalProgress;

            // If we have finished loading AND we have waited long enough, swap scenes!
            if (asyncLoad.progress >= 0.9f && elapsed >= minimumBootTime)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
