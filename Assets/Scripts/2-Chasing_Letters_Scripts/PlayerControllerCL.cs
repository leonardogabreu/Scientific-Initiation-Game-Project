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
    [SerializeField] private InteractionEffectManager interactionEffectManager;

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
                    CarryLetterBox(interactionObject, true);
                }
                break;

            case InteractableType.Table:
            case InteractableType.Deliver:
                if (interactionObject.transform.childCount > 0)
                {
                    GameObject tableLetter = interactionObject.transform.GetChild(0).gameObject;

                    if (isCarrying && !tableLetter.activeInHierarchy)
                    {
                        DropLetterBox(interactionObject);
                    }
                    else if (!isCarrying && tableLetter.activeInHierarchy)
                    {
                        CarryLetterBox(interactionObject, false);
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
                    submitZoneManager.EvaluateWord();
                }
                break;
        }
    }

    public void CarryLetterBox(GameObject targetObj, bool isFromSpawner)
    {
        if (carriedLetter == null || carriedLetterText == null) return;

        GameObject targetBox = null;

        if (isFromSpawner)
        {
            targetBox = targetObj;
        }
        else if (targetObj.transform.childCount > 0)
        {
            targetBox = targetObj.transform.GetChild(0).gameObject;
        }

        if (targetBox == null) return;

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

    public void DropLetterBox(GameObject targetObj)
    {
        if (targetObj.transform.childCount == 0) return;

        GameObject interactionLetter = targetObj.transform.GetChild(0).gameObject;
        if (interactionLetter == null) return;

        TMP_Text interactionLetterText = interactionLetter.GetComponentInChildren<TMP_Text>();
        if (interactionLetterText != null)
        {
            interactionLetterText.text = carriedLetterText.text;
        }

        interactionLetter.SetActive(true);
        carriedLetter.SetActive(false);
        isCarrying = false;

        if (animator == null) return;
        animator.SetBool("IsCarrying", false);
    }
}