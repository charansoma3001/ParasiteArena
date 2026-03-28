using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class GameManager : MonoBehaviour
{
    public string mainMenuScene = "MainMenu";
    public string gameScene = "Game";

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