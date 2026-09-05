using UnityEngine;

public class InteractionButtonController : MonoBehaviour
{
    [SerializeField] private DoorController[] linkedDoors;

    public void Press()
    {
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