using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseWordDisplay : MonoBehaviour
{
    [SerializeField] private Image currentWordImageUI;
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Pasta dentro de Resources. Ex.: WordData ou WordData/Animais")]
    [SerializeField] private string wordFolder = "WordData";

    void OnEnable()
    {
        if (WordSelectionManager.Instance != null)
        {
            WordSelectionManager.Instance.onWordSelected += HandleWordSelected;
        }

        if (HintManager.Instance != null)
        {
            HintManager.Instance.onHintVisibilityChanged += HandleHintVisibilityChanged;
        }

        if (ChasingLettersApiManager.Instance != null)
        {
            ChasingLettersApiManager.Instance.onWordImageReceived += HandleApiImageReceived;
        }
    }

    

    void Start()
    {
        WordSelectionManager.Instance?.LoadWordSet(wordFolder);
    }

    void OnDisable()
    {
        if (WordSelectionManager.Instance != null)
        {
            WordSelectionManager.Instance.onWordSelected -= HandleWordSelected;
        }

        if (HintManager.Instance != null)
        {
            HintManager.Instance.onHintVisibilityChanged -= HandleHintVisibilityChanged;
        }

        if (ChasingLettersApiManager.Instance != null)
        {
            ChasingLettersApiManager.Instance.onWordImageReceived -= HandleApiImageReceived;
        }
    }

    private void HandleWordSelected(WordData data)
    {
        if (data == null) return;

        if (currentWordImageUI != null && data.wordImage != null)
        {
            currentWordImageUI.sprite = data.wordImage;
        }

        if (hintText != null)
        {
            hintText.text = data.targetWord;
        }
    }

    private void HandleHintVisibilityChanged(bool visible)
    {
        if (hintText != null)
        {
            hintText.gameObject.SetActive(visible);
        }
    }

    private void HandleApiImageReceived(Sprite sprite)
    {
        if (currentWordImageUI != null && sprite != null)
        {
            currentWordImageUI.sprite = sprite;
            currentWordImageUI.preserveAspect = true;
        }
    }
}