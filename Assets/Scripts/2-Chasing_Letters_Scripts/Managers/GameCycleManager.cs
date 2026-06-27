using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;


public enum GameStateCL { Start, Playing, Over }
public class GameCycleManager : MonoBehaviour
{
    [SerializeField] ChasingLettersGameManager gameManager;
    [SerializeField] DataCollectionManager dataCollectionManager;

    [Header("Game Loop")]
    public GameStateCL currentGameState = GameStateCL.Start;
    [SerializeField] private int wordsToWin = 3;
    private int correctWords = 0;
    public event Action<GameStateCL> onGameStateChanged;

    public NavMeshAgent navMeshAgent;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject playingPanel;
    public GameObject gameOverPanel;
    public GameObject[] starsArray;
    public GameObject[] starsOutlineArray;

    void Start()
    {
        changeGameState(GameStateCL.Start);
    }

    public void changeGameState(GameStateCL gameStateCL)
    {
        if(startPanel != null && playingPanel != null && gameOverPanel != null){
            if(gameStateCL == GameStateCL.Start)
            {
                currentGameState = GameStateCL.Start;
                startPanel.SetActive(true);
                playingPanel.SetActive(false);
                gameOverPanel.SetActive(false);

                correctWords = 0;
            }
            else if(gameStateCL == GameStateCL.Playing  && starsArray != null && starsOutlineArray != null)
            {
                currentGameState = GameStateCL.Playing;
                startPanel.SetActive(false);
                playingPanel.SetActive(true);
                gameOverPanel.SetActive(false);

                foreach(GameObject star in starsArray)
                {
                    star.SetActive(false);
                }
                foreach(GameObject starOutline in starsOutlineArray)
                {
                    starOutline.SetActive(true);
                }

                if (gameManager.letterBoxesInstances != null)
                {
                    foreach (GameObject letterBox in gameManager.letterBoxesInstances)
                    {
                        if (letterBox != null) 
                        {
                            letterBox.SetActive(false);
                        }
                    }
                }

                // Starts new game session
                if (dataCollectionManager != null && gameManager != null)
                {
                    dataCollectionManager.startNewGameSession();
                    dataCollectionManager.startNewWordAttempt(gameManager.targetWord); // Inicia a primeira palavra
                }
            }
            else
            {
                currentGameState = GameStateCL.Over;
                
                startPanel.SetActive(false);
                playingPanel.SetActive(false);
                gameOverPanel.SetActive(true); 

                navMeshAgent.ResetPath();

                dataCollectionManager.finishAndSaveSession();
            }

            if (onGameStateChanged != null)
            {   
                onGameStateChanged(gameStateCL);
            }
        }
    }
    
    public void addScore()
    {
        correctWords++;

        if(starsArray != null && starsOutlineArray != null)
        {
            starsArray[correctWords-1].SetActive(true);
            starsOutlineArray[correctWords-1].SetActive(false);
        }

        if(correctWords >= wordsToWin)
        {
            changeGameState(GameStateCL.Over);
        }

    }

    public void StartGameFromButton(){
        changeGameState(GameStateCL.Playing);
    }

    public void MainMenuFromButton()
    {
        SceneManager.LoadScene("0-Main_Menu");
    }
}
