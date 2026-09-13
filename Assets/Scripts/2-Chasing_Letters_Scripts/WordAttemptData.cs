public class WordAttemptData
{
    public string targetWord = "";
    public string challengeId = null;   // null quando a palavra veio do banco local
    public int wordIndex = 0;           // 1 = primeira palavra da sessão
    public int mistakesCount = 0;
    public int hintsCount = 0;
    public int actionsCount = 0;        // pegar, colocar e descartar letras, na ordem em que aconteceram
    public float timeSpent = 0;
}
