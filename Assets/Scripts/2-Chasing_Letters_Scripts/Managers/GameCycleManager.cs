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
    if (startPanel == null || playingPanel == null || gameOverPanel == null)
    {
        Debug.LogError("[GameCycleManager] Painéis de UI não configurados no Inspector.");
        return;
    }

    switch (gameStateCL)
    {
        case GameStateCL.Start:
            currentGameState = GameStateCL.Start;
            startPanel.SetActive(true);
            playingPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            correctWords = 0;
            break;

        case GameStateCL.Playing:
            if (starsArray == null || starsOutlineArray == null)
            {
                Debug.LogError("[GameCycleManager] Arrays de estrelas não configurados no Inspector.");
                return;
            }

            currentGameState = GameStateCL.Playing;
            startPanel.SetActive(false);
            playingPanel.SetActive(true);
            gameOverPanel.SetActive(false);

            foreach (GameObject star in starsArray) star.SetActive(false);
            foreach (GameObject starOutline in starsOutlineArray) starOutline.SetActive(true);

            gameManager?.letterBoxesInstances?.ForEach(box => box?.SetActive(false));

            if (dataCollectionManager != null && gameManager != null)
            {
                dataCollectionManager.startNewGameSession();
                dataCollectionManager.startNewWordAttempt(gameManager.targetWord);
            }
            break;

        case GameStateCL.Over:
            currentGameState = GameStateCL.Over;
            startPanel.SetActive(false);
            playingPanel.SetActive(false);
            gameOverPanel.SetActive(true);

            navMeshAgent?.ResetPath();
            
            if (dataCollectionManager == null)
            {
                Debug.LogError("[GameCycleManager] DataCollectionManager não configurado — sessão NÃO foi salva!");
            }
            else
            {
            dataCollectionManager.finishAndSaveSession();
            }
            break;
        }
    // Observer
    onGameStateChanged?.Invoke(gameStateCL);
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
