using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public string nextSceneName;
    public GameObject tutorialPanel;

    public void loadNextScene()
    {
        if (nextSceneName != null)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    public void showTutorial()
    {
        if(tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }
    }
    public void hideTutorial()
    {
        if(tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
}