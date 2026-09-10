using System;
using System.Collections.Generic;
using UnityEngine;

public class WordSelectionManager : MonoBehaviour
{
    public static WordSelectionManager Instance { get; private set; }

    private WordData[] levelWords;
    private List<int> availableWords = new List<int>();

    public string TargetWord { get; private set; } = "";
    public event Action<WordData> onWordSelected;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Carrega o conjunto de palavras de uma pasta em Resources e sorteia a primeira.</summary>
    public void LoadWordSet(string folder)
    {
        levelWords = Resources.LoadAll<WordData>(folder);

        if (levelWords == null || levelWords.Length == 0)
        {
            Debug.LogError($"[WordSelectionManager] Nenhum WordData encontrado em Resources/{folder}.");
            return;
        }

        RefillAvailableWords();
        SelectNewWord();
    }

    public void SelectNewWord()
    {
        if (levelWords == null || levelWords.Length == 0)
        {
            Debug.LogError("[WordSelectionManager] SelectNewWord chamado sem conjunto de palavras carregado.");
            return;
        }

        if (availableWords.Count == 0) RefillAvailableWords();

        int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);
        WordData selectedData = levelWords[availableWords[randomIndex]];
        availableWords.RemoveAt(randomIndex);

        if (selectedData == null) return;

        TargetWord = selectedData.targetWord;
        onWordSelected?.Invoke(selectedData);
    }

    /// <summary>Define a palavra vinda da API (sobrescreve o sorteio local).</summary>
    public void SetTargetWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return;
        TargetWord = word.ToUpper();
    }

    private void RefillAvailableWords()
    {
        if (levelWords == null || levelWords.Length == 0) return;

        availableWords.Clear();
        for (int i = 0; i < levelWords.Length; i++)
        {
            availableWords.Add(i);
        }
    }
}