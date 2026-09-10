using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private NavMeshAgent playerAgent;

    void Start()
    {
        SceneManager.LoadScene("2-Chasing_Letters", LoadSceneMode.Additive);
        playerAgent.enabled = true; // reativa depois que a fase (e o NavMesh) já carregaram
    }
}