using UnityEngine;

public class SubmitZoneManager : MonoBehaviour
{
    [SerializeField] private DeliverTablesManager deliverTablesManager;
    [SerializeField] private ChasingLettersGameManager gameManager;
    [SerializeField] private GameCycleManager gameCycleManager;
    [SerializeField] private DataCollectionManager dataCollectionManager;

    [Header("Audio & VFX")]
    public AudioClip deliverCorrectSFX;
    public AudioClip deliverWrongSFX;
    public AudioSource audioSource;
    public GameObject deliverRightVFX;
    public GameObject deliverWrongVFX;

    public void EvaluateWord()
    {
        if (deliverTablesManager == null) return;

        // Lida antes da avaliação: o reset das mesas apaga o que o aluno montou.
        string submittedWord = deliverTablesManager.GetBuiltWord();

        // Slot vazio não contribui caractere nenhum: entrega com mesas pela metade dá uma
        // palavra mais curta. Isso é entrega incompleta, não erro de conteúdo.
        bool isCompleteAttempt = submittedWord.Length == gameManager?.targetWord?.Length;

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
        dataCollectionManager?.FinishWordAttempt(); // Saves the correct word

        audioSource?.PlayOneShot(deliverCorrectSFX);
        Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);

        deliverTablesManager.ResetAllTables();

        gameCycleManager?.addScore(); // pode encerrar a sessão na última palavra

        if (gameCycleManager != null && gameCycleManager.currentGameState != GameStateCL.Playing) return;

        // A próxima palavra vem da plataforma: as mesas são remontadas pelo onWordChanged e a
        // nova tentativa só começa a contar tempo quando a palavra existe.
        gameManager?.SelectNewWord(() =>
        {
            if (dataCollectionManager != null && gameManager != null)
            {
                dataCollectionManager.StartNewWordAttempt(gameManager.targetWord, gameManager.CurrentChallengeId);
            }
        });
    }

    private void HandleWrongWord(string submittedWord, bool isCompleteAttempt)
    {
        audioSource?.PlayOneShot(deliverWrongSFX);
        Instantiate(deliverWrongVFX, transform.position, Quaternion.identity);

        dataCollectionManager?.RegisterMistake(submittedWord, isCompleteAttempt);
    }
}
