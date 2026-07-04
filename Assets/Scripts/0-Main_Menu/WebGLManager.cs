using UnityEngine;

[System.Serializable]
public class SessionParams
{
    public string discipline;
    public string subarea;
    public string schoolId;
    public string gameId;
    public string sessionNumber;
}

public class WebGLManager : MonoBehaviour
{
    public static WebGLManager Instance { get; private set; }

    public string AuthToken { get; private set; }
    public SessionParams CurrentParams { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnReceiveParams(string jsonParams)
    {
        if (jsonParams != null)
        {
            CurrentParams = JsonUtility.FromJson<SessionParams>(jsonParams);
            Debug.Log("Parâmetros de sessão injetados no WebGLManager com sucesso.");
        }
    }

    public void OnReceiveAuthToken(string token)
    {
        if (token != null)
        {
            AuthToken = token;
            Debug.Log("Token do Firebase armazenado.");
        }
    }
}