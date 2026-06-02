using UnityEngine;
using TMPro;
using UnityEngine.AI;
using Unity.VisualScripting;

public enum gameState { Start, Playing, Over }

public class GameManager : MonoBehaviour
{
    public float gameTimer = 60.0f;
    public int currentScore = 0;
    public int currentCombo = 0;
    public GameObject player;
    public Transform cursorVisual;
    public NavMeshAgent navMeshAgent;
    
    [Header("UI Panels")]
    public GameObject gameOverPanel;
    public GameObject HUDPanel;
    public GameObject startPanel;
    
    [Header("HUD Texts")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;

    [Header("Game Over Texts")]
    public TextMeshProUGUI finalScore;
    public TextMeshProUGUI newRecordText;
    
    public gameState currentGameState;
    
    void Start()
    {
        changeGameState(gameState.Start);
    }

    void Update()
    {
        if(currentGameState == gameState.Playing && gameTimer > 0)
        {
            gameTimer -= Time.deltaTime;
            // Arredonda o tempo para não mostrar casas decimais na tela
            timerText.text = "Time: " + Mathf.Ceil(gameTimer).ToString();
        }
        else if (currentGameState == gameState.Playing && gameTimer <= 0)
        {
            changeGameState(gameState.Over);
        }
        else if(Input.GetKeyDown(KeyCode.Space)&& currentGameState == gameState.Over)
        {
            changeGameState(gameState.Playing);
        } 
        else if(Input.GetKeyDown(KeyCode.Space) && currentGameState == gameState.Start)
        {
            changeGameState(gameState.Playing);
        }
    }

    public void changeGameState(gameState newGameState)
    {
        if(newGameState == gameState.Playing)
        {
            startPanel.SetActive(false);
            HUDPanel.SetActive(true);
            gameOverPanel.SetActive(false);
            
            // Resets all values
            player.transform.position = new Vector3 (0, 0, 0);
            cursorVisual.position = new Vector3 (0, 0.1f, 0);
            navMeshAgent.ResetPath();
            
            gameTimer = 60.0f;
            currentScore = 0;
            currentCombo = 0;
            UpdateHUD();
            
            currentGameState = gameState.Playing;
        } 
        else if(newGameState == gameState.Over)
        {
            startPanel.SetActive(false);
            HUDPanel.SetActive(false);
            gameOverPanel.SetActive(true);
            navMeshAgent.ResetPath();
            
            // Resets timer
            gameTimer = 0.0f;
            timerText.text = "Time: 0";
            
            // Highscore logic
            finalScore.text = "" + currentScore;
            int record = PlayerPrefs.GetInt("HighScore", 0);
            
            if (currentScore > record)
            {
                PlayerPrefs.SetInt("HighScore", currentScore);
                newRecordText.text = "NOVO RECORDE: " + currentScore + " !";
            }
            else
            {
                newRecordText.text = "Seu Recorde: " + record;
            }

            currentGameState = gameState.Over;
        } 
        else if(newGameState == gameState.Start)
        {
            startPanel.SetActive(true);
            HUDPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            currentGameState = gameState.Start;
        }
    }
    public int RegisterCorrectDelivery()
    {
        int earnedPoints = 100 + (25 * currentCombo);
        currentScore += earnedPoints;
        currentCombo++;
        UpdateHUD();
        return earnedPoints;
    }

    public int RegisterWrongDelivery()
    {
        currentScore -= 50;
        currentCombo = 0;
        UpdateHUD();
        return -50;
    }

    private void UpdateHUD()
    {
        scoreText.text = "Score: " + currentScore;
        comboText.text = "Combo: " + currentCombo;
    }
}