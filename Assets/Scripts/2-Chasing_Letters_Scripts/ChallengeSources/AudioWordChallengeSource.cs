using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioWordChallengeSource : ChallengeSource
{
    [Header("Session Words")]
    public AudioWord[] sessionWords;
    private List<int> availableWords = new List<int>();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float firstWordDelay = 3f;
    [SerializeField] private float nextWordDelay = 1f;
    [SerializeField] private ParticleSystem gramophoneVisualEffect;

    [Header("Letter Spawn")]
    [SerializeField, Range(0, 100)] private int correctLetterChance = 0;

    private AudioWord currentWord;
    private string letterPool = "";
    private Coroutine playRoutine;

    public override string GameMode => "audio_word";

    public override string LetterPool => string.IsNullOrEmpty(letterPool) ? base.LetterPool : letterPool;

    public override int GetCorrectLetterChance(int profileChance) => correctLetterChance;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        BuildLetterPool();
        refillAvailableWords();
    }

    void OnEnable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged -= HandleStateChange;
        }
    }

    public override void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady)
    {
        AudioWord selected = SelectAndRemoveWord(isPlayable);

        if (selected == null)
        {
            refillAvailableWords();
            selected = SelectAndRemoveWord(isPlayable);
        }

        if (selected == null)
        {
            Debug.LogError("[AudioWordChallengeSource] Nenhuma palavra da sessão tem áudio e cabe nas mesas disponíveis.");
            onReady(null, null);
            return;
        }

        currentWord = selected;
        onReady(GetNormalizedWord(selected), null);

        if (IsPlaying())
        {
            SchedulePlay(nextWordDelay);
        }
    }

    public void PlayCurrentWord()
    {
        DataCollectionManager.Instance?.RegisterAudioReplay();

        PlayWordAudio();
    }

    private void PlayWordAudio()
    {
        StopScheduledPlay();

        if (audioSource == null || currentWord == null || currentWord.audioClip == null) return;

        audioSource.Stop();
        audioSource.clip = currentWord.audioClip;
        audioSource.Play();

        if (gramophoneVisualEffect != null)
        {
            gramophoneVisualEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gramophoneVisualEffect.Play();
        }
    }

    private void HandleStateChange(GameStateCL newState)
    {
        if (newState == GameStateCL.Playing)
        {
            SchedulePlay(firstWordDelay);
            return;
        }

        StopScheduledPlay();

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void SchedulePlay(float delay)
    {
        StopScheduledPlay();
        playRoutine = StartCoroutine(PlayAfterDelay(delay));
    }

    private IEnumerator PlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        playRoutine = null;
        PlayWordAudio();
    }

    private void StopScheduledPlay()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    private bool IsPlaying()
    {
        return GameCycleManager.Instance != null && GameCycleManager.Instance.currentGameState == GameStateCL.Playing;
    }

    private AudioWord SelectAndRemoveWord(Func<string, bool> isPlayable)
    {
        while (availableWords.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableWords.Count);

            if (availableWords.Count > 1 && sessionWords[availableWords[randomIndex]] == currentWord)
            {
                randomIndex = (randomIndex + 1) % availableWords.Count;
            }

            AudioWord candidate = sessionWords[availableWords[randomIndex]];
            availableWords.RemoveAt(randomIndex);

            if (!IsValidWord(candidate) || !isPlayable(GetNormalizedWord(candidate)))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void BuildLetterPool()
    {
        letterPool = "";

        if (sessionWords == null) return;

        foreach (AudioWord audioWord in sessionWords)
        {
            if (!IsValidWord(audioWord))
            {
                Debug.LogWarning($"[AudioWordChallengeSource] Palavra ignorada (sem áudio ou fora do alfabeto do jogo): '{(audioWord != null ? audioWord.name : "vazia")}'.");
                continue;
            }

            foreach (char letter in GetNormalizedWord(audioWord))
            {
                if (letterPool.IndexOf(letter) < 0)
                {
                    letterPool += letter;
                }
            }
        }
    }

    protected virtual bool IsValidWord(AudioWord audioWord)
    {
        return audioWord != null && audioWord.audioClip != null && GetNormalizedWord(audioWord) != null;
    }

    protected string GetNormalizedWord(AudioWord audioWord)
    {
        if (audioWord == null) return null;

        return ChasingLettersApiManager.TryNormalizeWord(audioWord.targetWord, out string word) ? word : null;
    }

    public void refillAvailableWords()
    {
        if(sessionWords != null && sessionWords.Length != 0)
        {
            availableWords.Clear();
            for(int i=0; i<sessionWords.Length; i++)
            {
                availableWords.Add(i);
            }
        }
    }
}
