using UnityEngine;

public class LetterStatus : MonoBehaviour
{
    public bool isUseful {get; set;} = false;
    private bool wasRegistered = false;
    public void NotifyLetterDisabled(bool wasPicked) // Se foi pega/trocada, ou se o incinerador que desativou ela
    {
        if (isUseful && !wasRegistered)
        {
#if UNITY_EDITOR
            Debug.Log("[DDA] " + GetComponent<InteractableCL>().LetterText.text + " registrada: " + wasPicked);
#endif
            DifficultyManager.Instance.RegisterCollectedData(MetricType.ReachableLetterCaught, wasPicked);
            DataCollectionManager.Instance.CountSavedOrBurnedLetters(wasPicked);
            isUseful = false;
            wasRegistered = true;
        }
        else if (isUseful && wasRegistered)
        {
#if UNITY_EDITOR
            Debug.Log("[DDA] " + GetComponent<InteractableCL>().LetterText.text + " ignorada: já registrada");
#endif
        }

    }

    void OnEnable()
    {
        isUseful = false;
        wasRegistered = false;
    }
}
