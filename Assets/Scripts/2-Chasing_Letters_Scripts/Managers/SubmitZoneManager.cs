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

        if (deliverTablesManager.CheckSubmit())
        {
            HandleCorrectWord();
        }
        else
        {
            HandleWrongWord();
        }
    }

    private void HandleCorrectWord()
    {
        dataCollectionManager?.FinishWordAttempt(); // Saves the correct word

        deliverTablesManager.ResetAllTables();
        gameManager?.SelectNewWord();
        deliverTablesManager.SpawnTables();

        if (dataCollectionManager != null && gameManager != null)
        {
            dataCollectionManager.StartNewWordAttempt(gameManager.targetWord); // Tracks new word
        }

        gameCycleManager?.addScore();

        audioSource?.PlayOneShot(deliverCorrectSFX);
        Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);
    }

    private void HandleWrongWord()
    {
        audioSource?.PlayOneShot(deliverWrongSFX);
        Instantiate(deliverWrongVFX, transform.position, Quaternion.identity);

        dataCollectionManager?.RegisterMistake();
    }
}