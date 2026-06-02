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

        if (Input.GetKeyDown(KeyCode.Space) && interactionZone != "" && gameManager.currentGameState == GameStateCL.Playing)
        {
            // Pegar da Esteira
            if (interactionZone == "LetterBoxInteractionZone" && interactionObject != null && !isCarrying) 
            {
                carryLetterBox();
            }
            // Interações com a Mesa (Pegar ou Soltar)  
            else if ((interactionZone == "TableBenchInteractionZone" || interactionZone == "DeliverZone") && interactionObject != null)
            {
                GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;

                if (isCarrying && !tableLetter.activeInHierarchy)
                {
                    // Se o robô tem uma letra e a mesa está vazia, solta
                    dropLetterBox();
                }
                else if (!isCarrying && tableLetter.activeInHierarchy)
                {
                    // Se o robô está de mãos vazias e a mesa tem uma letra, pega
                    carryLetterBox();
                }
            }
            else if(interactionZone == "SubmitZone")
            {
                if (submitZoneManager != null)
                {
                    submitZoneManager.evaluateWord();
                }
            }
        }
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
            else if (other.CompareTag("SubmitZone"))    // TODO - já dá pra fazer...
            {
                interactionZone = "SubmitZone";
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
            else if ( other.CompareTag("ShowWord") || other.CompareTag("SubmitZone"))
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