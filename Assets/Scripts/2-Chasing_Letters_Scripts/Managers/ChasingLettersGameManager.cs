using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChasingLettersGameManager : MonoBehaviour
{
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

    void Start()
    {
        // Busca a primeira palavra enquanto o painel inicial está na tela, para o aluno nunca
        // esperar pela rede depois de apertar "jogar".
        SelectNewWord();
    }

    // Searches the pool for the first available (inactive) letter box. Else returns null
    public GameObject GetLetterBoxInstance()
    {
        return letterBoxesInstances.FirstOrDefault(box => box != null && !box.activeInHierarchy);
    }

    /// <summary>
    /// O DeliverTablesManager se registra aqui no Start para que a escolha de palavra já
    /// descarte palavras que não cabem nas mesas disponíveis.
    /// </summary>
    public void RegisterTables(DeliverTablesManager deliverTablesManager)
    {
        tables = deliverTablesManager;
    }

    public bool IsPlayableWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        return tables == null || tables.CanFitWord(word.Length);
    }

    /// <summary>
    /// Pede a próxima palavra à plataforma e cai no banco local se ela não puder fornecer.
    /// Assíncrono: <paramref name="onReady"/> roda quando targetWord já está válido.
    /// </summary>
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

        provider.RequestWord(IsPlayableWord, challenge =>
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

        // Avoids repetition of words
        for (int i = availableWords.Count - 1; i >= 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);
            WordData selectedData = levelWords[availableWords[randomIndex]];

            availableWords.RemoveAt(randomIndex);   // Removes the word selected from the available words list

            if (selectedData == null || !IsPlayableWord(selectedData.targetWord)) continue;

            targetWord = selectedData.targetWord.ToUpperInvariant();

            if (currentWordImageUI != null && selectedData.wordImage != null)
            {
                currentWordImageUI.sprite = selectedData.wordImage;
            }

            if (hintText != null)
            {
                hintText.text = targetWord;
            }

            return;
        }

        Debug.LogError("[ChasingLettersGameManager] Nenhuma palavra local cabe nas mesas disponíveis.");
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
