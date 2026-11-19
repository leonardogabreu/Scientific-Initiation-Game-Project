using UnityEngine;
using UnityEngine.Events;

public class InteractionButtonController : MonoBehaviour
{
    [SerializeField] private DoorController[] linkedDoors;
    [SerializeField] private UnityEvent onPressed;

    public void Press()
    {
        onPressed?.Invoke();

        if (linkedDoors == null) return;

        foreach (DoorController door in linkedDoors)
        {
            if (door != null)
            {
                door.OpenDoor();
            }
        }
    }
}