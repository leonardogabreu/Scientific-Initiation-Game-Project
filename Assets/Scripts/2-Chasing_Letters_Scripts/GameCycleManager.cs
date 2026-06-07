using System;
using UnityEngine;
using UnityEngine.AI;


public enum GameStateCL { Start, Playing, Over }
public class GameCycleManager : MonoBehaviour
{
    [SerializeField] ChasingLettersGameManager gameManager;
    [Header("Game Loop")]
    public GameStateCL currentGameState = GameStateCL.Start;
    [SerializeField] private int wordsToWin = 3;
    private int correctWords = 0;
    public event Action<GameStateCL> onGameStateChanged;

    public NavMeshAgent navMeshAgent;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject playingPanel;
    public GameObject[] starsArray;
    public GameObject[] starsOutlineArray;
    public GameObject gameOverPanel;

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

                foreach (GameObject letterBox in gameManager.letterBoxesInstances)
                {
                    letterBox.SetActive(false);
                }
            }
            else
            {
                currentGameState = GameStateCL.Over;
                startPanel.SetActive(false);
                playingPanel.SetActive(false);
                gameOverPanel.SetActive(true); 

                navMeshAgent.ResetPath();
                // Atualizar os painéis que eu vou colocar (não só nesse, mas nos outros tbm), etc.
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
}
