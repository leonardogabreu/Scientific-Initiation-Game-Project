using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class PlayerControllerCL : MonoBehaviour
{
    public bool isCarrying = false;
    public GameObject carriedLetter;
    public TMP_Text carriedLetterText;

    [Header("Interaction Settings")]
    public float interactionRadius = 1.5f;
    public LayerMask interactionLayer; // Defina a Layer dos objetos interativos aqui

    [Header("Managers")]
    public ChasingLettersGameManager gameManager;
    public SubmitZoneManager submitZoneManager;
    public HintManager hintManager;
    public GameCycleManager gameCycleManager;

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

        if (Input.GetKeyDown(KeyCode.Space) && gameCycleManager != null && gameCycleManager.currentGameState == GameStateCL.Playing)
        {
            interactWithClosest();
        }
    }

    private void interactWithClosest()
    {
        // Scans around the player
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, interactionLayer);
        
        InteractableCL closestInteractable = null;
        float closestDistance = Mathf.Infinity;

        // Searches nearest neighbor
        foreach (Collider hitCollider in hitColliders)
        {
            InteractableCL interactable = hitCollider.GetComponent<InteractableCL>();
            if (interactable != null)
            {
                float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestInteractable = interactable;
                }
            }
        }

        // if found an interactible object, interacts with it
        if (closestInteractable != null)
        {
            executeInteraction(closestInteractable);
        }
    }

    private void executeInteraction(InteractableCL interactable)
    {
        GameObject interactionObject = interactable.gameObject;

        switch (interactable.type)
        {
            case InteractableType.LetterBox:
                if (!isCarrying)
                {
                    carryLetterBox(interactionObject, true);
                }
                break;

            case InteractableType.Table:
            case InteractableType.Deliver:
                if (interactionObject.transform.childCount > 0)
                {
                    GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;

                    if (isCarrying && !tableLetter.activeInHierarchy)
                    {
                        dropLetterBox(interactionObject);
                    }
                    else if (!isCarrying && tableLetter.activeInHierarchy)
                    {
                        carryLetterBox(interactionObject, false);
                    }
                }
                break;

            case InteractableType.Trash:
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
                break;

            case InteractableType.Hint:
                if (hintManager != null)
                {
                    hintManager.ShowHint();
                }
                break;

            case InteractableType.Submit:
                if (submitZoneManager != null)
                {
                    submitZoneManager.evaluateWord();
                }
                break;
        }
    }

    public void carryLetterBox(GameObject targetObj, bool isFromSpawner)
    {
        if (carriedLetter != null && carriedLetterText != null)
        {
            GameObject targetBox = null;

            if (isFromSpawner)
            {
                targetBox = targetObj;
            }
            else if (targetObj.transform.childCount > 0)
            {
                targetBox = targetObj.transform.GetChild(0).gameObject;
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

                if (animator != null)
                {
                    animator.SetBool("IsCarrying", true);
                }
            }
        }
    }

    public void dropLetterBox(GameObject targetObj)
    {
        if (targetObj.transform.childCount > 0)
        {
            GameObject interactionLetter = targetObj.transform.GetChild(0).gameObject;
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

                if (animator != null)
                {
                    animator.SetBool("IsCarrying", false);
                }
            }
        }
    }
} 