using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{

    public SceneManager sceneManager;
    public String nextSceneName;
    [Header("Paineis Principais")]
    public GameObject mainMenuPanel; 
    public GameObject tutorialContainer;
    public GameObject settingsMenu;

    [Header("Paginas do Tutorial")]
    public GameObject[] tutorialPages; 

    private int currentIndex = 0;

    void Start()
    {
        closeTutorial(); 
        closeSettingsMenu();
    }

    public void openTutorial()
    {
        if (mainMenuPanel != null) 
        {
            mainMenuPanel.SetActive(false);
        }
        
        if (tutorialContainer != null) 
        {
            tutorialContainer.SetActive(true);
        }
        
        currentIndex = 0;
        updateTutorialUI();
    }

    public void closeTutorial()
    {
        if (tutorialContainer != null) 
        {
            tutorialContainer.SetActive(false);
        }
        
        if (mainMenuPanel != null) 
        {
            mainMenuPanel.SetActive(true);
        }
    }

    public void nextPage()
    {
        if (tutorialPages != null)
        {
            if (currentIndex < tutorialPages.Length - 1)
            {
                currentIndex++;
                updateTutorialUI();
            }
        }
    }

    public void prevPage()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            updateTutorialUI();
        }
    }

    public void goToPage(int pageIndex)
    {
        if (tutorialPages != null)
        {
            if (pageIndex >= 0 && pageIndex < tutorialPages.Length)
            {
                currentIndex = pageIndex;
                updateTutorialUI();
            }
        }
    }

    private void updateTutorialUI()
    {
        if (tutorialPages != null)
        {
            for (int i = 0; i < tutorialPages.Length; i++)
            {
                if (tutorialPages[i] != null)
                {
                    tutorialPages[i].SetActive(i == currentIndex);
                }
            }
        }
    }
    public void openSettingsMenu()
    {
        if(settingsMenu != null) settingsMenu.SetActive(true);   
    }

    public void closeSettingsMenu()
    {
        if(settingsMenu != null) settingsMenu.SetActive(false);   
    }
    
    public void loadNextScene()
    {
        if (nextSceneName != null)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}