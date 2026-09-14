using UnityEngine;

public class SubmitZoneManager : MonoBehaviour
{
    public static SubmitZoneManager Instance { get; private set; }
    [SerializeField] private DeliverTablesManager deliverTablesManager;

    [Header("Audio & VFX")]
    public AudioClip deliverCorrectSFX;
    public AudioClip deliverWrongSFX;
    public AudioSource audioSource;
    public GameObject deliverRightVFX;
    public GameObject deliverWrongVFX;
    

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

    public void EvaluateWord()
    {
        if (deliverTablesManager == null) return;

        // Lida antes da avaliação: o reset das mesas apaga o que o aluno montou.
        string submittedWord = deliverTablesManager.GetBuiltWord();

        // Slot vazio não contribui caractere nenhum: entrega com mesas pela metade dá uma
        // palavra mais curta. Isso é entrega incompleta, não erro de conteúdo.
        bool isCompleteAttempt = submittedWord.Length == ChasingLettersGameManager.Instance?.targetWord?.Length;

        if (deliverTablesManager.CheckSubmit())
        {
            HandleCorrectWord();
        }
        else
        {
            HandleWrongWord(submittedWord, isCompleteAttempt);
        }
    }

    private void HandleCorrectWord()
    {
        DataCollectionManager.Instance?.FinishWordAttempt(); // Saves the correct word

        audioSource?.PlayOneShot(deliverCorrectSFX);
        Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);

        deliverTablesManager.ResetAllTables();

        GameCycleManager.Instance?.addScore(); // pode encerrar a sessão na última palavra

        if (GameCycleManager.Instance != null && GameCycleManager.Instance.currentGameState != GameStateCL.Playing) return;

        // A próxima palavra vem da plataforma: as mesas são remontadas pelo onWordChanged e a
        // nova tentativa só começa a contar tempo quando a palavra existe.
        ChasingLettersGameManager.Instance?.SelectNewWord(() =>
        {
            if (DataCollectionManager.Instance != null && ChasingLettersGameManager.Instance != null)
            {
                DataCollectionManager.Instance.StartNewWordAttempt(
                    ChasingLettersGameManager.Instance.targetWord,
                    ChasingLettersGameManager.Instance.CurrentChallengeId);
            }
        });
    }

    private void HandleWrongWord(string submittedWord, bool isCompleteAttempt)
    {
        audioSource?.PlayOneShot(deliverWrongSFX);
        Instantiate(deliverWrongVFX, transform.position, Quaternion.identity);

        DataCollectionManager.Instance?.RegisterMistake(submittedWord, isCompleteAttempt);
    }
}
