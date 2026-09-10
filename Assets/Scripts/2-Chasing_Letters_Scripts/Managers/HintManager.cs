using System;
using System.Collections;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [Header("Hint Manager")]
    [SerializeField] private float hintCooldown = 20.0f;
    [SerializeField] private float hintShowTime = 10.0f;

    private bool canGiveHint = true;

    public event Action<bool> onHintVisibilityChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowHint()
    {
        if (!canGiveHint) return;
        StartCoroutine(HintRoutine());
    }

    private IEnumerator HintRoutine()
    {
        canGiveHint = false;
        onHintVisibilityChanged?.Invoke(true);

        yield return new WaitForSeconds(hintShowTime);

        onHintVisibilityChanged?.Invoke(false);

        float remainingCooldown = hintCooldown - hintShowTime;
        if (remainingCooldown > 0)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        canGiveHint = true;
    }

    void OnEnable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged -= HandleStateChange;
        }
    }

    private void HandleStateChange(GameStateCL newState)
    {
        // Interrupts any ongoing hint and resets the state, for any new state.
        StopAllCoroutines();
        canGiveHint = true;
        onHintVisibilityChanged?.Invoke(false);
    }
}