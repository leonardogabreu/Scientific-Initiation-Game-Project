using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private float closeTime = 10f;
    [SerializeField] private float targetAngle = 90f;
    [SerializeField] private float rotationSpeed = 2f;
    [SerializeField] private bool isLeftDoor;

    private int directionFixingConst;
    private bool isRotating = false;

    void Awake()
    {
        directionFixingConst = isLeftDoor ? -1 : 1;
    }

    public void OpenDoor()
    {
        if (isRotating) return;
        StartCoroutine(OpenRoutine());
    }

    public void CloseDoor()
    {
        if (isRotating) return;
        StartCoroutine(RotateDoor(-1f));
    }

    private IEnumerator OpenRoutine()
    {
        yield return StartCoroutine(RotateDoor(1f));
        yield return new WaitForSeconds(closeTime);
        CloseDoor();
    }

    private IEnumerator RotateDoor(float direction)
    {
        isRotating = true;
        float rotatedSoFar = 0f;

        while (rotatedSoFar < targetAngle)
        {
            transform.Rotate(0, rotationSpeed * direction * directionFixingConst, 0);
            rotatedSoFar += rotationSpeed;
            yield return null;
        }

        isRotating = false;
    }
}