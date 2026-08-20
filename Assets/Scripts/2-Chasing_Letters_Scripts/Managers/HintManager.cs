using System.Collections;
using TMPro;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    [Header("Hint Manager")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private float hintCooldown = 20.0f;
    [SerializeField] private float hintShowTime = 10.0f;
    [SerializeField] private GameCycleManager gameCycleManager;

    private bool canGiveHint = true;

    public void ShowHint()
    {
        if (hintText == null || !canGiveHint) return;

        StartCoroutine(HintRoutine());
    }

    private IEnumerator HintRoutine()
    {
        canGiveHint = false;
        hintText.gameObject.SetActive(true);

        yield return new WaitForSeconds(hintShowTime);

        hintText.gameObject.SetActive(false);

        float remainingCooldown = hintCooldown - hintShowTime;
        if (remainingCooldown > 0)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        canGiveHint = true;
    }

    void OnEnable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged -= HandleStateChange;
        }
    }

    private void HandleStateChange(GameStateCL newState)
    {
        // Interrupts any ongoing hint and resets the state, for any new state.
        StopAllCoroutines();
        canGiveHint = true;

        if (hintText != null)
        {
            hintText.gameObject.SetActive(false);
        }
    }
}