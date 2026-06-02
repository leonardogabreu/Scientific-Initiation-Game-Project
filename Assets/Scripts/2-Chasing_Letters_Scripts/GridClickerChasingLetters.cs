using UnityEngine;
using UnityEngine.AI;

public class ChasingLettersGridClicker : MonoBehaviour
{
    public float gridSize = 1f;
    public Transform cursorVisual; 
    public NavMeshAgent robotAgent; 
    public ChasingLettersGameManager gameManager;
    public LayerMask floorMask;

    void Update()
    {
       if (Input.GetMouseButtonDown(0) && gameManager.currentGameState == GameStateCL.Playing)
        {
            DetectClickAndMove();
        }
    }

    void DetectClickAndMove()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, floorMask)) 
        {
            // Finds exact grid square
            int gridX = Mathf.RoundToInt(hit.point.x / gridSize);
            int gridZ = Mathf.RoundToInt(hit.point.z / gridSize);
            Vector3 gridDestination = new Vector3(gridX * gridSize, 0f, gridZ * gridSize);

            // Moves visual cursor
            if (cursorVisual != null)
            {
                cursorVisual.position = new Vector3(gridDestination.x, 0.1f, gridDestination.z);
            }

            // Robot heads towards cursor
            if (robotAgent != null)
            {
                robotAgent.SetDestination(gridDestination);
            }
        }
    }
}