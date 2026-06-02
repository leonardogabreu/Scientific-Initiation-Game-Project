using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GameStateCL { Start, Playing, Over }

public class ChasingLettersGameManager : MonoBehaviour
{
    public GameStateCL currentGameState;
    [Header("Letter Boxes Pool")]
    public GameObject letterBoxPrefab;
    public List<GameObject> letterBoxesInstances = new List<GameObject>();
    private int numberOfInstances = 15; 
    [Header("Word Database")]
    public string targetWord = "";
    public WordData[] levelWords;
    public Image currentWordImageUI;
    [SerializeField] private DeliverTablesManager deliverTablesManager;
    
    [Header("Hint Manager")]

    [SerializeField] private TMP_Text hintText;
    public float hintTimer = 30.0f; // Fazer lógica depois
    void Start()
    {
        // Populate the object pool at the start of the game
        for(int i = 0; i < numberOfInstances; i++)
        {
            GameObject newBox = Instantiate(letterBoxPrefab, transform.position, Quaternion.identity);
            newBox.SetActive(false);
            letterBoxesInstances.Add(newBox);
        }

        levelWords = Resources.LoadAll<WordData>("WordData");

        deliverTablesManager.resetAllTables();

        if (levelWords != null && levelWords.Length > 0)
        {
            SelectNewWord();
        }
    }
    
    public GameObject GetLetterBoxInstance()
    {
        // Search the pool for the first available (inactive) letter box
        for (int i = 0; i < letterBoxesInstances.Count; i++)
        {
            if (!letterBoxesInstances[i].activeInHierarchy)
            {
                return letterBoxesInstances[i];
            }
        }
        return null;
    }

    public void SelectNewWord()
    {
        if (levelWords != null && levelWords.Length > 0)
        {
            int randomIndex = Random.Range(0, levelWords.Length);
            WordData selectedData = levelWords[randomIndex];

            if (selectedData != null)
            {
                targetWord = selectedData.targetWord;

                if (currentWordImageUI != null && selectedData.wordImage != null)
                {
                    currentWordImageUI.sprite = selectedData.wordImage;
                }
                if (hintText != null)
                {
                    hintText.text = selectedData.targetWord;
                }
            }
        }
    }
}