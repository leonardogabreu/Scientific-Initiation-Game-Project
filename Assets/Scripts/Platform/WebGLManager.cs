using System;
using System.Runtime.InteropServices;
using UnityEngine;

[Serializable]
public class SessionParams
{
    public string discipline;
    public string subarea;
    public string schoolId;
    public string gameId;
    public string sessionNumber;
}

/// <summary>
/// Ponte com a plataforma Adapt2Learn no build WebGL.
///
/// O host (Assets/WebGLTemplates/Adapt2Learn/index.html) entrega os dados da sessão via
/// unityInstance.SendMessage("WebGLManager", "OnReceiveParams" | "OnReceiveAuthToken", ...).
/// Tanto o nome do GameObject quanto o nome dos dois métodos fazem parte do contrato com o
/// host: renomear qualquer um deles quebra a integração sem erro de compilação.
/// </summary>
public class WebGLManager : MonoBehaviour
{
    public const string HostGameObjectName = "WebGLManager";

    public static WebGLManager Instance { get; private set; }

    public string AuthToken { get; private set; }
    public SessionParams CurrentParams { get; private set; }

    /// <summary>Token e parâmetros já chegaram — a partir daqui dá para chamar a API.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && CurrentParams != null;

    /// <summary>
    /// O host respondeu, mas sem sessão: é o jogo aberto fora da plataforma (build local, ou
    /// URL sem query string). Aqui não adianta esperar pelo token — ele nunca vem.
    /// </summary>
    public bool HasEmptySession =>
        CurrentParams != null && string.IsNullOrEmpty(SchoolId) && string.IsNullOrEmpty(GameId);

    /// <summary>Disparado uma única vez, quando token e parâmetros estão ambos disponíveis.</summary>
    public event Action onAuthenticated;

    public string GameId => CurrentParams?.gameId ?? "";
    public string SchoolId => CurrentParams?.schoolId ?? "";
    public string Discipline => CurrentParams?.discipline ?? "";
    public string Subarea => CurrentParams?.subarea ?? "";

    public int SessionNumber =>
        int.TryParse(CurrentParams?.sessionNumber, out int parsed) ? parsed : 0;

#if UNITY_EDITOR
    [Header("Somente no Editor")]
    [Tooltip("Cole um ID token do Firebase para testar a integração rodando dentro do Editor.")]
    [SerializeField] private string editorAuthToken;
    [SerializeField] private SessionParams editorParams = new SessionParams();
#endif

    private bool authenticatedNotified;

    /// <summary>
    /// Garante um WebGLManager mesmo quando a cena de jogo é aberta direto (testes no Editor),
    /// já que a instância "de verdade" só existe no menu principal. Roda depois do Awake da
    /// primeira cena, então a instância configurada no Inspector sempre tem precedência.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null) return;

        Debug.Log("[WebGLManager] Nenhuma instância na cena — criando uma em runtime.");
        new GameObject(HostGameObjectName).AddComponent<WebGLManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAuthToken))
        {
            AuthToken = editorAuthToken;
            CurrentParams = editorParams;
            Debug.Log("[WebGLManager] Usando token e parâmetros do Inspector (modo Editor).");
            NotifyAuthenticatedOnce();
        }
#endif
    }

    [DllImport("__Internal")]
    private static extern void PingReady();

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PingReady();
#endif
    }

    public void OnReceiveParams(string jsonParams)
    {
        if (string.IsNullOrEmpty(jsonParams)) return;

        try
        {
            CurrentParams = JsonUtility.FromJson<SessionParams>(jsonParams);
            Debug.Log($"[WebGLManager] Parâmetros da sessão recebidos: {jsonParams}");
            NotifyAuthenticatedOnce();
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebGLManager] JSON de parâmetros inválido: {e.Message}");
        }
    }

    public void OnReceiveAuthToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return;

        // O host reenvia o token a cada refresh do Firebase; sobrescrever é o comportamento certo.
        AuthToken = token;
        Debug.Log("[WebGLManager] Token do Firebase armazenado.");
        NotifyAuthenticatedOnce();
    }

    private void NotifyAuthenticatedOnce()
    {
        if (authenticatedNotified || !IsAuthenticated) return;

        authenticatedNotified = true;
        onAuthenticated?.Invoke();
    }
}
