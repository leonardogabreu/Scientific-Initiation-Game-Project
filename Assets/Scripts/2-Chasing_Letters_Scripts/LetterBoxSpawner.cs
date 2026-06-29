using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LetterBoxSpawner : MonoBehaviour
{
    private ChasingLettersGameManager gameManager;
    [SerializeField] private GameCycleManager gameCycleManager; 
    public bool isALeftSpawner;
    private int letterRotationFixingConst;
    private float spawnTimer = 1f;
    private float timeCounter = 0f;
    private int correctLetterChance = 70;
    private List<char> currentWordLettersList = new List<char>();
    private string lastTrackedWord = "";
    private string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZÇÁÉÍÓÚÂÊÔÃÕ";

    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<ChasingLettersGameManager>();
        if (isALeftSpawner)
        {
            letterRotationFixingConst = -1;
        }
        else
        {
            letterRotationFixingConst = 1;
        }
        fillCurrentWordLettersList();
    }

    void Update()
    {
        if (gameCycleManager.currentGameState == GameStateCL.Playing)
        {
            timeCounter += Time.deltaTime;

            if (timeCounter >= spawnTimer && currentWordLettersList != null)
            {
                GameObject letter = gameManager.GetLetterBoxInstance();

                if (letter != null)   
                {
                    letter.transform.localPosition = transform.position;
                    letter.transform.localRotation = Quaternion.Euler(0, -letterRotationFixingConst*90, 0);
                    
                    TMP_Text letterText = letter.GetComponentInChildren<TMP_Text>();
                    if (letterText != null)
                    {
                        letterText.text = GetWeightedRandomLetter();
                        
                        // A lot of bug correction from the letter position and rotation, with very specific numbers
                        letterText.transform.localRotation = Quaternion.Euler(90, letterRotationFixingConst * 90, 0);
                        letterText.transform.localPosition = new Vector3(-letterRotationFixingConst * 0.1f, 0.51f, -letterRotationFixingConst * 0.34f);
                    }

                    letter.SetActive(true);
                }

                // Reset timer and randomize the next spawn delay
                timeCounter = 0f;
                spawnTimer = Random.Range(4.5f, 8);
            }
        }
    }

    private string GetWeightedRandomLetter()
    {
        int roll = Random.Range(0, 100);
        string currentWord = gameManager.targetWord; 

        // If the roll is within the percentage and the word is valid, pick a letter from the target word
        if (roll < correctLetterChance && !string.IsNullOrEmpty(currentWord) && currentWordLettersList != null)
        {
            // If the list is empty or the word has changed (new word)
            if (currentWordLettersList.Count == 0 || currentWord != lastTrackedWord) 
            {
                fillCurrentWordLettersList();
                lastTrackedWord = currentWord;
            }

            int randomIndex = Random.Range(0, currentWordLettersList.Count);
            string letterToSpawn = currentWordLettersList[randomIndex].ToString().ToUpper();
            currentWordLettersList.RemoveAt(randomIndex);

        return letterToSpawn;
        }
        else 
        {
            // Pick a completely random letter from the alphabet array
            int randomIndex = Random.Range(0, alphabet.Length);
            return alphabet[randomIndex].ToString();
        }
    }

    // Observer Pattern
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
            timeCounter = 0f;
            spawnTimer = Random.Range(4.5f, 8);
        }
    }

    private void fillCurrentWordLettersList()
    {
        if (currentWordLettersList != null && gameManager != null && gameManager.targetWord != null)
        {
            currentWordLettersList.Clear();
            for (int i = 0; i < gameManager.targetWord.Length; i++) 
            {
                currentWordLettersList.Add(gameManager.targetWord[i]);
            }
        }
    }
}