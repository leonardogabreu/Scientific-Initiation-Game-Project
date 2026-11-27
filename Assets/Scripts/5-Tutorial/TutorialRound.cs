using System;
using UnityEngine;

/// <summary>
/// Uma rodada do modo tutorial: uma letra só, com os objetos de cena que o robô precisa
/// visitar para completar o ciclo (pegar na esteira, colocar na mesa).
/// Montado no Inspector pela cena 5-Tutorial.
/// </summary>
[Serializable]
public class TutorialRound
{
    [Tooltip("A letra da rodada (\"A\"). Vira a \"palavra\" da rodada via TutorialChallengeSource.")]
    public string letter;

    [Tooltip("Áudio da letra, de Prefabs/SFX/WordSounds/Letters.")]
    public AudioClip letterAudio;

    [Tooltip("A caixa da letra, já posicionada e parada na esteira.")]
    public GameObject letterBox;

    [Tooltip("Onde o robô para para pegar a letra. No máximo 1.0 unidade do centro do collider da caixa.")]
    public Transform beltAnchor;

    [Tooltip("A mesa de destino da letra.")]
    public GameObject table;

    [Tooltip("Onde o robô para para colocar a letra. No máximo 1.0 unidade do centro do collider da mesa.")]
    public Transform tableAnchor;

    [Tooltip("KeyCode.None = derivar da geometria (TutorialDirector.ResolveDirectionKey).")]
    public KeyCode keyToBeltOverride = KeyCode.None;

    [Tooltip("KeyCode.None = derivar da geometria (TutorialDirector.ResolveDirectionKey).")]
    public KeyCode keyToTableOverride = KeyCode.None;
}

/// <summary>Os oito passos de uma rodada do tutorial, na ordem em que acontecem.</summary>
public enum TutorialStepKind
{
    Cue,
    AwaitKeyToBelt,
    MoveToBelt,
    AwaitPick,
    AwaitKeyToTable,
    MoveToTable,
    AwaitPlace,
    RoundComplete
}
