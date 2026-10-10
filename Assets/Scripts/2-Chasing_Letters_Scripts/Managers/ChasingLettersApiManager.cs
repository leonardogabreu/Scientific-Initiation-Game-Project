using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ApiWordResponse
{
    public string challenge_id;
    public string word;
    public string image_url;
}

public class WordChallenge
{
    public string challengeId;
    public string word;
    public Sprite image;
    public WordDifficulty difficulty;
}

/// <summary>
/// Fonte de palavras vinda da plataforma (GET /api/word-challenges/puzzles) e registro das
/// respostas (POST /api/word-challenges/responses).
///
/// Registrar a resposta não é opcional: o backend usa a coleção word_challenge_responses para
/// preferir desafios que o aluno ainda não viu. Sem esse POST o sorteio repete palavras.
///
/// Devolve null quando não há sessão da plataforma ou nenhum desafio serve — quem chama
/// (ChasingLettersGameManager) cai no banco local de WordData.
/// </summary>
public class ChasingLettersApiManager : MonoBehaviour
{
    private const string PuzzlesPath = "/api/word-challenges/puzzles";
    private const string ResponsesPath = "/api/word-challenges/responses";

    [Tooltip("Quantas palavras diferentes buscar antes de desistir, quando a sorteada não serve.")]
    [SerializeField] private int maxFetchAttempts = 4;

    [Tooltip("Segundos de espera pelo token da plataforma na primeira busca.")]
    [SerializeField] private float firstFetchAuthTimeout = 10f;

    private static ChasingLettersApiManager instance;

