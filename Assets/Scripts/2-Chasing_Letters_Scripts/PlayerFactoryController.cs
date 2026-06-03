using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class PlayerFactoryController : MonoBehaviour
{
    public bool isCarrying = false;
    public string interactionZone = "";
    public GameObject interactionObject;
    public GameObject carriedLetter;
    public TMP_Text carriedLetterText;
    public ChasingLettersGameManager gameManager;
    public SubmitZoneManager submitZoneManager;
    public HintManager hintManager;

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

        InteractWithGameObject();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != null)
        {
            if (other.CompareTag("LetterBoxInteractionZone")) 
            {
                interactionZone = "LetterBoxInteractionZone";

                if (other.transform.parent != null)
                {
                    interactionObject = other.transform.parent.gameObject;
                }
            }
            else if (other.CompareTag("TableBenchInteractionZone"))
            {
                interactionZone = "TableBenchInteractionZone";

                if (other.transform.parent != null)
                {
                    interactionObject = other.transform.parent.gameObject;
                }
            }
            else if (other.CompareTag("DeliverZone")) // TODO
            {
                interactionZone = "DeliverZone";

                if(other.transform.parent != null)
                {
                    interactionObject = other.transform.parent.gameObject;
                }
            }
            else if (other.CompareTag("ShowWord"))  // TODO
            {
                interactionZone = "ShowWord";
            }
            else if (other.CompareTag("SubmitZone"))
            {
                interactionZone = "SubmitZone";
            }
            else if (other.CompareTag("TrashZone"))
            {
                interactionZone = "TrashZone";
            }
            else if (other.CompareTag("HintButtonZone"))
            {
                interactionZone = "HintButtonZone";
            }
        }
    }

    private void InteractWithGameObject()
    {
        if (Input.GetKeyDown(KeyCode.Space) && interactionZone != "" && gameManager.currentGameState == GameStateCL.Playing)
        {
            // Carry from treadmill
            if (interactionZone == "LetterBoxInteractionZone" && interactionObject != null && !isCarrying) 
            {
                carryLetterBox();
            }
            // Table interactions (carry or drop) 
            else if ((interactionZone == "TableBenchInteractionZone" || interactionZone == "DeliverZone") && interactionObject != null)
            {
                GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;

                if (isCarrying && !tableLetter.activeInHierarchy) // Is carrying and doesn't have a box
                {
                    dropLetterBox();
                }
                else if (!isCarrying && tableLetter.activeInHierarchy) // Is not carrying and has a box
                {
                    carryLetterBox();
                }
            }
            else if(interactionZone == "TrashZone")
            {
                if (carriedLetter != null)
                {
                    carriedLetter.SetActive(false);
                }
                
                isCarrying = false;
                
                if (animator != null)
                {
                    animator.SetBool("IsCarrying", false);
                }
                
                if (carriedLetterText != null)
                {
                    carriedLetterText.text = "";
                }
            }
            else if(interactionZone == "HintButtonZone")
            {
                if (hintManager != null)
                {
                    hintManager.ShowHint();
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            if (other.CompareTag("LetterBoxInteractionZone") || other.CompareTag("TableBenchInteractionZone") || other.CompareTag("DeliverZone"))
            {
                // Só zera a variável se estiver saindo do objeto atual
                if (other.transform.parent != null && interactionObject == other.transform.parent.gameObject)
                {
                    interactionZone = "";
                    interactionObject = null;
                }
            }
            else if ( other.CompareTag("ShowWord") || other.CompareTag("SubmitZone") || other.CompareTag("TrashZone") || other.CompareTag("HintButtonZone"))
            {
                interactionZone = "";
            }
        }
    }

    public void carryLetterBox()
    {
        if (interactionObject != null && carriedLetter != null && carriedLetterText != null)
        {
            GameObject targetBox = null;

            // Define quem é a caixa a ser pega (a própria ou a filha da mesa)
            if (interactionZone == "LetterBoxInteractionZone")
            {
                targetBox = interactionObject;
            }
            else if (interactionZone == "TableBenchInteractionZone" || interactionZone == "DeliverZone")
            {
                targetBox = interactionObject.transform.GetChild(0).gameObject;
            }

            if (targetBox != null)
            {
                TMP_Text targetBoxText = targetBox.GetComponentInChildren<TMP_Text>();
                
                if (targetBoxText != null)
                {
                    carriedLetterText.text = targetBoxText.text;
                }

                carriedLetter.SetActive(true);
                targetBox.SetActive(false);
                
                isCarrying = true;
                animator.SetBool("IsCarrying", true);
            }
        }
    }

    public void dropLetterBox()
    {
        if (interactionObject != null)
        {
            GameObject interactionLetter = interactionObject.transform.GetChild(0).gameObject;
            
            if (interactionLetter != null)
            {
                TMP_Text interactionLetterText = interactionLetter.GetComponentInChildren<TMP_Text>();

                if (interactionLetterText != null)
                {
                    interactionLetterText.text = carriedLetterText.text;
                }

                interactionLetter.SetActive(true);
                carriedLetter.SetActive(false);
                
                isCarrying = false;
                animator.SetBool("IsCarrying", false);
            }
        }
    }
}