using UnityEngine;
using System.IO;
using System;

public class DataCollectionManager : MonoBehaviour
{
    private GameSessionData currentSession;
    private WordAttemptData currentWordAttempt;
    private float sessionStartTime;
    private float wordStartTime;

    // Called when changing game mode to Playing in GameCycleManager
    public void startNewGameSession()
    {
        currentSession = new GameSessionData();
        currentSession.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        sessionStartTime = Time.time;
    }

    // Called everytime a new word is picked
    public void startNewWordAttempt(string word)
    {
        currentWordAttempt = new WordAttemptData
        {
            targetWord = word
        };
        wordStartTime = Time.time;
    }

    // Called in SubmitZoneManager when player misses
    public void registerMistake()
    {
        if (currentWordAttempt != null)
        {
            currentWordAttempt.mistakesCount++;
        }
    }

    // Called in SubmitZoneManager when player writes the word right
    public void finishWordAttempt()
    {
        if (currentWordAttempt != null && currentSession != null)
        {
            currentWordAttempt.timeSpent = Time.time - wordStartTime;   
            currentSession.wordAttempts.Add(currentWordAttempt);
            
            // Stops the timer from keep counting
            currentWordAttempt = null;
        }
    }

    // Called in GameCycleManager when game is over
    public void finishAndSaveSession()
    {
        if (currentSession != null)
        {
            currentSession.totalSessionTime = Time.time - sessionStartTime; 
            saveDataToJson();
            
            // Makes the session null to stop the total timer.
            currentSession = null;
        }
    }

    private void saveDataToJson()
    {
        if (currentSession == null) return;

        string jsonString = JsonUtility.ToJson(currentSession, true);
        
        if (string.IsNullOrEmpty(jsonString)) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"dados_sessao_{timestamp}.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(filePath, jsonString);
            Debug.Log($"Dados salvos com sucesso no caminho: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao salvar os dados da sessão: {e.Message}");
        }  
    }
}