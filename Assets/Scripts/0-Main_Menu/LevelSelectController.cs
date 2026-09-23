using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectController : MonoBehaviour
{
    [Serializable]
    public class LevelInfo
    {
        public string displayName;
        [Tooltip("Nome da cena da fase, carregada de forma additive pelo GameBootstrap depois que a Core sobe (precisa estar no Build Settings)")]
        public string sceneName;
    }

    private const string UnlockedKeyPrefix = "levelUnlocked_";

    [Header("Fases")]
    public LevelInfo[] levels;

    [Header("UI")]
    [Tooltip("Pai onde os botões das fases serão criados")]
    public Transform buttonContainerParent;
    [Tooltip("Botão (com um TMP_Text filho) usado como modelo; deve ficar desativado na cena")]
    public Button levelButtonTemplate;

    [Header("Desbloqueio")]
    [Tooltip("Enquanto estiver ligado, todas as fases ficam liberadas")]
    [SerializeField] private bool unlockAll = true;

    void Start()
    {
        buildButtons();
    }

    void buildButtons()
    {
        if (levelButtonTemplate == null || buttonContainerParent == null || levels == null) return;

        for (int i = 0; i < levels.Length; i++)
        {
            int index = i;
            Button button = Instantiate(levelButtonTemplate, buttonContainerParent);
            button.gameObject.SetActive(true);
            button.interactable = isUnlocked(index);
            button.onClick.AddListener(() => loadLevel(index));

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = levels[i].displayName;
        }
    }

    public void loadLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length) return;
        if (!isUnlocked(index)) return;

        LevelSelection.TargetScene = levels[index].sceneName;
        SceneManager.LoadScene("Core");
    }

    // A primeira fase é sempre liberada; as demais dependem de unlockLevel()
    // (a ser chamado quando a fase anterior for concluída) ou de unlockAll.
    public bool isUnlocked(int index)
    {
        if (unlockAll || index == 0) return true;
        return PlayerPrefs.GetInt(UnlockedKeyPrefix + index, 0) == 1;
    }

    public static void unlockLevel(int index)
    {
        PlayerPrefs.SetInt(UnlockedKeyPrefix + index, 1);
        PlayerPrefs.Save();
    }
}
