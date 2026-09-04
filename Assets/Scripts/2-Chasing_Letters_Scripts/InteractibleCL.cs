using UnityEngine;
using TMPro;

public enum InteractableType
{
    LetterBox,
    Table,
    Deliver,
    Trash,
    Hint,
    Submit
}

public class InteractableCL : MonoBehaviour
{
    public InteractableType type;
    [SerializeField] private TMP_Text interactionPromptText;

    [SerializeField] private TMP_Text letterText;

    public TMP_Text InteractionPromptText => interactionPromptText;
    public TMP_Text LetterText => letterText;

    void Awake()
    {
        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false);
        }
    }
}