using UnityEngine;

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
}