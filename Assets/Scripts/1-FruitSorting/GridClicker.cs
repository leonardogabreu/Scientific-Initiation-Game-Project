using UnityEngine;
using UnityEngine.AI;

public class GridClicker : MonoBehaviour
{
    public float gridSize = 1f;
    public Transform cursorVisual; 
    public NavMeshAgent agenteDoRobo; 
    public GameManager gameManager;
    public LayerMask floorMask;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && gameManager.currentGameState == gameState.Playing)
        {
            DetectarCliqueEMover();
        }
    }

    void DetectarCliqueEMover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, floorMask)) // Layermask for the floor
        {
            // Finds exact grid square
            int gridX = Mathf.RoundToInt(hit.point.x / gridSize);
            int gridZ = Mathf.RoundToInt(hit.point.z / gridSize);
            Vector3 destinoNoGrid = new Vector3(gridX * gridSize, 0f, gridZ * gridSize);

            // Moves visual cursor
            if (cursorVisual != null)
            {
                cursorVisual.position = new Vector3(destinoNoGrid.x, 0.1f, destinoNoGrid.z);
            }

            // Robot heads towards cursor
            if (agenteDoRobo != null)
            {
                agenteDoRobo.SetDestination(destinoNoGrid);
            }
        }
    }
}