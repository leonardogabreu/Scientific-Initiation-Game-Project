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
    [Tooltip("Opcionais: se vazios, são resolvidos na cena. Usados para registrar a trajetória.")]
    public DataCollectionManager dataCollectionManager;
    public DeliverTablesManager deliverTablesManager;

    [Header("Animator")]
    private Animator animator;
    private NavMeshAgent agent;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (dataCollectionManager == null)
        {
            dataCollectionManager = FindAnyObjectByType<DataCollectionManager>();
        }

        if (deliverTablesManager == null)
        {
            deliverTablesManager = FindAnyObjectByType<DeliverTablesManager>();
        }
    }

    void Update()
    {
        if (animator != null && agent != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }

        if (Input.GetKeyDown(KeyCode.Space) && gameCycleManager != null && gameCycleManager.currentGameState == GameStateCL.Playing)
        {
            InteractWithClosest();
        }
    }

    private void InteractWithClosest()
    {
        // Scans around the player
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, interactionLayer);
        
        InteractableCL closestInteractable = GetClosestInteractible(hitColliders);

        // if found an interactible object, interacts with it
        if (closestInteractable != null)
        {
            ExecuteInteraction(closestInteractable);
        }
    }

    private InteractableCL GetClosestInteractible(Collider[] colliders)
    {
        InteractableCL closestInteractable = null;
        float closestDistance = Mathf.Infinity;

        // Searches nearest neighbor
        foreach (Collider hitCollider in colliders)
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
        return closestInteractable;
    }

    private void ExecuteInteraction(InteractableCL interactable)
    {
        GameObject interactionObject = interactable.gameObject;

        switch (interactable.type)
        {
            case InteractableType.LetterBox:
                if (!isCarrying)
                {
                    string pickedFromBelt = CarryLetterBox(interactionObject, true);
                    dataCollectionManager?.RegisterLetterPicked(pickedFromBelt, "belt", -1);
                }
                break;

            case InteractableType.Table:
            case InteractableType.Deliver:
                if (interactionObject.transform.childCount > 0)
                {
                    GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;
                    int slot = deliverTablesManager != null ? deliverTablesManager.GetSlotIndex(interactionObject) : -1;

                    if (isCarrying && !tableLetter.activeInHierarchy)
                    {
                        string placed = DropLetterBox(interactionObject);
                        dataCollectionManager?.RegisterLetterPlaced(placed, slot);
                    }
                    else if (!isCarrying && tableLetter.activeInHierarchy)
                    {
                        string pickedFromTable = CarryLetterBox(interactionObject, false);
                        dataCollectionManager?.RegisterLetterPicked(pickedFromTable, "table", slot);
                    }
                }
                break;

            case InteractableType.Trash:
                if (isCarrying && carriedLetterText != null)
                {
                    dataCollectionManager?.RegisterLetterDiscarded(carriedLetterText.text);
                }

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
                    submitZoneManager.EvaluateWord();
                }
                break;
        }
    }

    /// <summary>Pega a letra e devolve qual foi, ou null se não havia o que pegar.</summary>
    public string CarryLetterBox(GameObject targetObj, bool isFromSpawner)
    {
        if(carriedLetter == null || carriedLetterText == null) return null;

        GameObject targetBox = null;

        if (isFromSpawner)
        {
            targetBox = targetObj;
        }
        else if (targetObj.transform.childCount > 0)
        {
            targetBox = targetObj.transform.GetChild(0).gameObject;
        }

        if (targetBox == null) return null;

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

        return carriedLetterText.text;
    }

    /// <summary>Larga a letra na mesa e devolve qual foi, ou null se não deu para largar.</summary>
    public string DropLetterBox(GameObject targetObj)
    {
        if (targetObj.transform.childCount == 0) return null;

        GameObject interactionLetter = targetObj.transform.GetChild(0).gameObject;
        if (interactionLetter == null) return null;

        TMP_Text interactionLetterText = interactionLetter.GetComponentInChildren<TMP_Text>();
        if (interactionLetterText != null)
        {
            interactionLetterText.text = carriedLetterText.text;
        }

        string droppedLetter = carriedLetterText != null ? carriedLetterText.text : null;

        interactionLetter.SetActive(true);
        carriedLetter.SetActive(false);
        isCarrying = false;

        if (animator != null)
        {
            animator.SetBool("IsCarrying", false);
        }

        return droppedLetter;
    }
}