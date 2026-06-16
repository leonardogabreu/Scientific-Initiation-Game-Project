using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WordAttemptData
{
    public string targetWord;
    public int mistakesCount;
    public float timeSpent;

    public WordAttemptData()
    {
        targetWord = "";
        mistakesCount = 0;
        timeSpent = 0f;
    }
}

[System.Serializable]
public class GameSessionData
{
    public string sessionDate;
    public float totalSessionTime;
    public List<WordAttemptData> wordAttempts;

    public GameSessionData()
    {
        sessionDate = "";
        totalSessionTime = 0f;
        wordAttempts = new List<WordAttemptData>();
    }
}