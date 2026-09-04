using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LetterBoxSpawner : MonoBehaviour
{
    [Header("Game Managers")] 
    [SerializeField] private ChasingLettersGameManager gameManager;
    [SerializeField] private GameCycleManager gameCycleManager; 

    [Header("Letter rotation parameters")] 
    [SerializeField] private bool isALeftSpawner;
    private int letterRotationFixingConst;
    private const float LetterHeightOffset = 0.51f;
    private const float LetterDepthOffset = 0.34f;
    private const float LetterSideOffset = 0.1f;

    [Header("Time parameters")] 
    [SerializeField] private float spawnTimer = 1f;
    [SerializeField] private float minSpawnTime = 4.5f;
    [SerializeField] private float maxSpawnTime = 8f;
    private float timeCounter = 0f;

    [Header("Random Letter parameters")] 
    [SerializeField] private int correctLetterChance = 70;
    private List<char> currentWordLettersList = new List<char>();
    private string lastTrackedWord = "";
    private string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZÇÁÉÍÓÚÂÊÔÃÕ";

    void Start()
    {
        if (isALeftSpawner)
        {
            letterRotationFixingConst = -1;
        }
        else
        {
            letterRotationFixingConst = 1;
        }
        FillCurrentWordLettersList();
    }

    void Update()
    {
        if (gameCycleManager == null) return;

        if (gameCycleManager.currentGameState == GameStateCL.Playing)
        {
            timeCounter += Time.deltaTime;

            if (timeCounter >= spawnTimer && currentWordLettersList != null)
            {
                GameObject letter = gameManager.GetLetterBoxInstance();

                if (letter != null){   
                    letter.transform.localPosition = transform.position;
                    letter.transform.localRotation = Quaternion.Euler(0, -letterRotationFixingConst*90, 0);
                    
                    InteractableCL interactableCL = letter.GetComponent<InteractableCL>();
                    TMP_Text letterText = interactableCL != null ? interactableCL.LetterText : null;

                    if (letterText != null)
                    {
                        letterText.text = GetWeightedRandomLetter();   
                        // A lot of bug correction from the letter position and rotation, with very specific numbers
                        letterText.transform.localRotation = Quaternion.Euler(90, letterRotationFixingConst * 90, 0);
                        letterText.transform.localPosition = new Vector3(-letterRotationFixingConst * LetterSideOffset, LetterHeightOffset, -letterRotationFixingConst * LetterDepthOffset);

                        letter.SetActive(true);
                    }
                     // Corrects the rotation of the interaction prompt
                    if (interactableCL != null && interactableCL.InteractionPromptText != null)
                    {
                        interactableCL.InteractionPromptText.transform.localRotation = Quaternion.Euler(0, letterRotationFixingConst * 90, 0);
                    }
                }
                // Resets timer and randomizes the next spawn delay
                timeCounter = 0f;
                spawnTimer = Random.Range(minSpawnTime, maxSpawnTime);
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
                FillCurrentWordLettersList();
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
        if (newState == GameStateCL.Playing)
        {
            timeCounter = 0f;
            spawnTimer = Random.Range(minSpawnTime, maxSpawnTime);
        }
    }

    private void FillCurrentWordLettersList()
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