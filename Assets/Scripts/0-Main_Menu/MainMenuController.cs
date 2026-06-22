using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{

    public SceneManager sceneManager;
    public String nextSceneName;
    [Header("Paineis Principais")]
    public GameObject mainMenuPanel; 
    public GameObject tutorialContainer; 

    [Header("Paginas do Tutorial")]
    public GameObject[] tutorialPages; 

    private int currentIndex = 0;

    void Start()
    {
        closeTutorial(); 
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
        updateUI();
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
                updateUI();
            }
        }
    }

    public void prevPage()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            updateUI();
        }
    }

    public void goToPage(int pageIndex)
    {
        if (tutorialPages != null)
        {
            if (pageIndex >= 0 && pageIndex < tutorialPages.Length)
            {
                currentIndex = pageIndex;
                updateUI();
            }
        }
    }

    private void updateUI()
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
    
    public void loadNextScene()
    {
        if (nextSceneName != null)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}