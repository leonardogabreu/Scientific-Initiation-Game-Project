using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private NavMeshAgent playerAgent;

    void Start()
    {
        // Por padrão, o player deve começar com o NavMeshAgent desligado no inspector
        SceneManager.LoadScene("2-Chasing_Letters", LoadSceneMode.Additive);
        playerAgent.enabled = true; // reativa depois que a fase (e o NavMesh) já carregaram
    }
}