using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public string mainMenuScene = "01_MainMenu";
    public string gameScene = "Sandbox_Charan";

    // PLAY BUTTON
    public void PlayGame()
    {
        SceneManager.LoadScene(gameScene);
    }

    // EXIT BUTTON
    public void ExitGame()
    {
        Application.Quit();

        // For unity editor
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // RESTART BUTTON
    public void RestartGame()
    {
        SceneManager.LoadScene(gameScene);
    }

    // MAIN MENU BUTTON
    public void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}