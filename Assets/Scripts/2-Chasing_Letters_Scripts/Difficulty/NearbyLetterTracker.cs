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
        List<InteractableCL> interactables = GetNearbyInteractibleLetters();

        if(interactables == null) return;

        foreach(InteractableCL letter in interactables)
        {
            SetLetter(letter);
        }
    }

    // Public pra poder ser usado no PlayerController, para resolver o problema de letras serem pegas antes de serem marcadas como úteis
    // (devido ao intervalo de 0.1s em cada evento de marcar letras próximas).
    public void SetLetter(InteractableCL letter)
    {
        if(letter == null) return;

        if (deliverTablesManager == null) deliverTablesManager = FindAnyObjectByType<DeliverTablesManager>();
        if(pc == null) pc = FindAnyObjectByType<PlayerControllerCL>();
        if (deliverTablesManager == null || pc == null) return;

       // Usado para marcar letras próximas como inúteis quando uma criança está segurando uma letra útil. 
        bool holdingUsefulLetter = pc.isCarrying && deliverTablesManager.IsHeldLetterUsefull(pc.carriedLetterText.text, pc);

        LetterStatus letterStatus = letter.GetComponent<LetterStatus>();

        if(letter.type != InteractableType.LetterBox || letterStatus == null) return;

        letterStatus.isUseful = !holdingUsefulLetter && deliverTablesManager.IsNearbyLetterUsefull(letter, pc);
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
