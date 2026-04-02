using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootController : MonoBehaviour
{
    [Header("UI References")]
    public Slider progressBar;

    [Header("Settings")]
    public string mainMenuSceneName = "01_MainMenu";
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
        
        // to start loading the Main Menu in the background
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mainMenuSceneName);
        
        // to not switch to scenes until a minimum wait time
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float elapsed = Time.time - startTime;
            
            // Calculate time
            float timeProgress = Mathf.Clamp01(elapsed / minimumBootTime);
            
            // Calculate progress of loading assets
            float sceneProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // Use the lower to not finish early
            float finalProgress = Mathf.Min(timeProgress, sceneProgress);

            if (progressBar != null)
                progressBar.value = finalProgress;

            // If loading is finished, swap scenes
            if (asyncLoad.progress >= 0.9f && elapsed >= minimumBootTime)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
