// Classificação provisória por tamanho da palavra (ex.: GATO = fácil, HIPOPÓTAMO = difícil).
// Isolado numa classe própria para poder ser trocado por outro critério
public enum WordDifficulty
{
    Easy,
    Medium,
    Hard
}
public static class WordDifficultyClassifier
{
    private const int EasyMaxLength = 4;
    private const int MediumMaxLength = 7;

    public static WordDifficulty Classify(string word)
    {
        int length = string.IsNullOrEmpty(word) ? 0 : word.Length;

        if (length <= EasyMaxLength) return WordDifficulty.Easy;
        if (length <= MediumMaxLength) return WordDifficulty.Medium;
        return WordDifficulty.Hard;
    }
}
