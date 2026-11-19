using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class ChallengeSource : MonoBehaviour
{
    public abstract string GameMode { get; }

    public virtual string LetterPool => ChasingLettersGameManager.Alphabet;

    public virtual List<string> PiecePool => SplitIntoLetters(LetterPool);

    public virtual List<string> GetPieces(string word) => SplitIntoLetters(word);

    public virtual Sprite GetPieceImage(string piece) => null;

    public virtual int GetCorrectLetterChance(int profileChance) => profileChance;

    public static List<string> SplitIntoLetters(string letters)
    {
        List<string> pieces = new List<string>();

        if (string.IsNullOrEmpty(letters)) return pieces;

        foreach (char letter in letters)
        {
            pieces.Add(letter.ToString());
        }

        return pieces;
    }

    public abstract void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady);
}
