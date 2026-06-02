using UnityEngine;
using Unity.Mathematics;
using UnityEngine.AI;

public enum robotState { Empty, HoldingApple, HoldingOrange }

public class PlayerController : MonoBehaviour
{
    public string interactionZone = "";
    public robotState currentRobotState = robotState.Empty;
    public GameObject[] fruitPrefabs;
    private const int APPLE = 0;
    private const int ORANGE = 1;
    
    public GameManager gameManager;
    
    [Header("Audio & VFX")]
    public AudioClip deliverCorrectSFX;
    public AudioClip deliverWrongSFX;
    public AudioSource audioSource;
    public GameObject deliverRightVFX;
    public GameObject deliverWrongVFX;
    public GameObject floatingTextPrefab;

    [Header("Animator")]
    private Animator animator;
    private NavMeshAgent agent;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (animator != null && agent != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }
        
        if (Input.GetKeyDown(KeyCode.Space) && interactionZone != "" && gameManager.currentGameState == gameState.Playing) 
        {
            if(currentRobotState == robotState.Empty && interactionZone == "FruitStackZone")
            {
                int fruitIndex = UnityEngine.Random.Range(0, fruitPrefabs.Length);
                fruitPrefabs[fruitIndex].SetActive(true);
                
                if(fruitIndex == APPLE) {
                    currentRobotState = robotState.HoldingApple;
                } else {
                    currentRobotState = robotState.HoldingOrange;
                }
                animator.SetBool("IsCarrying", true);
            } 
            else if (currentRobotState == robotState.HoldingApple && interactionZone == "AppleDeliverZone") 
            {
                deliverFruitRight(APPLE);
            } 
            else if (currentRobotState == robotState.HoldingOrange && interactionZone == "AppleDeliverZone") 
            {
                deliverFruitWrong(ORANGE);
            } 
            else if(currentRobotState == robotState.HoldingOrange && interactionZone == "OrangeDeliverZone") 
            {
                deliverFruitRight(ORANGE);
            } 
            else if (currentRobotState == robotState.HoldingApple && interactionZone == "OrangeDeliverZone") 
            {
                deliverFruitWrong(APPLE);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("AppleDeliverZone"))
        {
            interactionZone = "AppleDeliverZone";
        } 
        else if (other.CompareTag("OrangeDeliverZone"))
        {
            interactionZone = "OrangeDeliverZone";
        } 
        else if (other.CompareTag("FruitStackZone"))
        {
            interactionZone = "FruitStackZone";
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("AppleDeliverZone") || other.CompareTag("OrangeDeliverZone") || other.CompareTag("FruitStackZone"))
        {
            interactionZone = "";
        }
    }

    private void deliverFruitRight(int fruitType)
    {
        currentRobotState = robotState.Empty;
        fruitPrefabs[fruitType].SetActive(false);
        animator.SetBool("IsCarrying", false);

        int points = gameManager.RegisterCorrectDelivery();

        if(gameManager.currentCombo <= 5) {
            audioSource.pitch = 1.0f + (gameManager.currentCombo * 0.1f);
        }
        audioSource.PlayOneShot(deliverCorrectSFX);
        Instantiate(deliverRightVFX, transform.position + Vector3.up, Quaternion.identity);

        // Floating text
        GameObject floatText = Instantiate(floatingTextPrefab, transform.position + (Vector3.up * 1.5f), Quaternion.Euler(0,45,0));
        floatText.GetComponent<FloatingText>().Setup(points);
    }

    private void deliverFruitWrong(int fruitType)
    {
        currentRobotState = robotState.Empty;
        fruitPrefabs[fruitType].SetActive(false);
        animator.SetBool("IsCarrying", false);
        
        int points = gameManager.RegisterWrongDelivery();
        
        audioSource.pitch = 1.0f;
        audioSource.PlayOneShot(deliverWrongSFX);
        Instantiate(deliverWrongVFX, transform.position, quaternion.identity);

        GameObject floatText = Instantiate(floatingTextPrefab, transform.position + (Vector3.up * 1.5f), Quaternion.Euler(0,45,0));
        floatText.GetComponent<FloatingText>().Setup(points);
    }
}