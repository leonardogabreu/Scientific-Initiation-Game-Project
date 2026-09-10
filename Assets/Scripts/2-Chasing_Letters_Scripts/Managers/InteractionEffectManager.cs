using UnityEngine;

public class InteractionEffectManager : MonoBehaviour
{
    [SerializeField] private InteractableCL lastShownInteractible;

    public void InteractionEffect(InteractableCL interactableCL)
    {
        if (interactableCL == lastShownInteractible) return;

        SetActiveInteractibleEffect(lastShownInteractible, false);
        SetActiveInteractibleEffect(interactableCL, true);
    }

    private void SetActiveInteractibleEffect(InteractableCL interactableCL, bool state)
    {
        if (state)
        {
            lastShownInteractible = interactableCL;
        }

        if (interactableCL == null || interactableCL.InteractionPromptText == null) return;

        interactableCL.InteractionPromptText.gameObject.SetActive(state);
    }
}