    public static ChasingLettersApiManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<ChasingLettersApiManager>();
            }

            return instance;
        }
    }

    private readonly HashSet<string> servedChallengeIds = new HashSet<string>();
    private readonly Dictionary<string, Sprite> imageCache = new Dictionary<string, Sprite>();

    private WordChallenge prefetched;
    private bool isPrefetching;
    private bool waitedForAuth;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    /// <summary>
    /// Entrega um desafio que passe em <paramref name="isPlayable"/>, ou null se a plataforma
    /// não puder fornecer um. Nunca bloqueia o jogo: a resposta chega pelo callback.
    /// </summary>
    public void RequestWord(Func<string, bool> isPlayable, WordDifficulty? targetDifficulty, Action<WordChallenge> onReady)
    {
        StartCoroutine(RequestWordRoutine(isPlayable, targetDifficulty, onReady));
    }

    private IEnumerator RequestWordRoutine(Func<string, bool> isPlayable, WordDifficulty? targetDifficulty, Action<WordChallenge> onReady)
    {
        if (prefetched != null && IsUsable(prefetched, isPlayable))
        {
            WordChallenge ready = prefetched;
            prefetched = null;

            Deliver(ready, onReady);
            StartPrefetch(isPlayable, targetDifficulty);
            yield break;
        }

        prefetched = null;

        if (!waitedForAuth)
        {
            waitedForAuth = true;
            yield return PlatformApiClient.Instance.WaitForAuthentication(firstFetchAuthTimeout);
        }

        WordChallenge fetched = null;
        yield return FetchUsableChallenge(isPlayable, targetDifficulty, result => fetched = result);

        if (fetched == null)
        {
            onReady?.Invoke(null);
            yield break;
        }

        Deliver(fetched, onReady);
        StartPrefetch(isPlayable, targetDifficulty);
    }

    private void Deliver(WordChallenge challenge, Action<WordChallenge> onReady)
    {
        servedChallengeIds.Add(challenge.challengeId);
        onReady?.Invoke(challenge);
    }

    private void StartPrefetch(Func<string, bool> isPlayable, WordDifficulty? targetDifficulty)
    {
        if (isPrefetching || prefetched != null) return;

        StartCoroutine(PrefetchRoutine(isPlayable, targetDifficulty));
    }

    // Adianta a próxima palavra enquanto o aluno joga a atual, para a troca de rodada ser imediata.
    private IEnumerator PrefetchRoutine(Func<string, bool> isPlayable, WordDifficulty? targetDifficulty)
    {
        isPrefetching = true;
        yield return FetchUsableChallenge(isPlayable, targetDifficulty, result => prefetched = result);
        isPrefetching = false;
    }

    private IEnumerator FetchUsableChallenge(Func<string, bool> isPlayable, WordDifficulty? targetDifficulty, Action<WordChallenge> onDone)
    {
        WebGLManager session = WebGLManager.Instance;

        if (session == null || !session.IsAuthenticated)
        {
            Debug.LogWarning("[ChasingLettersApiManager] Sem sessão da plataforma — usando banco local.");
            onDone(null);
            yield break;
        }

        string query =
            $"?school_id={UnityWebRequest.EscapeURL(session.SchoolId)}" +
            $"&discipline={UnityWebRequest.EscapeURL(session.Discipline)}" +
            $"&subarea={UnityWebRequest.EscapeURL(session.Subarea)}";

        WordChallenge fallbackCandidate = null;
        string fallbackImageUrl = null;

        for (int attempt = 1; attempt <= maxFetchAttempts; attempt++)
        {
            ApiResponse response = null;
            yield return PlatformApiClient.Instance.Get(PuzzlesPath + query, result => response = result);

            if (response.IsNotFound)
            {
                Debug.LogWarning(
                    "[ChasingLettersApiManager] Nenhum desafio cadastrado para " +
                    $"escola/disciplina/subárea ({session.SchoolId}/{session.Discipline}/{session.Subarea}).");
                break;
            }

            if (!response.ok || string.IsNullOrEmpty(response.body)) break;

            ApiWordResponse parsed = JsonUtility.FromJson<ApiWordResponse>(response.body);

            if (parsed == null || !TryNormalizeWord(parsed.word, out string word))
            {
                Debug.LogWarning($"[ChasingLettersApiManager] Palavra ignorada (fora do alfabeto do jogo): '{parsed?.word}'.");
                continue;
            }

            if (servedChallengeIds.Contains(parsed.challenge_id) || !isPlayable(word))
            {
                continue;
            }

            WordChallenge challenge = new WordChallenge
            {
                challengeId = parsed.challenge_id,
                word = word,
                difficulty = WordDifficultyClassifier.Classify(word)
            };

            bool matchesDifficulty = !targetDifficulty.HasValue || challenge.difficulty == targetDifficulty.Value;

            if (matchesDifficulty)
            {
                yield return LoadImage(parsed.image_url, sprite => challenge.image = sprite);
                onDone(challenge);
                yield break;
            }

            // Guarda a primeira jogável fora do nível pedido como plano B: melhor jogar com uma
            // palavra fora da dificuldade atual do que não ter palavra nenhuma.
            if (fallbackCandidate == null)
            {
                fallbackCandidate = challenge;
                fallbackImageUrl = parsed.image_url;
            }
        }

        if (fallbackCandidate != null)
        {
            yield return LoadImage(fallbackImageUrl, sprite => fallbackCandidate.image = sprite);
            onDone(fallbackCandidate);
            yield break;
        }

        onDone(null);
    }

    private bool IsUsable(WordChallenge challenge, Func<string, bool> isPlayable)
    {
        return challenge != null
            && !servedChallengeIds.Contains(challenge.challengeId)
            && isPlayable(challenge.word);
    }

    /// <summary>
    /// A API devolve a palavra em caixa alta, mas pode conter espaços (expressões de duas
    /// palavras). As mesas e a esteira trabalham só com letras, então espaços são removidos e
    /// palavras com qualquer caractere fora do alfabeto do jogo são descartadas — a esteira
    /// nunca conseguiria produzir a letra que falta.
    /// </summary>
    public static bool TryNormalizeWord(string raw, out string word)
    {
        word = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string candidate = raw.Trim().ToUpperInvariant().Replace(" ", "");
        if (candidate.Length == 0) return false;

        foreach (char letter in candidate)
        {
            if (ChasingLettersGameManager.Alphabet.IndexOf(letter) < 0) return false;
        }

        word = candidate;
        return true;
    }

    private IEnumerator LoadImage(string imageUrl, Action<Sprite> onLoaded)
    {
        if (string.IsNullOrEmpty(imageUrl)) yield break;

        if (imageCache.TryGetValue(imageUrl, out Sprite cached))
        {
            onLoaded(cached);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ChasingLettersApiManager] Erro ao baixar imagem do telão: {request.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) yield break;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            imageCache[imageUrl] = sprite;
            onLoaded(sprite);
        }
    }

    /// <summary>
    /// Registra que o aluno respondeu o desafio. <paramref name="correct"/> significa "montou a
    /// palavra sem nenhuma entrega errada" — no Chasing Letters a rodada só avança no acerto,
    /// então o sinal útil é ter acertado de primeira.
    /// </summary>
    public void SubmitResponse(string challengeId, bool correct)
    {
        if (string.IsNullOrEmpty(challengeId)) return;

        // Roda no cliente persistente: o registro da última palavra não pode morrer junto
        // com esta cena quando o aluno volta para o menu logo depois de terminar.
        PlatformApiClient.Instance.StartCoroutine(SubmitResponseRoutine(challengeId, correct));
    }

    private IEnumerator SubmitResponseRoutine(string challengeId, bool correct)
    {
        WordChallengeResponseBody body = new WordChallengeResponseBody
        {
            challenge_id = challengeId,
            game_session_number = WebGLManager.Instance?.SessionNumber ?? 0,
            correct = correct
        };

        yield return PlatformApiClient.Instance.Post(ResponsesPath, JsonUtility.ToJson(body), null);
    }

    [Serializable]
    private class WordChallengeResponseBody
    {
        public string challenge_id;
        public int game_session_number;
        public bool correct;
    }
}
