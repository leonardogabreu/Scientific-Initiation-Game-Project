using UnityEngine;

[CreateAssetMenu(fileName = "NewWordData", menuName = "Chasing Letters/Word Data")]
public class WordData : ScriptableObject
{
    public string targetWord;
    public Sprite wordImage;
}
