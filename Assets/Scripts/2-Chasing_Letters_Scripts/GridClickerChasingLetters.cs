using UnityEngine;
using UnityEngine.AI;

public class ChasingLettersGridClicker : MonoBehaviour
{
    public float gridSize = 1f;
    public Transform cursorVisual;
    public NavMeshAgent robotAgent;
    public GameCycleManager gameCycleManager;
    public LayerMask floorMask;

    // Este objeto mora na cena Core, que não recarrega entre o menu e a fase: a preferência
    // é relida a cada início de partida, e não uma vez só no Start.
    private bool useKeyboardControls;

    void Update()
    {
        if (gameCycleManager == null || gameCycleManager.currentGameState != GameStateCL.Playing) return;

        if (useKeyboardControls)
        {
            DetectKeyboardAndMove();
        }
        else if (Input.GetMouseButtonDown(0))
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
            MoveToGridCell(new Vector3(hit.point.x, 0f, hit.point.z));
        }
    }

    // Setas movem o agente continuamente (o grid é pequeno demais para andar casa a casa).
    // Move respeita o NavMesh, então o personagem não sai do chão andável.
    void DetectKeyboardAndMove()
    {
        if (robotAgent == null || !robotAgent.isOnNavMesh) return;

        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) direction += Vector3.forward;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) direction += Vector3.back;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direction += Vector3.left;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direction += Vector3.right;

        if (direction == Vector3.zero) return;

        direction.Normalize();
        robotAgent.ResetPath();
        robotAgent.Move(direction * robotAgent.speed * Time.deltaTime);

        Quaternion target = Quaternion.LookRotation(direction);
        robotAgent.transform.rotation = Quaternion.RotateTowards(
            robotAgent.transform.rotation, target, robotAgent.angularSpeed * Time.deltaTime);
    }

    private void MoveToGridCell(Vector3 worldPosition)
    {
        Vector3 gridDestination = SnapToGrid(worldPosition);

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

    private Vector3 SnapToGrid(Vector3 position)
    {
        int gridX = Mathf.RoundToInt(position.x / gridSize);
        int gridZ = Mathf.RoundToInt(position.z / gridSize);
        return new Vector3(gridX * gridSize, 0f, gridZ * gridSize);
    }

    void OnEnable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged -= HandleStateChange;
        }
    }

    private void HandleStateChange(GameStateCL newState)
    {
        if (newState == GameStateCL.Playing)
        {
            useKeyboardControls = PlayerPrefs.GetInt(SettingsManager.MovementControlPrefKey, 0) == 1;

            if (cursorVisual != null)
            {
                cursorVisual.gameObject.SetActive(!useKeyboardControls);
            }
        }

        if (newState == GameStateCL.Start)
        {
            if (cursorVisual != null && robotAgent != null && robotAgent.isOnNavMesh)
            {
                cursorVisual.position = new Vector3(0, 0.1f, 0);
                robotAgent.ResetPath();
                robotAgent.Warp(new Vector3(0, 0.16f, 0));
            }
        }
    }
}