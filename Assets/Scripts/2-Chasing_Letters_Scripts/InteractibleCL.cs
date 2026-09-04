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

    public TMP_Text InteractionPromptText => interactionPromptText;
}