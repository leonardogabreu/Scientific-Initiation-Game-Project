using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traduz o que acontece na partida em eventos de jogo da plataforma.
///
/// O envio em si é da GameEventReporter (fila persistente com retry); aqui fica só o que é
/// específico do Chasing Letters: o recorte de cada rodada e os totais da sessão. Além dos
/// eventos, cada palavra concluída também vira uma resposta em /api/word-challenges/responses,
/// que é o que faz a plataforma parar de sortear desafios já vistos pelo aluno.
/// </summary>
public class DataCollectionManager : MonoBehaviour
{
    private WordAttemptData currentWordAttempt;
    private float wordStartTime;
    public static DataCollectionManager Instance { get; private set; }

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

    private float sessionStartTime;
    private int wordsCompleted;
    private int sessionMistakes;

    public void ReportSessionStarted(int wordsToWin)
    {
        sessionStartTime = Time.time;
        wordsCompleted = 0;
        sessionMistakes = 0;

        WebGLManager session = WebGLManager.Instance;

        GameEventReporter.Instance?.Report("start_session", new StartSessionPayload
        {
            session_number = session?.SessionNumber ?? 0,
            school_id = session?.SchoolId ?? "",
            discipline = session?.Discipline ?? "",
            subarea = session?.Subarea ?? "",
            words_to_win = wordsToWin,
            correct_letter_chance = ChasingLettersGameManager.Instance != null
                ? ChasingLettersGameManager.Instance.GetCorrectLetterChance(DifficultyManager.Instance.CurrentProfile.correctLetterChance)
                : DifficultyManager.Instance.CurrentProfile.correctLetterChance,
            min_spawn_time = DifficultyManager.Instance.CurrentProfile.minSpawnTime,
            max_spawn_time = DifficultyManager.Instance.CurrentProfile.maxSpawnTime,
            treadmill_speed = DifficultyManager.Instance.CurrentProfile.treadmillSpeed,
            player_speed = DifficultyManager.Instance.CurrentProfile.playerSpeed,
            hint_display_time = DifficultyManager.Instance.CurrentProfile.hintDisplayTime,
            hint_cooldown = DifficultyManager.Instance.CurrentProfile.hintCooldown,
            word_difficulty = DifficultyManager.Instance.CurrentProfile.wordDifficulty.ToString(),
            adjustment_system_type = DifficultyManager.Instance.adjustmentSystemType.ToString(),
            game_mode = GameMode
        });
    }

