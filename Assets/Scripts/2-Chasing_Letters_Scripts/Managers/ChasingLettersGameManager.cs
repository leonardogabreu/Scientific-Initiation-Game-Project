using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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

    [Header("Challenge Source")]
    [SerializeField] private ChallengeSource challengeSource;

    public string GameMode => challengeSource != null ? challengeSource.GameMode : "";

    public string LetterPool => challengeSource != null ? challengeSource.LetterPool : Alphabet;

    public int GetCorrectLetterChance(int profileChance) => challengeSource != null ? challengeSource.GetCorrectLetterChance(profileChance) : profileChance;

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

        // Populate the object pool at the start of the game
        for(int i = 0; i < numberOfInstances; i++)
        {
            GameObject newBox = Instantiate(letterBoxPrefab, transform.position, Quaternion.identity);
            newBox.SetActive(false);
            letterBoxesInstances.Add(newBox);
        }

        if (challengeSource == null)
        {
            challengeSource = GetComponent<ChallengeSource>();
        }
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

        if (challengeSource == null)
        {
            Debug.LogError("[ChasingLettersGameManager] Nenhuma ChallengeSource configurada.");
            FinishSelection(onReady);
            return;
        }

        challengeSource.RequestChallenge(IsPlayableWord, (word, challengeId) =>
        {
            ApplyChallenge(word, challengeId);
            FinishSelection(onReady);
        });
    }

    private void FinishSelection(Action onReady)
    {
        IsWordReady = !string.IsNullOrEmpty(targetWord);

        onWordChanged?.Invoke();
        onReady?.Invoke();
    }

    private void ApplyChallenge(string word, string challengeId)
    {
        CurrentChallengeId = challengeId;

        if (string.IsNullOrEmpty(word)) return;

        targetWord = word;

        if (hintText != null)
        {
            hintText.text = word;
        }
    }
}
