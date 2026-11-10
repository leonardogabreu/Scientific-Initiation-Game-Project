using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WordImageChallengeSource : ChallengeSource
{
    [Header("Word Database")]
    public WordData[] levelWords;
    private List<int> availableWords = new List<int>();    // List of words that have not been selected yet
    public Image currentWordImageUI;

    void Awake()
    {
        levelWords = Resources.LoadAll<WordData>("WordData");

        refillAvailableWords();
    }

    public override void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady)
    {
        ChasingLettersApiManager provider = ChasingLettersApiManager.Instance;

        if (provider == null)
        {
            onReady(ApplyLocalWord(isPlayable), null);
            return;
        }

        WordDifficulty? targetDifficulty = DifficultyManager.Instance != null && DifficultyManager.Instance.adjustmentSystemType == AdjustmentSystemType.InRealTime?
        DifficultyManager.Instance.CurrentProfile.wordDifficulty : null;

        provider.RequestWord(isPlayable, targetDifficulty, challenge =>
        {
            if (challenge != null)
            {
                ApplyChallenge(challenge);
                onReady(challenge.word, challenge.challengeId);
            }
            else
            {
                onReady(ApplyLocalWord(isPlayable), null);
            }
        });
    }

    private void ApplyChallenge(WordChallenge challenge)
    {
        if (currentWordImageUI != null && challenge.image != null)
        {
            currentWordImageUI.sprite = challenge.image;
            currentWordImageUI.preserveAspect = true;
        }
    }

    private string ApplyLocalWord(Func<string, bool> isPlayable)
    {
        if (levelWords == null || levelWords.Length == 0) return null;

        if (availableWords.Count == 0) refillAvailableWords();

        WordDifficulty? targetDifficulty = DifficultyManager.Instance != null && DifficultyManager.Instance.adjustmentSystemType == AdjustmentSystemType.InRealTime?
        DifficultyManager.Instance.CurrentProfile.wordDifficulty : null;

        WordData selectedData = SelectAndRemoveWord(targetDifficulty, isPlayable) ?? SelectAndRemoveWord(null, isPlayable);

        if (selectedData == null)
        {
            Debug.LogError("[WordImageChallengeSource] Nenhuma palavra local cabe nas mesas disponíveis.");
            return null;
        }

        string word = selectedData.targetWord.ToUpperInvariant();

        if (currentWordImageUI != null && selectedData.wordImage != null)
        {
            currentWordImageUI.sprite = selectedData.wordImage;
        }

        return word;
    }

    // Percorre as palavras disponíveis em ordem aleatória. Palavras que não cabem nas mesas são descartadas
    private WordData SelectAndRemoveWord(WordDifficulty? targetDifficulty, Func<string, bool> isPlayable)
    {
        List<int> skipped = new List<int>();

        while (availableWords.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);
            int wordIndex = availableWords[randomIndex];
            WordData candidate = levelWords[wordIndex];

            if (candidate == null || !isPlayable(candidate.targetWord))
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
