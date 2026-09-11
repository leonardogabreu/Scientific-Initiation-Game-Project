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
    public LayerMask interactionLayer;

    [Header("Managers")]
    public ChasingLettersGameManager gameManager;
    public SubmitZoneManager submitZoneManager;
    public HintManager hintManager;
    public GameCycleManager gameCycleManager;
    [SerializeField] private InteractionEffectManager interactionEffectManager;
    [Tooltip("Opcionais: se vazios, são resolvidos na cena. Usados para registrar a trajetória.")]
    public DataCollectionManager dataCollectionManager;
    public DeliverTablesManager deliverTablesManager;

    [Header("Interaction Prompt")]
    [SerializeField] private float promptCheckInterval = 0.1f;
    private float promptCheckTimer = 0f;

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

        if (interactionEffectManager == null) return;

        // Timer limiting the ammount of times it seeks for new interactibles, for better performance.
        promptCheckTimer += Time.deltaTime;
        if (promptCheckTimer >= promptCheckInterval)
        {
            promptCheckTimer = 0f;
            SeekInteractibles();
        }
    }

    private void SeekInteractibles()
    {
        InteractableCL interactible = GetClosestInteractible();
        interactionEffectManager.InteractionEffect(interactible);
    }

    private void InteractWithClosest()
    {
        InteractableCL closestInteractable = GetClosestInteractible();

        // if found an interactible object, interacts with it
        if (closestInteractable != null)
        {
            ExecuteInteraction(closestInteractable);
        }
    }

    private InteractableCL GetClosestInteractible()
    {
        // Scans around the player
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRadius, interactionLayer);

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
                else
                {
                    string handedToBelt = carriedLetterText != null ? carriedLetterText.text : null;
                    string takenFromBelt = SwapLetterBox(interactionObject);
                    dataCollectionManager?.RegisterLetterSwapped(takenFromBelt, handedToBelt, "belt", -1);
                }
                break;

            case InteractableType.Table:
            case InteractableType.Deliver:
                if (interactionObject.transform.childCount > 0)
                {
                    GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;
                    int slot = deliverTablesManager != null ? deliverTablesManager.GetSlotIndex(interactionObject) : -1;

                    if (isCarrying && tableLetter.activeInHierarchy)
                    {
                        string handedToTable = carriedLetterText != null ? carriedLetterText.text : null;
                        string takenFromTable = SwapLetterBox(tableLetter);
                        dataCollectionManager?.RegisterLetterSwapped(takenFromTable, handedToTable, "table", slot);
                    }
                    else if (isCarrying && !tableLetter.activeInHierarchy)
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
                
            case InteractableType.InteractionButton:
                InteractionButtonController button = interactionObject.GetComponent<InteractionButtonController>();

                button?.Press();
                break;
        }
    }

    /// <summary>Pega a letra e devolve qual foi, ou null se não havia o que pegar.</summary>
    public string CarryLetterBox(GameObject targetObj, bool isFromSpawner)
    {
        if (carriedLetter == null || carriedLetterText == null) return null;

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

        InteractableCL targetInteractable = targetBox.GetComponent<InteractableCL>();
        TMP_Text targetBoxText = targetInteractable != null ? targetInteractable.LetterText : null;

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

    /// <summary>Troca a letra carregada pela do alvo e devolve a que o aluno passou a carregar,
    /// ou null se não deu para trocar.</summary>
    public string SwapLetterBox(GameObject targetObj)
    {
        if (carriedLetter == null || carriedLetterText == null) return null;

        InteractableCL targetInteractable = targetObj.GetComponent<InteractableCL>();
        TMP_Text targetBoxText = targetInteractable != null ? targetInteractable.LetterText : null;
        if (targetBoxText == null) return null;

        string previousCarriedLetter = carriedLetterText.text;
        carriedLetterText.text = targetBoxText.text;
        targetBoxText.text = previousCarriedLetter;

        return carriedLetterText.text;
    }

    /// <summary>Larga a letra na mesa e devolve qual foi, ou null se não deu para largar.</summary>
    public string DropLetterBox(GameObject targetObj)
    {
        if (targetObj.transform.childCount == 0) return null;

        GameObject interactionLetter = targetObj.transform.GetChild(0).gameObject;
        if (interactionLetter == null) return null;

        InteractableCL interactableCL = interactionLetter.GetComponent<InteractableCL>();
        TMP_Text interactionLetterText = interactableCL != null ? interactableCL.LetterText : null;

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