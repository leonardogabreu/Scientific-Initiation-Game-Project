using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Fila de eventos de jogo para POST /api/events/game.
///
/// Vive num GameObject persistente e envia um evento por vez: um evento disparado no fim da
/// partida não é cancelado pela troca de cena, como acontecia quando a coroutine pertencia a
/// um manager da cena. Sem token (Editor, ou host sem Firebase) o evento é apenas logado.
/// </summary>
public class GameEventReporter : MonoBehaviour
{
    private const string EventsPath = "/api/events/game";
    private const float AuthWaitSeconds = 20f;

    private static GameEventReporter instance;
    private static bool isQuitting;

    public static GameEventReporter Instance
    {
        get
        {
            if (instance == null && !isQuitting)
            {
                GameObject host = new GameObject("GameEventReporter");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<GameEventReporter>();
            }

            return instance;
        }
    }

    private readonly Queue<string> pendingEvents = new Queue<string>();
    private bool isDispatching;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit() => isQuitting = true;

    /// <summary>
    /// Enfileira um evento. <paramref name="payload"/> precisa ser uma classe [System.Serializable]
    /// — a serialização usa JsonUtility, que ignora dicionários e propriedades.
    /// </summary>
    public void Report(string eventType, object payload)
    {
        string gameId = WebGLManager.Instance?.GameId ?? "";
        string payloadJson = payload != null ? JsonUtility.ToJson(payload) : "{}";

        string body = new StringBuilder()
            .Append("{\"event_type\":\"").Append(Escape(eventType))
            .Append("\",\"game_id\":\"").Append(Escape(gameId))
            .Append("\",\"payload\":").Append(payloadJson)
            .Append("}")
            .ToString();

        pendingEvents.Enqueue(body);

        if (!isDispatching)
        {
            StartCoroutine(DispatchPending());
        }
    }

    private IEnumerator DispatchPending()
    {
        isDispatching = true;

        yield return PlatformApiClient.Instance.WaitForAuthentication(AuthWaitSeconds);

        while (pendingEvents.Count > 0)
        {
            string body = pendingEvents.Dequeue();

            // game_id é obrigatório no backend: sem ele o POST volta 400 em todas as tentativas.
            if (WebGLManager.Instance == null || !WebGLManager.Instance.IsAuthenticated ||
                string.IsNullOrEmpty(WebGLManager.Instance.GameId))
            {
                Debug.Log($"[GameEventReporter] Sem sessão da plataforma — evento não enviado: {body}");
                continue;
            }

            yield return PlatformApiClient.Instance.Post(EventsPath, body, null);
        }

        isDispatching = false;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
