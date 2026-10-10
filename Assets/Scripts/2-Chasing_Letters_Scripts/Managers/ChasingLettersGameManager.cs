using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChasingLettersGameManager : MonoBehaviour
{
    public static ChasingLettersGameManager Instance { get; private set; }

    /// <summary>
    /// Letras que a esteira sabe produzir. Palavras da plataforma com qualquer caractere fora
    /// desta lista são descartadas, senão a rodada fica impossível de completar.
    /// </summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZÇÁÉÍÓÚÂÊÔÃÕ";

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

    /// <summary>Disparado quando targetWord passa a valer — mesas e esteira reagem a ele.</summary>
    public event Action onWordChanged;

    /// <summary>false enquanto a palavra da rodada está sendo buscada na plataforma.</summary>
    public bool IsWordReady { get; private set; }

    /// <summary>Desafio da plataforma da rodada atual, ou null quando a palavra veio do banco local.</summary>
    public string CurrentChallengeId { get; private set; }

    private DeliverTablesManager tables;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        hintText?.gameObject.SetActive(false);

        if (DifficultyManager.Instance != null)
        {
            numberOfInstances = DifficultyManager.Instance.CurrentProfile.letterPoolSize;
        }

        // Populate the object pool at the start of the game
        for(int i = 0; i < numberOfInstances; i++)
        {
            GameObject newBox = Instantiate(letterBoxPrefab, transform.position, Quaternion.identity);
            newBox.SetActive(false);
            letterBoxesInstances.Add(newBox);
        }

        levelWords = Resources.LoadAll<WordData>("WordData");

        refillAvailableWords();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        SelectNewWord();
    }

    // Searches the pool for the first available (inactive) letter box. Else returns null
    public GameObject GetLetterBoxInstance()
    {
        return letterBoxesInstances.FirstOrDefault(box => box != null && !box.activeInHierarchy);
    }

    public void RegisterTables(DeliverTablesManager deliverTablesManager)
    {
        tables = deliverTablesManager;
    }

    public void SetHintVisible(bool visible)
    {
        if (hintText != null) hintText.gameObject.SetActive(visible);
    }

    public bool IsPlayableWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        return tables == null || tables.CanFitWord(word.Length);
    }

    public void SelectNewWord(Action onReady = null)
    {
        IsWordReady = false;

        ChasingLettersApiManager provider = ChasingLettersApiManager.Instance;

        if (provider == null)
        {
            ApplyLocalWord();
            FinishSelection(onReady);
            return;
        }

        WordDifficulty? targetDifficulty = DifficultyManager.Instance != null ? DifficultyManager.Instance.CurrentProfile.wordDifficulty : null;

        provider.RequestWord(IsPlayableWord, targetDifficulty, challenge =>
        {
            if (challenge != null)
            {
                ApplyChallenge(challenge);
            }
            else
            {
                ApplyLocalWord();
            }

            FinishSelection(onReady);
        });
    }

    private void FinishSelection(Action onReady)
    {
        IsWordReady = !string.IsNullOrEmpty(targetWord);

        onWordChanged?.Invoke();
        onReady?.Invoke();
    }

    private void ApplyChallenge(WordChallenge challenge)
    {
        CurrentChallengeId = challenge.challengeId;
        targetWord = challenge.word;

        if (currentWordImageUI != null && challenge.image != null)
        {
            currentWordImageUI.sprite = challenge.image;
            currentWordImageUI.preserveAspect = true;
        }

        if (hintText != null)
        {
            hintText.text = challenge.word;
        }
    }

    private void ApplyLocalWord()
    {
        CurrentChallengeId = null;

        if (levelWords == null || levelWords.Length == 0) return;

        if (availableWords.Count == 0) refillAvailableWords();

        WordDifficulty? targetDifficulty = DifficultyManager.Instance != null ? DifficultyManager.Instance.CurrentProfile.wordDifficulty: null;

        WordData selectedData = SelectAndRemoveWord(targetDifficulty) ?? SelectAndRemoveWord(null);

        if (selectedData == null)
        {
            Debug.LogError("[ChasingLettersGameManager] Nenhuma palavra local cabe nas mesas disponíveis.");
            return;
        }

        targetWord = selectedData.targetWord.ToUpperInvariant();

        if (currentWordImageUI != null && selectedData.wordImage != null)
        {
            currentWordImageUI.sprite = selectedData.wordImage;
        }

        if (hintText != null)
        {
            hintText.text = targetWord;
        }
    }

    // Percorre as palavras disponíveis em ordem aleatória. Palavras que não cabem nas mesas são descartadas
    private WordData SelectAndRemoveWord(WordDifficulty? targetDifficulty)
    {
        List<int> skipped = new List<int>();

        while (availableWords.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);
            int wordIndex = availableWords[randomIndex];
            WordData candidate = levelWords[wordIndex];

            if (candidate == null || !IsPlayableWord(candidate.targetWord))
            {
                availableWords.RemoveAt(randomIndex);
                continue;
            }

            if (targetDifficulty.HasValue && candidate.difficulty != targetDifficulty.Value)
            {
                availableWords.RemoveAt(randomIndex);
                skipped.Add(wordIndex);
                continue;
            }

            availableWords.RemoveAt(randomIndex);
            availableWords.AddRange(skipped);
            return candidate;
        }

        availableWords.AddRange(skipped);
        return null;
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
