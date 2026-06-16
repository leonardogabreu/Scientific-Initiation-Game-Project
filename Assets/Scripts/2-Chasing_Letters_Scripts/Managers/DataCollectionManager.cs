using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class DataCollectionManager : MonoBehaviour
{
    private GameSessionData currentSession;
    private WordAttemptData currentWordAttempt;
    private float sessionTimer;
    private float wordTimer;

    void Update()
    {
        if (currentSession != null)
        {
            sessionTimer += Time.deltaTime;
        }

        if (currentWordAttempt != null)
        {
            wordTimer += Time.deltaTime;
        }
    }

    // Called when changing game mode to Playing in GameCycleManager
    public void startNewGameSession()
    {
        currentSession = new GameSessionData();
        currentSession.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        sessionTimer = 0f;
    }

    // Called everytime a new word is picked
    public void startNewWordAttempt(string word)
    {
        currentWordAttempt = new WordAttemptData();
        currentWordAttempt.targetWord = word;
        wordTimer = 0f;
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
            currentWordAttempt.timeSpent = wordTimer;
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
            currentSession.totalSessionTime = sessionTimer;
            saveDataToJson();
            
            // Makes the session null to stop the total timer.
            currentSession = null;
        }
    }

    private void saveDataToJson()
    {
        if (currentSession != null)
        {
            string jsonString = JsonUtility.ToJson(currentSession, true);
            
            if (jsonString != null)
            {
                // Uses date and current time when naming the file so it never overwrites
                string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = "/dados_sessao_" + timestamp + ".json";
                string filePath = Application.persistentDataPath + fileName;
                
                File.WriteAllText(filePath, jsonString);
                Debug.Log("Dados salvos com sucesso no caminho: " + filePath);
            }
        }
    }
}