    public void StartNewWordAttempt(string word, string challengeId)
    {
        currentWordAttempt = new WordAttemptData
        {
            targetWord = word,
            challengeId = challengeId,
            wordIndex = wordsCompleted + 1
        };

        wordStartTime = Time.time;

        GameEventReporter.Instance?.Report("start_word", new StartWordPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = challengeId ?? "",
            word = word,
            word_length = word?.Length ?? 0,
            source = challengeId != null ? "platform" : "local",
            game_mode = GameMode
        });
    }

    /// <summary>
    /// Uma letra saiu da esteira ou de uma mesa para a mão do aluno.
    /// <paramref name="slot"/> é a posição na palavra quando veio de uma mesa, ou -1.
    /// </summary>
    public void RegisterLetterPicked(string letter, string source, int slot)
    {
        if (currentWordAttempt == null || !LetterMoved(letter)) return;

        GameEventReporter.Instance?.Report("letter_picked", new LetterActionPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            letter = letter ?? "",
            source = source,
            slot = slot,
            action_index = ++currentWordAttempt.actionsCount,
            time = ElapsedOnWord
        });
    }

    /// <summary>Uma letra foi colocada numa mesa. <paramref name="slot"/> é a posição na palavra.</summary>
    public void RegisterLetterPlaced(string letter, int slot)
    {
        if (currentWordAttempt == null || !LetterMoved(letter)) return;

        GameEventReporter.Instance?.Report("letter_placed", new LetterActionPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            letter = letter ?? "",
            source = "hand",
            slot = slot,
            action_index = ++currentWordAttempt.actionsCount,
            time = ElapsedOnWord
        });

        if (IsResearchSession)
        {
            DifficultyManager.Instance.RegisterCollectedData(MetricType.RightLetterInputs, IsLetterInRightSlot(letter, slot));
        }
    }

    /// <summary>
    /// O aluno trocou a letra que carregava pela de uma caixa da esteira ou de uma mesa.
    /// <paramref name="received"/> é a que passou a carregar e <paramref name="given"/> a que
    /// deixou no lugar — sem os dois lados a trajetória fica com letras mudando sozinhas.
    /// </summary>
    public void RegisterLetterSwapped(string received, string given, string source, int slot)
    {
        if (currentWordAttempt == null || !LetterMoved(received)) return;

        GameEventReporter.Instance?.Report("letter_swapped", new LetterSwapPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            letter = received ?? "",
            given_letter = given ?? "",
            source = source,
            slot = slot,
            action_index = ++currentWordAttempt.actionsCount,
            time = ElapsedOnWord
        });

        if (IsResearchSession && source == "table" && LetterMoved(given))
        {
            DifficultyManager.Instance.RegisterCollectedData(MetricType.RightLetterInputs, IsLetterInRightSlot(given, slot));
        }
    }

    /// <summary>O aluno jogou fora a letra que estava carregando.</summary>
    public void RegisterLetterDiscarded(string letter)
    {
        if (currentWordAttempt == null || !LetterMoved(letter)) return;

        GameEventReporter.Instance?.Report("letter_discarded", new LetterActionPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            letter = letter ?? "",
            source = "hand",
            slot = -1,
            action_index = ++currentWordAttempt.actionsCount,
            time = ElapsedOnWord
        });
    }

    /// <summary>
    /// Entrega errada. <paramref name="wasCompleteAttempt"/> distingue "montou a palavra toda e
    /// errou" de "entregou com mesas vazias" — só a primeira conta como resposta ao desafio.
    /// </summary>
    public void RegisterMistake(string submittedWord, bool wasCompleteAttempt)
    {
        if (currentWordAttempt == null)
        {
            Debug.LogWarning("[DataCollectionManager] RegisterMistake chamado sem tentativa ativa.");
            return;
        }

        currentWordAttempt.mistakesCount++;
        sessionMistakes++;

        if (submittedWord != currentWordAttempt.lastCountedMistakeWord)
        {
            currentWordAttempt.distinctMistakesCount++;
            currentWordAttempt.lastCountedMistakeWord = submittedWord;
        }

        GameEventReporter.Instance?.Report("word_mistake", new WordMistakePayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            submitted_word = submittedWord ?? "",
            mistake_number = currentWordAttempt.mistakesCount,
            complete = wasCompleteAttempt,
            time = ElapsedOnWord
        });

        if (IsResearchSession && wasCompleteAttempt)
        {
            ChasingLettersApiManager.Instance?.SubmitResponse(currentWordAttempt.challengeId, false);
        }
    }

    public void RegisterHint(float hintAvailableSince)
    {
        if (currentWordAttempt == null) return;

        currentWordAttempt.hintsCount++;

        float timeSinceHintAvailable = Time.time - Mathf.Max(hintAvailableSince, wordStartTime);

        GameEventReporter.Instance?.Report("hint_used", new HintUsedPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            hint_number = currentWordAttempt.hintsCount,
            time = ElapsedOnWord,
            time_since_hint_available = timeSinceHintAvailable
        });

        if (IsResearchSession)
        {
            DifficultyManager.Instance.RegisterCollectedData(MetricType.TimeUntilHint, timeSinceHintAvailable);
        }
    }

    public void RegisterAudioReplay()
    {
        if (currentWordAttempt == null) return;

        currentWordAttempt.audioReplaysCount++;

        GameEventReporter.Instance?.Report("audio_replayed", new AudioReplayedPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            replay_number = currentWordAttempt.audioReplaysCount,
            time = ElapsedOnWord
        });
    }

    /// <summary>
    /// Um passo do modo tutorial foi concluído com a ação correta.
    ///
    /// <paramref name="timeToAction"/> é tempo <b>não escalado</b> (o tutorial congela a tela com
    /// Time.timeScale = 0 nos passos de espera) e, junto com <paramref name="wrongKeys"/>, é a
    /// métrica de tempo de reação que o modo tutorial existe para produzir.
    ///
    /// Diferente dos outros métodos daqui, este não desiste quando não há tentativa de palavra
    /// ativa: o passo Cue de uma rodada pode acontecer antes do StartNewWordAttempt, que só roda
    /// no callback de SelectNewWord — e perder o tempo de reação por causa disso seria perder
    /// justamente o dado novo. Os campos da palavra saem vazios nesse caso.
    /// </summary>
    public void RegisterTutorialStep(int roundIndex, string step, KeyCode requestedKey, float timeToAction, int wrongKeys, string wrongKeysPressed)
    {
        GameEventReporter.Instance?.Report("tutorial_step", new TutorialStepPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt?.wordIndex ?? 0,
            challenge_id = currentWordAttempt?.challengeId ?? "",
            word = currentWordAttempt?.targetWord ?? "",
            round_index = roundIndex,
            step = step ?? "",
            requested_key = requestedKey.ToString(),
            awaits_input = requestedKey != KeyCode.None,
            time = timeToAction,
            wrong_keys = wrongKeys,
            wrong_keys_pressed = wrongKeysPressed ?? ""
        });
    }

    public void CountSavedOrBurnedLetters(bool saved)
    {
        if(currentWordAttempt == null) return;

        if (saved) currentWordAttempt.usefulLettersSaved++;  
        else currentWordAttempt.usefulLettersBurned++;

        if (IsResearchSession)
        {
            DifficultyManager.Instance.RegisterCollectedData(MetricType.ReachableLetterCaught, saved);
        }
    }
    public void FinishWordAttempt()
    {
        if (currentWordAttempt == null)
        {
            Debug.LogWarning("[DataCollectionManager] FinishWordAttempt chamado sem tentativa ativa.");
            return;
        }

        currentWordAttempt.timeSpent = ElapsedOnWord;
        wordsCompleted++;

        bool firstTry = currentWordAttempt.mistakesCount == 0;

        GameEventReporter.Instance?.Report("word_completed", new WordCompletedPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            mistakes = currentWordAttempt.mistakesCount,
            hints = currentWordAttempt.hintsCount,
            useful_letters_saved = currentWordAttempt.usefulLettersSaved,
            useful_letters_burned = currentWordAttempt.usefulLettersBurned,
            actions = currentWordAttempt.actionsCount,
            time = currentWordAttempt.timeSpent,
            first_try = firstTry,
            audio_replays = currentWordAttempt.audioReplaysCount
        });

        if (IsResearchSession)
        {
            ChasingLettersApiManager.Instance?.SubmitResponse(currentWordAttempt.challengeId, true);

            DifficultyManager.Instance.RegisterCollectedData(MetricType.NumberOfHints ,currentWordAttempt.hintsCount);
            DifficultyManager.Instance.RegisterCollectedData(MetricType.MissedAttemps, currentWordAttempt.distinctMistakesCount);
        }

        currentWordAttempt = null;
    }

    private bool IsLetterInRightSlot(string letter, int slot)
    {
        List<string> pieces = ChasingLettersGameManager.Instance != null ? ChasingLettersGameManager.Instance.GetPieces(currentWordAttempt.targetWord) : ChallengeSource.SplitIntoLetters(currentWordAttempt.targetWord);
        return slot >= 0 && slot < pieces.Count && letter == pieces[slot];
    }

    public void ReportSessionFinished()
    {
        GameEventReporter.Instance?.Report("finish_session", new FinishSessionPayload
        {
            session_number = SessionNumber,
            words_completed = wordsCompleted,
            total_mistakes = sessionMistakes,
            total_time = Time.time - sessionStartTime
        });
    }

    /// <summary>
    /// CarryLetterBox, DropLetterBox e SwapLetterBox devolvem null quando a ação não aconteceu
    /// (player mal configurado, ou alvo sem LetterText no InteractableCL). Sem esta checagem a
    /// trajetória ganharia eventos de letras que nunca se moveram.
    /// </summary>
    private static bool LetterMoved(string letter) => !string.IsNullOrEmpty(letter);

    /// <summary>
    /// false na sessão de tutorial. Lá a criança é guiada até a resposta certa, então nada do
    /// que ela faz pode alimentar o DifficultyManager — o sistema concluiria que ela domina o
    /// conteúdo e endureceria as fases reais, o oposto do que se quer — nem contar como resposta
    /// a um desafio em /api/word-challenges/responses, que é o que faz a plataforma parar de
    /// sortear a palavra para o aluno.
    ///
    /// Os eventos de /api/events/game continuam sendo enviados normalmente: eles vão marcados com
    /// game_mode "tutorial" e dá para filtrar na análise.
    /// </summary>
    private static bool IsResearchSession => !DifficultyManager.IsTutorialSession;

    private int SessionNumber => WebGLManager.Instance?.SessionNumber ?? 0;

    private float ElapsedOnWord => Time.time - wordStartTime;

    private string GameMode => ChasingLettersGameManager.Instance != null ? ChasingLettersGameManager.Instance.GameMode : "";

    [System.Serializable]
    private class StartSessionPayload
    {
        public int session_number;
        public string school_id;
        public string discipline;
        public string subarea;
        public int words_to_win;
        public int correct_letter_chance;
        public float min_spawn_time;
        public float max_spawn_time;
        public float treadmill_speed;
        public float player_speed;
        public float hint_display_time;
        public float hint_cooldown;
        public string word_difficulty;
        public string adjustment_system_type;
        public string game_mode;
    }

    [System.Serializable]
    private class StartWordPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public int word_length;
        public string source;
        public string game_mode;
    }

    [System.Serializable]
    private class LetterActionPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public string letter;
        public string source;   // "belt", "table" ou "hand"
        public int slot;        // posição na palavra, ou -1
        public int action_index;
        public float time;
    }

    [System.Serializable]
    private class LetterSwapPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public string letter;         // a que o aluno passou a carregar
        public string given_letter;   // a que ficou no lugar do alvo
        public string source;         // "belt" ou "table"
        public int slot;              // posição na palavra, ou -1
        public int action_index;
        public float time;
    }

    [System.Serializable]
    private class WordMistakePayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public string submitted_word;
        public int mistake_number;
        public bool complete;   // false = entregou com mesas vazias
        public float time;
    }

    [System.Serializable]
    private class HintUsedPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public int hint_number;
        public float time;
        public float time_since_hint_available;
    }

    [System.Serializable]
    private class AudioReplayedPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public int replay_number;
        public float time;
    }

    [System.Serializable]
    private class TutorialStepPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;                // a letra da rodada do tutorial
        public int round_index;            // 0-based
        public string step;                // TutorialStepKind
        public string requested_key;       // tecla pedida; "None" nos passos automáticos
        public bool awaits_input;          // false nos passos de áudio, auto-andar e entrega
        public float time;                 // segundos não escalados até a ação correta
        public int wrong_keys;             // teclas erradas antes de acertar
        public string wrong_keys_pressed;  // quais, em ordem, separadas por vírgula
    }

    [System.Serializable]
    private class WordCompletedPayload
    {
        public int session_number;
        public int word_index;
        public string challenge_id;
        public string word;
        public int mistakes;
        public int hints;
        public int useful_letters_saved;
        public int useful_letters_burned;
        public int actions;
        public float time;
        public bool first_try;
        public int audio_replays;
    }

    [System.Serializable]
    private class FinishSessionPayload
    {
        public int session_number;
        public int words_completed;
        public int total_mistakes;
        public float total_time;
    }
}
