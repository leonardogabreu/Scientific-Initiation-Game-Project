using TMPro;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    [Header("Hint Manager")]

    [SerializeField] private TMP_Text hintText;
    public static float hintCooldown = 20.0f;
    public float hintTimer = hintCooldown;
    public bool canGiveHint = true;
    public bool isShowingHint = false;
    public static float hintShowTime = 10.0f;
    public float hintShownTime = 0.0f;
    public GameCycleManager gameCycleManager;

    void Update()
    {
        if (gameCycleManager != null)
        {
            if (gameCycleManager.currentGameState == GameStateCL.Playing)
            {
                if (!canGiveHint) // Hint is on cooldown
                {
                    hintTimer -= Time.deltaTime;
                    if (hintTimer <= 0) // if the cooldown is over, can give hint and resets the cooldown
                    {
                        canGiveHint = true;
                        hintTimer = hintCooldown;
                    }
                }

                if (isShowingHint) // Hint is given
                {
                    hintShownTime += Time.deltaTime;
                    if(hintShownTime >= hintShowTime) // if hint show time is over, hides the hint, and resets boolean and timer values
                    {
                        hintText.gameObject.SetActive(false);
                        isShowingHint = false;
                        canGiveHint = true;
                        hintShownTime = 0.0f;
                    }
                }
            }
        }
    }

    public void ShowHint()
    {
        if(hintText != null && canGiveHint)
        {
            hintText.gameObject.SetActive(true);
            isShowingHint = true;
            hintShownTime = 0;
            canGiveHint = false;
        }
    }

    void OnEnable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged += handleStateChange;
        }
    }

    void OnDisable()
    {
        if (gameCycleManager != null)
        {
            gameCycleManager.onGameStateChanged -= handleStateChange;
        }
    }

    private void handleStateChange(GameStateCL newState)
    {
        if (newState == GameStateCL.Playing)
        {
            // Resets everything for the new round
            canGiveHint = true;
            isShowingHint = false;
            hintTimer = hintCooldown;
            hintShownTime = 0.0f;

            if (hintText != null)
            {
                hintText.gameObject.SetActive(false);
            }
        }
        else if (newState == GameStateCL.Start || newState == GameStateCL.Over)
        {
            if (hintText != null)
            {
                hintText.gameObject.SetActive(false);
            }
        }
    }
}
