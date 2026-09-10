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
            words_to_win = wordsToWin
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
            source = challengeId != null ? "platform" : "local"
        });
    }

    /// <summary>
    /// Uma letra saiu da esteira ou de uma mesa para a mão do aluno.
    /// <paramref name="slot"/> é a posição na palavra quando veio de uma mesa, ou -1.
    /// </summary>
    public void RegisterLetterPicked(string letter, string source, int slot)
    {
        if (currentWordAttempt == null) return;

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
        if (currentWordAttempt == null) return;

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
    }

    /// <summary>
    /// O aluno trocou a letra que carregava pela de uma caixa da esteira ou de uma mesa.
    /// <paramref name="received"/> é a que passou a carregar e <paramref name="given"/> a que
    /// deixou no lugar — sem os dois lados a trajetória fica com letras mudando sozinhas.
    /// </summary>
    public void RegisterLetterSwapped(string received, string given, string source, int slot)
    {
        if (currentWordAttempt == null) return;

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
    }

    /// <summary>O aluno jogou fora a letra que estava carregando.</summary>
    public void RegisterLetterDiscarded(string letter)
    {
        if (currentWordAttempt == null) return;

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

        if (wasCompleteAttempt)
        {
            ChasingLettersApiManager.Instance?.SubmitResponse(currentWordAttempt.challengeId, false);
        }
    }

    public void RegisterHint()
    {
        if (currentWordAttempt == null) return;

        currentWordAttempt.hintsCount++;

        GameEventReporter.Instance?.Report("hint_used", new HintUsedPayload
        {
            session_number = SessionNumber,
            word_index = currentWordAttempt.wordIndex,
            challenge_id = currentWordAttempt.challengeId ?? "",
            word = currentWordAttempt.targetWord,
            hint_number = currentWordAttempt.hintsCount,
            time = ElapsedOnWord
        });
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
            actions = currentWordAttempt.actionsCount,
            time = currentWordAttempt.timeSpent,
            first_try = firstTry
        });

        ChasingLettersApiManager.Instance?.SubmitResponse(currentWordAttempt.challengeId, true);

        currentWordAttempt = null;
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

    private int SessionNumber => WebGLManager.Instance?.SessionNumber ?? 0;

    private float ElapsedOnWord => Time.time - wordStartTime;

    [System.Serializable]
    private class StartSessionPayload
    {
        public int session_number;
        public string school_id;
        public string discipline;
        public string subarea;
        public int words_to_win;
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
        public int actions;
        public float time;
        public bool first_try;
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
