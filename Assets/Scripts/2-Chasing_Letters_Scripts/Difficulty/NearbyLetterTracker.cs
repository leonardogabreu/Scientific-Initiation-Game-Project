using System.Collections.Generic;
using UnityEngine;

public class NearbyLetterTracker : MonoBehaviour
{
    private PlayerControllerCL pc;
    private DeliverTablesManager deliverTablesManager;

    void Awake()
    {
        pc = GetComponent<PlayerControllerCL>();
    }

    public void SetNearbyLetters()
    {
        if (deliverTablesManager == null) deliverTablesManager = FindAnyObjectByType<DeliverTablesManager>();
        if (deliverTablesManager == null) return;

        List<InteractableCL> interactables = GetNearbyInteractibleLetters();

        if(interactables == null) return;

        foreach(InteractableCL letter in interactables)
        {
            if(letter == null) continue;

            LetterStatus letterStatus = letter.GetComponent<LetterStatus>();

            if(letter.type != InteractableType.LetterBox || letterStatus == null) continue;

            letterStatus.isUsefull = deliverTablesManager.IsLetterUsefull(letter, pc);
        }
    }

    private List<InteractableCL> GetNearbyInteractibleLetters()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pc.interactionRadius, pc.interactionLayer);
        List<InteractableCL> interactables = new List<InteractableCL>();

        foreach (Collider collider in colliders)
        {
            InteractableCL interactible = collider.gameObject.GetComponent<InteractableCL>();

            if(interactible == null || interactible.type != InteractableType.LetterBox) continue;
            interactables.Add(interactible);
        }
        return interactables;
    }
}
