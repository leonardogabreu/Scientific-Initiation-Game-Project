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
        DataCollectionManager.Instance?.FinishWordAttempt(); // Saves the correct word

        deliverTablesManager.ResetAllTables();
        WordSelectionManager.Instance?.SelectNewWord();
        deliverTablesManager.SpawnTables();

        if (DataCollectionManager.Instance != null && WordSelectionManager.Instance != null)
        {
            DataCollectionManager.Instance?.StartNewWordAttempt(WordSelectionManager.Instance?.TargetWord ?? ""); // Tracks new word
        }

        GameCycleManager.Instance?.addScore();

        audioSource?.PlayOneShot(deliverCorrectSFX);
        Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);
    }

    private void HandleWrongWord()
    {
        audioSource?.PlayOneShot(deliverWrongSFX);
        Instantiate(deliverWrongVFX, transform.position, Quaternion.identity);

        DataCollectionManager.Instance?.RegisterMistake();
    }
}