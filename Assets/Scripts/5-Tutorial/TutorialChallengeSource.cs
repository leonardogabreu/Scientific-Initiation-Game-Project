using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Entrega a letra da rodada atual do tutorial como se fosse a "palavra" da rodada, para as
/// mesas (DeliverTablesManager) e o SubmitZoneManager funcionarem sem nenhuma mudança: palavra
/// de 1 letra → lista ímpar de mesas, 1 mesa centralizada.
///
/// É também a origem do game_mode da telemetria: ChasingLettersGameManager.GameMode propaga
/// este "tutorial" para o DataCollectionManager.
/// </summary>
public class TutorialChallengeSource : ChallengeSource
{
    [Tooltip("Opcional: se vazio, usa TutorialDirector.Instance.")]
    [SerializeField] private TutorialDirector director;

    public override string GameMode => "tutorial";

    /// <summary>
    /// Só as letras das rodadas do tutorial: se a esteira estiver gerando letras na cena, elas
    /// vêm deste conjunto em vez do alfabeto inteiro.
    /// </summary>
    public override string LetterPool
    {
        get
        {
            string letter = ResolveLetter();

            return string.IsNullOrEmpty(letter) ? base.LetterPool : letter;
        }
    }

    public override List<string> PiecePool => SplitIntoLetters(LetterPool);

    public override void RequestChallenge(Func<string, bool> isPlayable, Action<string, string> onReady)
    {
        if (onReady == null) return;

        string letter = ResolveLetter();

        if (string.IsNullOrEmpty(letter))
        {
            // Acontece de propósito no fim da última rodada: a entrega automática encadeia
            // SelectNewWord() e já não existe rodada seguinte. A palavra atual é mantida.
            onReady(null, null);
            return;
        }

        if (isPlayable != null && !isPlayable(letter))
        {
            Debug.LogError($"[TutorialChallengeSource] A letra '{letter}' não cabe nas mesas disponíveis da cena do tutorial.");
        }

        onReady(letter, null);
    }

    /// <summary>A letra da rodada atual, normalizada como o resto do jogo (maiúscula, sem espaços).</summary>
    private string ResolveLetter()
    {
        TutorialDirector activeDirector = director != null ? director : TutorialDirector.Instance;

        if (activeDirector == null) return null;

        string raw = activeDirector.CurrentLetter;

        if (!ChasingLettersApiManager.TryNormalizeWord(raw, out string letter))
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogError($"[TutorialChallengeSource] Letra '{raw}' fora do alfabeto do jogo ({ChasingLettersGameManager.Alphabet}).");
            }

            return null;
        }

        return letter;
    }
}
