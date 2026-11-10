using System;
using UnityEngine;

public abstract class ChallengeSource : MonoBehaviour
{
    public virtual string LetterPool => ChasingLettersGameManager.Alphabet;

    public virtual int GetCorrectLetterChance(int profileChance) => profileChance;

    public abstract void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady);
}
