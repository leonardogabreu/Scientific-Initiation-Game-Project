using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioImageChallengeSource : AudioWordChallengeSource
{
    [Header("Image Spawn")]
    [SerializeField, Min(0)] private int distractorCount = 2;

    private List<string> roundPool = new List<string>();

    public override string GameMode => "audio_image";

    public override List<string> PiecePool => roundPool;

    public override List<string> GetPieces(string word)
    {
        List<string> pieces = new List<string>();

        if (!string.IsNullOrEmpty(word))
        {
            pieces.Add(word);
        }

        return pieces;
    }

    public override Sprite GetPieceImage(string piece)
    {
        if (string.IsNullOrEmpty(piece) || sessionWords == null) return null;

        foreach (AudioWord audioWord in sessionWords)
        {
            if (IsValidWord(audioWord) && GetNormalizedWord(audioWord) == piece.ToUpper())
            {
                return audioWord.wordImage;
            }
        }

        return null;
    }

    public override void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady)
    {
        base.RequestChallenge(isPlayable, (word, challengeId) =>
        {
            BuildRoundPool(word);
            onReady(word, challengeId);
        });
    }

    protected override bool IsValidWord(AudioWord audioWord)
    {
        return base.IsValidWord(audioWord) && audioWord.wordImage != null;
    }

    private void BuildRoundPool(string word)
    {
        if (string.IsNullOrEmpty(word)) return;

        roundPool.Clear();
        roundPool.Add(word);

        List<string> distractors = new List<string>();

        foreach (AudioWord audioWord in sessionWords)
        {
            if (!IsValidWord(audioWord)) continue;

            string candidate = GetNormalizedWord(audioWord);

            if (candidate != word && !distractors.Contains(candidate))
            {
                distractors.Add(candidate);
            }
        }

        for (int i = 0; i < distractorCount && distractors.Count > 0; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, distractors.Count);
            roundPool.Add(distractors[randomIndex]);
            distractors.RemoveAt(randomIndex);
        }
    }
}
