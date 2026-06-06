using Unity.Mathematics;
using UnityEngine;

public class SubmitZoneManager : MonoBehaviour
{
    [SerializeField] private DeliverTablesManager deliverTablesManager;
    [SerializeField] private ChasingLettersGameManager gameManager;
    [SerializeField] private GameCycleManager gameCycleManager;

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
            // VFX e SFX positivos
            if (deliverTablesManager.checkSubmit())
            {
                deliverTablesManager.resetAllTables();
                gameManager.SelectNewWord();
                deliverTablesManager.spawnTables();

                if(gameCycleManager != null)
                {
                    gameCycleManager.addScore();
                }

            audioSource.PlayOneShot(deliverCorrectSFX);
            Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);            
            }

            else
            {
            audioSource.PlayOneShot(deliverWrongSFX);
            Instantiate(deliverWrongVFX, transform.position, quaternion.identity);

            }
        }
    }
}
