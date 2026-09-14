using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;


public enum GameStateCL { Start, Playing, Over }
public class GameCycleManager : MonoBehaviour
{
    public static GameCycleManager Instance { get; private set; }

    [Header("Game Loop")]
    public GameStateCL currentGameState = GameStateCL.Start;
    [SerializeField] private int wordsToWin = 3;
    [Tooltip("Segundos de espera pela primeira palavra da plataforma antes de começar assim mesmo.")]
    [SerializeField] private float firstWordTimeout = 20f;
    private int correctWords = 0;
    public event Action<GameStateCL> onGameStateChanged;

    public NavMeshAgent navMeshAgent;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject playingPanel;
    public GameObject gameOverPanel;
    public GameObject[] starsArray;
    public GameObject[] starsOutlineArray;

    private bool isStartingGame;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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

            if (DataCollectionManager.Instance != null && ChasingLettersGameManager.Instance != null)
            {
                DataCollectionManager.Instance.ReportSessionStarted(wordsToWin);
                DataCollectionManager.Instance.StartNewWordAttempt(
                    ChasingLettersGameManager.Instance.targetWord,
                    ChasingLettersGameManager.Instance.CurrentChallengeId);
            }
            break;

        case GameStateCL.Over:
            currentGameState = GameStateCL.Over;
            startPanel.SetActive(false);
            playingPanel.SetActive(false);
            gameOverPanel.SetActive(true);

            navMeshAgent?.ResetPath();

            DataCollectionManager.Instance?.ReportSessionFinished();

            // O botão do painel de game over reinicia direto em Playing, sem passar por Start:
            // deixa a próxima palavra (e as mesas) prontas para essa nova partida.
            ChasingLettersGameManager.Instance?.SelectNewWord();
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

    public void StartGameFromButton()
    {
        if (isStartingGame) return;

        StartCoroutine(StartGameWhenWordIsReady());
    }

    // A primeira palavra é buscada na plataforma já no Start da cena, mas se a rede estiver
    // lenta o painel inicial continua na tela até ela chegar — começar sem palavra deixaria a
    // esteira sem letras certas e as mesas vazias.
    private IEnumerator StartGameWhenWordIsReady()
    {
        isStartingGame = true;

        float remaining = firstWordTimeout;
        while (ChasingLettersGameManager.Instance != null && !ChasingLettersGameManager.Instance.IsWordReady && remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (ChasingLettersGameManager.Instance != null && !ChasingLettersGameManager.Instance.IsWordReady)
        {
            Debug.LogError("[GameCycleManager] Nenhuma palavra disponível — iniciando assim mesmo.");
        }

        isStartingGame = false;
        changeGameState(GameStateCL.Playing);
    }

    public void MainMenuFromButton()
    {
        SceneManager.LoadScene("0-Main_Menu");
    }
}
