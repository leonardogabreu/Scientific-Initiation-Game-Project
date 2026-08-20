using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Globalization;

/// <summary>
/// Envia eventos de tentativa de palavra (Hit/Miss) em tempo real para a API,
/// reaproveitando o endpoint /api/events/game.
/// ATENÇÃO: o formato do payload (document_id/question/answer) foi inferido
/// a partir do QuestionScript.cs do Arthur — validar se
/// esses campos fazem sentido pro contexto do Chasing Letters.
/// </summary>
public class DataCollectionManager : MonoBehaviour
{
    [SerializeField] private string backendUrl = "https://adapt2learn-895112363610.us-central1.run.app/api/events/game";

    private WordAttemptData currentWordAttempt;
    private float wordStartTime;

    public void StartNewWordAttempt(string word)
    {
        currentWordAttempt = new WordAttemptData { targetWord = word };
        wordStartTime = Time.time;
    }

    public void RegisterMistake()
    {
        if (currentWordAttempt == null)
        {
            Debug.LogWarning("[DataCollectionManager] RegisterMistake chamado sem tentativa ativa.");
            return;
        }

        currentWordAttempt.mistakesCount++;
        StartCoroutine(SendGameEvent("Miss", currentWordAttempt.targetWord, Time.time - wordStartTime));
    }

    public void FinishWordAttempt()
    {
        if (currentWordAttempt == null)
        {
            Debug.LogWarning("[DataCollectionManager] FinishWordAttempt chamado sem tentativa ativa.");
            return;
        }

        currentWordAttempt.timeSpent = Time.time - wordStartTime;
        StartCoroutine(SendGameEvent("Hit", currentWordAttempt.targetWord, currentWordAttempt.timeSpent));

        currentWordAttempt = null;
    }

    private IEnumerator SendGameEvent(string eventType, string word, float elapsedTime)
    {
        WebGLManager wgl = WebGLManager.Instance;
        if (wgl == null || string.IsNullOrEmpty(wgl.AuthToken))
        {
            Debug.LogWarning($"[DataCollectionManager] Sem AuthToken — evento '{eventType}' não enviado.");
            yield break;
        }

        string elapsedStr = elapsedTime.ToString("F3", CultureInfo.InvariantCulture);

        GameEventRequest eventData = new GameEventRequest
        {
            event_type = eventType,
            game_id = wgl.CurrentParams?.gameId,
            payload = new GameEventPayload
            {
                time = elapsedStr,
                document_id = "chasing_letters",
                question = word,
                answer = word
            }
        };

        string json = JsonUtility.ToJson(eventData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest req = new UnityWebRequest(backendUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + wgl.AuthToken);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[DataCollectionManager] Erro ao enviar evento '{eventType}': {req.error}");
        }
    }
}

[System.Serializable]
public class GameEventRequest
{
    public string event_type;
    public string game_id;
    public GameEventPayload payload;
}

[System.Serializable]
public class GameEventPayload
{
    public string time;
    public string document_id;
    public string question;
    public string answer;
}