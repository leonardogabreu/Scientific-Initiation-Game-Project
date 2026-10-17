using UnityEngine;

public class LetterStatus : MonoBehaviour
{
    public bool isUsefull {get; set;} = false;
    private bool wasRegistered = false;
    public void NotifyLetterDisabled(bool wasPicked) // Se foi pega/trocada, ou se o incinerador que desativou ela
    {
        if (isUsefull && !wasRegistered)
        {
            Debug.Log("[DDA] " + GetComponent<InteractableCL>().LetterText.text + " registrada: " + wasPicked);
            DifficultyManager.Instance.RegisterCollectedData(MetricType.ReachableLetterCaught, wasPicked);
            isUsefull = false;
            wasRegistered = true;
        }
        else if (isUsefull && wasRegistered)
        {
            Debug.Log("[DDA] " + GetComponent<InteractableCL>().LetterText.text + " ignorada: já registrada");
        }

    }

    void OnEnable()
    {
        isUsefull = false;
        wasRegistered = false;
    }
}
