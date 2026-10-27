using UnityEngine;

[CreateAssetMenu(fileName = "NewWordData", menuName = "Chasing Letters/Word Data")]
public class WordData : ScriptableObject
{
    public string targetWord;
    public Sprite wordImage;
    public WordDifficulty difficulty = WordDifficulty.Medium;

#if UNITY_EDITOR
    [ContextMenu("Classificar dificuldade pelo tamanho da palavra")]
    private void ClassifyByWordLength()
    {
        difficulty = WordDifficultyClassifier.Classify(targetWord);
    }
#endif
}
