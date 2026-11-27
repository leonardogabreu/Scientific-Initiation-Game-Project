using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ponte entre o modo tutorial e a coleta de dados.
///
/// O <see cref="TutorialDirector"/> não chama telemetria por dentro de propósito: a dependência
/// é toda nesta direção, para a máquina de passos e a coleta evoluírem sem se tocar. Este
/// componente assina os eventos do director e os traduz nos eventos de dados do
/// <see cref="DataCollectionManager"/>.
///
/// O <c>game_mode: "tutorial"</c> não é escrito aqui: sai do
/// <c>TutorialChallengeSource.GameMode</c>, que o <c>ChasingLettersGameManager.GameMode</c>
/// propaga para todos os payloads do DataCollectionManager. Marcar de novo aqui só criaria duas
/// fontes de verdade para o mesmo campo.
///
/// O dado que o tutorial existe para produzir é o <b>tempo de reação</b>: quantos segundos a
/// criança leva, com a tela congelada, para apertar a tecla pedida, e quantas teclas erradas
/// aperta antes. Isso vem pronto no <c>onStepCompleted</c>.
/// </summary>
public class TutorialTelemetryReporter : MonoBehaviour
{
    [Tooltip("Deixe vazio para procurar no próprio GameObject e depois na cena.")]
    [SerializeField] private TutorialDirector director;

    [Tooltip("Tecla errada repete o áudio da letra (regra 3 do modo tutorial). Marcado, cada " +
             "tecla errada também vira um evento audio_replayed, que é o que alimenta a " +
             "contagem de repetições de áudio do word_completed.")]
    [SerializeField] private bool countWrongKeyAsAudioReplay = true;

    private bool subscribed;

    // Contexto do passo atual. O onStepCompleted entrega passo, tempo e nº de erros, mas não
    // qual tecla estava sendo pedida nem quais foram apertadas — isso vem do onAwaitKey e do
    // onWrongKey, e é zerado a cada onStepStarted.
    private KeyCode requestedKey = KeyCode.None;
    private readonly List<KeyCode> wrongKeysInStep = new List<KeyCode>();

    // Totais da rodada e da sessão, só para o log de conferência do teste manual.
    private int wrongKeysInRound;
    private int audioReplaysInRound;
    private int wrongKeysInTutorial;

    // O director pode já ter terminado o Awake (Instance pronto) ou não, dependendo da ordem
    // de inicialização da cena: tenta no OnEnable e de novo no Start.
    void OnEnable() => Subscribe();

    void Start() => Subscribe();

    void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed) return;

        if (director == null) director = GetComponent<TutorialDirector>();
        if (director == null) director = TutorialDirector.Instance;
        if (director == null) return;

        // Os quatro eventos que viram dado:
        director.onStepCompleted += HandleStepCompleted;
        director.onWrongKey += HandleWrongKey;
        director.onRoundCompleted += HandleRoundCompleted;
        director.onTutorialFinished += HandleTutorialFinished;

        // Contexto para preencher os payloads acima:
        director.onStepStarted += HandleStepStarted;
        director.onAwaitKey += HandleAwaitKey;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || director == null) return;

        director.onStepCompleted -= HandleStepCompleted;
        director.onWrongKey -= HandleWrongKey;
        director.onRoundCompleted -= HandleRoundCompleted;
        director.onTutorialFinished -= HandleTutorialFinished;
        director.onStepStarted -= HandleStepStarted;
        director.onAwaitKey -= HandleAwaitKey;

        subscribed = false;
    }

    /// <summary>Passo novo: o contexto do passo anterior não vale mais.</summary>
    private void HandleStepStarted(TutorialStepKind step)
    {
        wrongKeysInStep.Clear();

        // Os passos de pegar e colocar esperam ESPAÇO; os de direção recebem a tecla pedida no
        // onAwaitKey, que vem logo depois deste evento. Os automáticos não esperam tecla.
        requestedKey = step == TutorialStepKind.AwaitPick || step == TutorialStepKind.AwaitPlace
            ? KeyCode.Space
            : KeyCode.None;
    }

    private void HandleAwaitKey(KeyCode key) => requestedKey = key;

    private void HandleWrongKey(KeyCode pressed)
    {
        wrongKeysInStep.Add(pressed);
        wrongKeysInRound++;
        wrongKeysInTutorial++;

        // A tecla errada faz o áudio da letra tocar de novo (regra 3): isso é uma repetição de
        // áudio como qualquer outra, e cai no audio_replays do word_completed sem código novo.
        if (countWrongKeyAsAudioReplay)
        {
            audioReplaysInRound++;
            DataCollectionManager.Instance?.RegisterAudioReplay();
        }
    }

    private void HandleStepCompleted(TutorialStepKind step, float secondsToAction, int wrongKeys)
    {
        DataCollectionManager.Instance?.RegisterTutorialStep(
            director != null ? director.CurrentRoundIndex : 0,
            step.ToString(),
            requestedKey,
            secondsToAction,
            wrongKeys,
            string.Join(",", wrongKeysInStep));

        wrongKeysInStep.Clear();
    }

    private void HandleRoundCompleted(int roundIndex)
    {
        // A rodada em si já virou word_completed pelo SubmitZoneManager -> FinishWordAttempt;
        // aqui só fecha a contagem da rodada e deixa o resumo no Console, que é o que o
        // pesquisador confere no teste manual.
        // RoundsCompleted não zera com o ciclo de estrelas do modo infinito; roundIndex sim.
        int roundNumber = director != null ? director.RoundsCompleted : roundIndex + 1;

        Debug.Log($"[TutorialTelemetry] Rodada {roundNumber} concluída — " +
                  $"{wrongKeysInRound} tecla(s) errada(s), {audioReplaysInRound} repetição(ões) de áudio.");

        wrongKeysInRound = 0;
        audioReplaysInRound = 0;
    }

    private void HandleTutorialFinished()
    {
        // O finish_session continua vindo do GameCycleManager quando o estado vira Over.
        Debug.Log($"[TutorialTelemetry] Tutorial concluído — {wrongKeysInTutorial} tecla(s) errada(s) no total. " +
                  "Nenhum ajuste de dificuldade foi registrado nesta sessão.");

        Unsubscribe();
    }
}
