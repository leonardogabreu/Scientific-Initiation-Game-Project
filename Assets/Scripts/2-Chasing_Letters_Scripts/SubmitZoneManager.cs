using UnityEngine;

public class SubmitZoneManager : MonoBehaviour
{
    [SerializeField] private DeliverTablesManager deliverTablesManager;
    [SerializeField] private ChasingLettersGameManager gameManager;

    public void evaluateWord()
    {
        if(deliverTablesManager != null)
        {
            if (deliverTablesManager.checkSubmit())
            {
                // VFX e SFX positivos
                if (deliverTablesManager.checkSubmit())
                {
                    deliverTablesManager.resetAllTables();
                    gameManager.SelectNewWord();
                    deliverTablesManager.spawnTables();
                }
                else
                {
                    // VFX e SFX negativos
                }
            }
        }
    }
}
