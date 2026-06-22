using Unity.Mathematics;
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

    public void evaluateWord()
    {
        if(deliverTablesManager != null)
        {
            // Player writes the correct word
            if (deliverTablesManager.checkSubmit())
            {
                if (dataCollectionManager != null)
                {
                    dataCollectionManager.finishWordAttempt(); // Saves the correct word
                }
                if (deliverTablesManager != null)
                {
                    deliverTablesManager.resetAllTables();
                }
                if (gameManager != null)
                {
                  gameManager.SelectNewWord();  
                } 
                if(deliverTablesManager!=null)
                {
                    deliverTablesManager.spawnTables();
                }
                if (dataCollectionManager != null && gameManager != null)
                {
                    dataCollectionManager.startNewWordAttempt(gameManager.targetWord); // Tracks new word
                }
                if(gameCycleManager != null)
                {
                    gameCycleManager.addScore();
                }

            audioSource.PlayOneShot(deliverCorrectSFX);
            Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);            
            }

            // Player misses the word
            else
            {
            audioSource.PlayOneShot(deliverWrongSFX);
            Instantiate(deliverWrongVFX, transform.position, quaternion.identity);

                if (dataCollectionManager != null)
                {
                    dataCollectionManager.registerMistake();
                }
            
            }
        }
    }
}
