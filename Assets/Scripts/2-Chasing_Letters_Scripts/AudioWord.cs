using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioWord", menuName = "Chasing Letters/Audio Word")]
public class AudioWord : ScriptableObject
{
    public string targetWord;
    public AudioClip audioClip;
    public Sprite wordImage;
}
