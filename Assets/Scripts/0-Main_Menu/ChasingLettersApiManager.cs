using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class ApiWordResponse 
{
    public string challenge_id;
    public string word;
    public string image_url;
}

public class ChasingLettersApiManager : MonoBehaviour
{
    public static ChasingLettersApiManager Instance; 
    
    [SerializeField] private string baseUrl = "https://adapt2learn-895112363610.us-central1.run.app"; 
    
    [Header("Conexões com o Jogo")]
    [SerializeField] private ChasingLettersGameManager gameManager;
    [SerializeField] private Image screenImage;
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        FetchWordFromApi();
    }

    public void FetchWordFromApi() 
    {
        StartCoroutine(FetchSinglePuzzleCoroutine());
    }

    private IEnumerator FetchSinglePuzzleCoroutine()
    {
        WebGLManager wgl = WebGLManager.Instance; 
        
        float timeout = 10f;
        while (wgl != null && (wgl.AuthToken == null || wgl.CurrentParams == null) && timeout > 0)
        {
            Debug.Log($"Aguardando autenticação do Firebase... ({timeout}s restantes)");
            yield return new WaitForSeconds(0.5f);
            timeout -= 0.5f;
        }

        if (wgl?.AuthToken != null && wgl?.CurrentParams != null)
        {
            string schoolId = wgl.CurrentParams.schoolId ?? "";
            string discipline = wgl.CurrentParams.discipline ?? "";
            string subarea = wgl.CurrentParams.subarea ?? "";

            List<string> queryStrings = new List<string>
            {
                $"school_id={UnityWebRequest.EscapeURL(schoolId)}",
                $"discipline={UnityWebRequest.EscapeURL(discipline)}",
                $"subarea={UnityWebRequest.EscapeURL(subarea)}"
            };

            string requestUrl = $"{baseUrl}/api/word-challenges/puzzles?{string.Join("&", queryStrings)}";

            using (UnityWebRequest www = UnityWebRequest.Get(requestUrl))
            {
                www.SetRequestHeader("Authorization", $"Bearer {wgl.AuthToken}");
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Falha ao comunicar com a API: {www.error}");
                }
                else if (www.downloadHandler?.text != null) 
                {
                    ApiWordResponse parsedData = JsonUtility.FromJson<ApiWordResponse>(www.downloadHandler.text);
                    
                    if (parsedData?.word != null)
                    {
                        Debug.Log($"Sucesso! A palavra sorteada pela API é: {parsedData.word}");

                        if (gameManager != null)
                        {
                            gameManager.targetWord = parsedData.word.ToUpper();
                        }

                        if (parsedData.image_url != null)
                        {
                            StartCoroutine(DownloadAndSetImage(parsedData.image_url));
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("Falha de rede: O Token do Firebase não foi recebido a tempo.");
        }
    }

    private IEnumerator DownloadAndSetImage(string imageUrl) 
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro ao baixar imagem do telão: {www.error}");
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                if (texture != null && screenImage != null)
                {
                    Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    if (newSprite != null)
                    {
                        screenImage.sprite = newSprite;
                        screenImage.preserveAspect = true;
                    }
                }
            }
        }
    }
}