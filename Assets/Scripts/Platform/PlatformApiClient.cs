using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Resultado já normalizado de uma chamada à API da plataforma.</summary>
public class ApiResponse
{
    public bool ok;
    public long statusCode;
    public string body;
    public string error;

    public bool IsNotFound => statusCode == 404;
}

/// <summary>
/// Único ponto de saída HTTP para a API do Adapt2Learn: injeta o Bearer token, aplica timeout
/// e repete requisições que falharam por rede ou erro 5xx. Vive num GameObject persistente,
/// então uma requisição em voo sobrevive à troca de cena.
/// </summary>
public class PlatformApiClient : MonoBehaviour
{
    public const string BaseUrl = "https://adapt2learn-895112363610.us-central1.run.app";

    private const int MaxAttempts = 3;
    private const int RequestTimeoutSeconds = 15;

    private static PlatformApiClient instance;
    private static bool isQuitting;

    public static PlatformApiClient Instance
    {
        get
        {
            if (instance == null && !isQuitting)
            {
                GameObject host = new GameObject("PlatformApiClient");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<PlatformApiClient>();
            }

            return instance;
        }
    }

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
    /// Espera o host entregar token e parâmetros. Retorna assim que autenticado, ou depois do
    /// timeout — quem chama decide o que fazer sem autenticação (normalmente cair no fallback).
    /// </summary>
    public IEnumerator WaitForAuthentication(float timeoutSeconds)
    {
        float remaining = timeoutSeconds;

        while (WebGLManager.Instance == null || !WebGLManager.Instance.IsAuthenticated)
        {
            // O host manda os parâmetros assim que o Unity carrega. Se eles chegaram vazios,
            // o jogo está rodando fora da plataforma e esperar pelo token é esperar por nada:
            // sai na hora para o jogo seguir com o conteúdo local em vez de travar.
            if (WebGLManager.Instance != null && WebGLManager.Instance.HasEmptySession)
            {
                Debug.Log("[PlatformApiClient] Sem sessão da plataforma — seguindo sem autenticação.");
                yield break;
            }

            if (remaining <= 0f)
            {
                Debug.LogWarning("[PlatformApiClient] Timeout aguardando autenticação da plataforma.");
                yield break;
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public IEnumerator Get(string path, Action<ApiResponse> onDone)
    {
        yield return Send(path, UnityWebRequest.kHttpVerbGET, null, onDone);
    }

    public IEnumerator Post(string path, string jsonBody, Action<ApiResponse> onDone)
    {
        yield return Send(path, UnityWebRequest.kHttpVerbPOST, jsonBody, onDone);
    }

    private IEnumerator Send(string path, string verb, string jsonBody, Action<ApiResponse> onDone)
    {
        string url = path.StartsWith("http") ? path : $"{BaseUrl}{path}";
        ApiResponse response = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, verb))
            {
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = RequestTimeoutSeconds;

                string token = WebGLManager.Instance?.AuthToken;
                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                yield return request.SendWebRequest();

                response = new ApiResponse
                {
                    ok = request.result == UnityWebRequest.Result.Success,
                    statusCode = request.responseCode,
                    body = request.downloadHandler?.text,
                    error = request.error
                };
            }

            if (response.ok || !ShouldRetry(response) || attempt == MaxAttempts) break;

            float backoff = Mathf.Pow(2f, attempt - 1) * 0.5f;
            Debug.LogWarning(
                $"[PlatformApiClient] {verb} {path} falhou (tentativa {attempt}/{MaxAttempts}, " +
                $"status {response.statusCode}): {response.error}. Repetindo em {backoff:F1}s.");
            yield return new WaitForSecondsRealtime(backoff);
        }

        if (!response.ok)
        {
            Debug.LogError($"[PlatformApiClient] {verb} {path} falhou: {response.statusCode} {response.error}");
        }

        onDone?.Invoke(response);
    }

    // 4xx é erro de contrato nosso (token inválido, filtro sem conteúdo): repetir não ajuda.
    private static bool ShouldRetry(ApiResponse response)
    {
        return response.statusCode == 0 || response.statusCode >= 500;
    }
}
