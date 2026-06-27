using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChasingLettersGameManager : MonoBehaviour
{
    [Header("Letter Boxes Pool")]
    public GameObject letterBoxPrefab;
    public List<GameObject> letterBoxesInstances = new List<GameObject>();
    private int numberOfInstances = 15; 

    [Header("Word Database")]
    public string targetWord = "";
    public WordData[] levelWords;
    private List<int> availableWords = new List<int>();    // List of words that have not been selected yet
    public Image currentWordImageUI;
    
    [Header("Hint Manager")]

    [SerializeField] private TMP_Text hintText;
    
    void Awake()
    {
        // Populate the object pool at the start of the game
        for(int i = 0; i < numberOfInstances; i++)
        {
            GameObject newBox = Instantiate(letterBoxPrefab, transform.position, Quaternion.identity);
            newBox.SetActive(false);
            letterBoxesInstances.Add(newBox);
        }

        levelWords = Resources.LoadAll<WordData>("WordData");

        refillAvailableWords();


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
            if (letterBoxesInstances[i] != null && !letterBoxesInstances[i].activeInHierarchy)
            {
                return letterBoxesInstances[i];
            }
        }
        return null;
    }

    public void SelectNewWord()
    {
        if(availableWords.Count == 0) refillAvailableWords();
        
        if (levelWords != null && availableWords.Count > 0)
        {
        int randomIndex = Random.Range(0, availableWords.Count);            // Avoids repetition of words
        WordData selectedData = levelWords[availableWords[randomIndex]];
        
        availableWords.RemoveAt(randomIndex);   // Removes the word selected form the available words list

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

    public void refillAvailableWords()
    {
        if(levelWords != null && levelWords.Length != 0)
        {
            availableWords.Clear();
            for(int i=0; i<levelWords.Length; i++)
            {
                availableWords.Add(i);
            }
        }
    }
}