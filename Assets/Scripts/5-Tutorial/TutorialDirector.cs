using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Máquina de passos do modo tutorial, e dono único do input enquanto está ativo.
///
/// Vive na cena 5-Tutorial, então <see cref="Instance"/> é null em qualquer outra fase — é o
/// mesmo padrão de DifficultyManager.HasInstance já usado no projeto, e é o que mantém o resto
/// do jogo intacto (dois if de uma linha nos scripts existentes).
///
/// Congelamento: Time.timeScale = 0 nos passos de espera, 1 durante o auto-andar. Por isso
/// TODA espera daqui usa WaitForSecondsRealtime ou Time.unscaledDeltaTime: um WaitForSeconds
/// travaria para sempre. Time.timeScale volta a 1 em OnDisable, OnDestroy e em qualquer
/// transição para GameStateCL.Start ou Over.
///
/// Nada de telemetria é chamado daqui: a telemetria assina os eventos públicos.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    /// <summary>true quando o modo tutorial está no comando do input.</summary>
    public static bool IsActive => Instance != null && Instance.IsRunning;

    [Header("Rodadas")]
    [SerializeField] private List<TutorialRound> rounds = new List<TutorialRound>();

    [Header("Cena")]
    [Tooltip("Opcionais: o robô mora na cena Core, então são resolvidos na cena em runtime se vazios.")]
    [SerializeField] private PlayerControllerCL player;
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("AudioSource do áudio das letras. Se vazio, usa o AudioSource deste objeto.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Tempos (segundos NÃO escalados)")]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Quanto tempo a letra fica na tela no passo Cue antes de pedir a primeira tecla.")]
    [SerializeField] private float cueDuration = 1.5f;
    [Tooltip("Respiro entre passos, para a criança perceber a troca de prompt.")]
    [SerializeField] private float stepPause = 0.3f;
    [Tooltip("Espera antes da entrega automática no fim da rodada.")]
    [SerializeField] private float roundCompleteDelay = 0.6f;
    [Tooltip("Timeout do auto-andar: depois disso o robô é teleportado para a âncora e o tutorial segue.")]
    [SerializeField] private float moveTimeout = 8f;

    [Header("Validação de âncoras")]
    [Tooltip("Só é usado se o PlayerControllerCL não for encontrado. Normalmente o alcance vem do interactionRadius do player da cena (Core.unity: 10).")]
    [SerializeField] private float maxAnchorDistance = 1f;

    /// <summary>Fração do interactionRadius exigida da âncora, como margem para o robô parar torto.</summary>
    private const float AnchorReachSafety = 0.9f;

    // ---- estado público ----

    public bool IsRunning { get; private set; }

    /// <summary>true apenas no passo que espera Space.</summary>
    public bool AllowsInteraction { get; private set; }

    public TutorialStepKind CurrentStep { get; private set; }

    /// <summary>Índice 0-based da rodada atual.</summary>
    public int CurrentRoundIndex { get; private set; }

    public int TotalRounds => rounds != null ? rounds.Count : 0;

    public string CurrentLetter => CurrentRound != null ? CurrentRound.letter : null;

    // ---- eventos para a camada visual e para a telemetria ----

    /// <summary>Passo 1: mostrar a letra na tela. Argumento: a letra ("A").</summary>
    public event Action<string> onCue;

    /// <summary>Passos 2 e 5: mostrar o ícone da tecla pedida.</summary>
    public event Action<KeyCode> onAwaitKey;

    /// <summary>Passos 4 e 7: mostrar o ícone de ESPAÇO.</summary>
    public event Action onAwaitInteract;

    /// <summary>Tecla diferente da pedida: pulsar o ícone e repetir o áudio.
    /// Argumento: a tecla que a criança apertou.</summary>
    public event Action<KeyCode> onWrongKey;

    /// <summary>Alvo do passo atual, para destacar no mundo. null = esconder o destaque.</summary>
    public event Action<Transform> onHighlightTarget;

    /// <summary>Entrou em um passo novo: esconder os prompts do passo anterior.</summary>
    public event Action<TutorialStepKind> onStepStarted;

    /// <summary>Passo concluído com a ação correta. Argumentos: passo, segundos (não escalados)
    /// que a criança levou para acertar, quantas teclas erradas apertou antes.</summary>
    public event Action<TutorialStepKind, float, int> onStepCompleted;

    /// <summary>Rodada concluída. Argumento: índice 0-based da rodada.</summary>
    public event Action<int> onRoundCompleted;

    public event Action onTutorialFinished;

    // ---- interno ----

    private Coroutine tutorialRoutine;
    private bool subscribedToGameCycle;
    private bool finishNotified;

    private float stepElapsed;
    private int stepWrongKeys;

    private static KeyCode[] trackedKeys;

    private TutorialRound CurrentRound =>
        rounds != null && CurrentRoundIndex >= 0 && CurrentRoundIndex < rounds.Count
            ? rounds[CurrentRoundIndex]
            : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CurrentRoundIndex = 0;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        SubscribeToGameCycle();
    }

    void Start()
    {
        ResolvePlayerReferences();
        SubscribeToGameCycle();
        ValidateRounds();

        // A cena do tutorial entra additive: o estado pode já ser Playing quando este Start roda.
        if (GameCycleManager.Instance != null && GameCycleManager.Instance.currentGameState == GameStateCL.Playing)
        {
            StartTutorial();
        }
    }

    void OnDisable()
    {
        UnsubscribeFromGameCycle();
        StopTutorial(false);
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // Rede de segurança: sair do tutorial nunca pode deixar o jogo congelado.
        Time.timeScale = 1f;

        if (Instance == this) Instance = null;
    }

    private void SubscribeToGameCycle()
    {
        if (subscribedToGameCycle || GameCycleManager.Instance == null) return;

        GameCycleManager.Instance.onGameStateChanged += HandleStateChange;
        subscribedToGameCycle = true;
    }

    private void UnsubscribeFromGameCycle()
    {
        if (!subscribedToGameCycle || GameCycleManager.Instance == null)
        {
            subscribedToGameCycle = false;
            return;
        }

        GameCycleManager.Instance.onGameStateChanged -= HandleStateChange;
        subscribedToGameCycle = false;
    }

    private void HandleStateChange(GameStateCL newState)
    {
        if (newState == GameStateCL.Playing)
        {
            StartTutorial();
            return;
        }

        // Start e Over: o tutorial para e o tempo volta ao normal.
        StopTutorial(true);
    }

    private void ResolvePlayerReferences()
    {
        if (player == null) player = FindAnyObjectByType<PlayerControllerCL>();
        if (agent == null && player != null) agent = player.GetComponent<NavMeshAgent>();
        if (agent == null) agent = FindAnyObjectByType<NavMeshAgent>();
    }

    // ---------------------------------------------------------------- validação

    private void ValidateRounds()
    {
        if (rounds == null || rounds.Count == 0)
        {
            Debug.LogError("[TutorialDirector] Nenhuma rodada configurada no Inspector.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[TutorialDirector] PlayerControllerCL não encontrado na cena: o tutorial não consegue interagir.");
        }

        if (agent == null)
        {
            Debug.LogError("[TutorialDirector] NavMeshAgent do robô não encontrado: o auto-andar não funciona.");
        }

        for (int i = 0; i < rounds.Count; i++)
        {
            TutorialRound round = rounds[i];
            int roundNumber = i + 1;

            if (round == null)
            {
                Debug.LogError($"[TutorialDirector] Rodada {roundNumber} está vazia.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(round.letter))
            {
                Debug.LogError($"[TutorialDirector] Rodada {roundNumber}: campo 'letter' vazio.");
            }

            if (round.letterAudio == null)
            {
                Debug.LogWarning($"[TutorialDirector] Rodada {roundNumber} ('{round.letter}'): sem áudio da letra.");
            }

            if (round.letterBox == null) Debug.LogError($"[TutorialDirector] Rodada {roundNumber} ('{round.letter}'): 'letterBox' não atribuída.");
            if (round.table == null) Debug.LogError($"[TutorialDirector] Rodada {roundNumber} ('{round.letter}'): 'table' não atribuída.");

            ValidateAnchor(round.beltAnchor, round.letterBox, roundNumber, "beltAnchor");
            ValidateAnchor(round.tableAnchor, round.table, roundNumber, "tableAnchor");
        }
    }

    /// <summary>
    /// O auto-andar precisa terminar dentro do interactionRadius do player, senão o Space não
    /// encontra nada no OverlapSphere.
    ///
    /// O raio é lido do player da cena, não de uma constante: o default do script é 1.5, mas o
    /// valor serializado no Player de Core.unity é 10, e é o do Inspector que vale em jogo.
    ///
    /// Com um raio grande, estar no alcance não basta — PlayerControllerCL interage com o
    /// interactible MAIS PRÓXIMO, então uma âncora de onde outro alvo fique mais perto faria a
    /// criança pegar a letra errada e o passo nunca concluir.
    /// </summary>
    private void ValidateAnchor(Transform anchor, GameObject target, int roundNumber, string fieldName)
    {
        if (anchor == null)
        {
            Debug.LogError($"[TutorialDirector] Rodada {roundNumber}: '{fieldName}' não atribuída.");
            return;
        }

        if (target == null) return;

        Vector3 targetCenter = GetInteractionCenter(target);
        float distance = Vector3.Distance(anchor.position, targetCenter);
        float reach = player != null ? player.interactionRadius : maxAnchorDistance;

        if (distance > reach * AnchorReachSafety)
        {
            Debug.LogError(
                $"[TutorialDirector] Âncora '{anchor.name}' ({fieldName} da rodada {roundNumber}) está a " +
                $"{distance:F2} unidades do centro do collider de '{target.name}', acima do limite de " +
                $"{reach * AnchorReachSafety:F2} (interactionRadius {reach:F2} com margem de {AnchorReachSafety:P0}). " +
                "O robô vai parar fora do alcance e o Space não vai achar o alvo.");
            return;
        }

        WarnIfAnotherTargetIsCloser(anchor, target, roundNumber, fieldName, distance);
    }

    /// <summary>
    /// Avisa quando, da âncora, outro interactible fica mais perto que o alvo: é ele que o Space
    /// pegaria. Só avisa — a hierarquia do alvo varia (a mesa guarda a caixa como filha), e um
    /// LogError aqui poderia ser falso.
    /// </summary>
    private void WarnIfAnotherTargetIsCloser(Transform anchor, GameObject target, int roundNumber,
        string fieldName, float targetDistance)
    {
        if (player == null) return;

        Collider[] colliders = Physics.OverlapSphere(anchor.position, player.interactionRadius, player.interactionLayer);

        foreach (Collider hit in colliders)
        {
            InteractableCL interactable = hit.GetComponent<InteractableCL>();
            if (interactable == null) continue;

            GameObject found = interactable.gameObject;

            // O próprio alvo, ou algo da família dele (a caixa que mora na mesa), não é concorrente.
            if (found == target || found.transform.IsChildOf(target.transform) ||
                target.transform.IsChildOf(found.transform)) continue;

            float otherDistance = Vector3.Distance(anchor.position, hit.transform.position);

            if (otherDistance < targetDistance)
            {
                Debug.LogWarning(
                    $"[TutorialDirector] Da âncora '{anchor.name}' ({fieldName} da rodada {roundNumber}), " +
                    $"'{found.name}' está a {otherDistance:F2} e o alvo '{target.name}' a {targetDistance:F2}: " +
                    "o Space pegaria o mais próximo, não o alvo.");
            }
        }
    }

    private static Vector3 GetInteractionCenter(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null) collider = target.GetComponentInChildren<Collider>(true);

        return collider != null ? collider.bounds.center : target.transform.position;
    }

    // ---------------------------------------------------------------- ciclo

    private void StartTutorial()
    {
        if (IsRunning || tutorialRoutine != null) return;

        ResolvePlayerReferences();

        if (rounds == null || rounds.Count == 0)
        {
            Debug.LogError("[TutorialDirector] Sem rodadas: o tutorial não vai começar.");
            return;
        }

        finishNotified = false;
        CurrentRoundIndex = 0;
        IsRunning = true;

        // Depois de uma sessão completa o targetWord ficou parado na letra da última rodada
        // (a entrega automática encadeia SelectNewWord() quando já não há rodada seguinte).
        // Pedir a palavra de novo aqui garante que a rodada 1 começa com a letra da rodada 1,
        // senão a primeira entrega automática de um replay avaliaria a letra errada.
        ChasingLettersGameManager.Instance?.SelectNewWord();

        tutorialRoutine = StartCoroutine(RunTutorial());
    }

    private void StopTutorial(bool notify)
    {
        bool wasRunning = IsRunning;

        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        IsRunning = false;
        AllowsInteraction = false;
        Time.timeScale = 1f;

        if (agent != null && agent.isOnNavMesh) agent.ResetPath();

        if (!notify) return;

        onHighlightTarget?.Invoke(null);

        if (wasRunning && !finishNotified)
        {
            finishNotified = true;
            onTutorialFinished?.Invoke();
        }
    }

    private IEnumerator RunTutorial()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        // CurrentRoundIndex avança dentro de RunRound (antes da entrega automática), porque é
        // dele que o TutorialChallengeSource tira a letra da rodada seguinte.
        while (IsRunning && CurrentRoundIndex < rounds.Count)
        {
            int roundIndex = CurrentRoundIndex;
            TutorialRound round = rounds[roundIndex];

            if (round == null)
            {
                CurrentRoundIndex = roundIndex + 1;
                continue;
            }

            yield return RunRound(round, roundIndex);
        }

        tutorialRoutine = null;
        FinishTutorial();
    }

    private void FinishTutorial()
    {
        IsRunning = false;
        AllowsInteraction = false;
        Time.timeScale = 1f;

        onHighlightTarget?.Invoke(null);

        if (finishNotified) return;

        finishNotified = true;
        onTutorialFinished?.Invoke();
    }

    private IEnumerator RunRound(TutorialRound round, int roundIndex)
    {
        // Passo 1 — Cue: toca o áudio da letra e mostra a letra na tela.
        BeginStep(TutorialStepKind.Cue, round.letterBox != null ? round.letterBox.transform : null);
        onCue?.Invoke(round.letter);
        PlayLetterAudio(round);
        yield return WaitRealtime(cueDuration);
        CompleteStep();

        // Passo 2 — a tecla de direção até a esteira.
        BeginStep(TutorialStepKind.AwaitKeyToBelt, round.letterBox != null ? round.letterBox.transform : null);
        KeyCode keyToBelt = ResolveStepKey(round.keyToBeltOverride, round.beltAnchor);
        onAwaitKey?.Invoke(keyToBelt);
        yield return WaitForDirectionKey(keyToBelt, round);
        if (!IsRunning) yield break;
        CompleteStep();
        yield return WaitRealtime(stepPause);

        // Passo 3 — auto-andar até a âncora da letra.
        BeginStep(TutorialStepKind.MoveToBelt, round.letterBox != null ? round.letterBox.transform : null);
        yield return MoveTo(round.beltAnchor, roundIndex, "beltAnchor");
        if (!IsRunning) yield break;
        CompleteStep();

        // Passo 4 — Space para pegar a letra.
        BeginStep(TutorialStepKind.AwaitPick, round.letterBox != null ? round.letterBox.transform : null);
        onAwaitInteract?.Invoke();
        yield return WaitForInteraction(round, true);
        if (!IsRunning) yield break;
        CompleteStep();
        yield return WaitRealtime(stepPause);

        // Passo 5 — a tecla de direção até a mesa.
        BeginStep(TutorialStepKind.AwaitKeyToTable, round.table != null ? round.table.transform : null);
        KeyCode keyToTable = ResolveStepKey(round.keyToTableOverride, round.tableAnchor);
        onAwaitKey?.Invoke(keyToTable);
        yield return WaitForDirectionKey(keyToTable, round);
        if (!IsRunning) yield break;
        CompleteStep();
        yield return WaitRealtime(stepPause);

        // Passo 6 — auto-andar até a âncora da mesa.
        BeginStep(TutorialStepKind.MoveToTable, round.table != null ? round.table.transform : null);
        yield return MoveTo(round.tableAnchor, roundIndex, "tableAnchor");
        if (!IsRunning) yield break;
        CompleteStep();

        // Passo 7 — Space para colocar a letra na mesa.
        BeginStep(TutorialStepKind.AwaitPlace, round.table != null ? round.table.transform : null);
        onAwaitInteract?.Invoke();
        yield return WaitForInteraction(round, false);
        if (!IsRunning) yield break;
        CompleteStep();

        // Passo 8 — entrega automática: reaproveita SFX/VFX, reset das mesas, estrela e a
        // SelectNewWord() que puxa a letra da rodada seguinte.
        BeginStep(TutorialStepKind.RoundComplete, null);
        yield return WaitRealtime(roundCompleteDelay);

        // O índice avança ANTES da entrega: SubmitZoneManager.EvaluateWord() encadeia
        // SelectNewWord(), e o TutorialChallengeSource lê CurrentLetter para a próxima rodada.
        CurrentRoundIndex = roundIndex + 1;
        SubmitZoneManager.Instance?.EvaluateWord();

        onRoundCompleted?.Invoke(roundIndex);
        CompleteStep();
    }

    private void BeginStep(TutorialStepKind step, Transform highlight)
    {
        CurrentStep = step;
        stepElapsed = 0f;
        stepWrongKeys = 0;

        bool isInteractionStep = step == TutorialStepKind.AwaitPick || step == TutorialStepKind.AwaitPlace;
        AllowsInteraction = isInteractionStep;

        bool isMoveStep = step == TutorialStepKind.MoveToBelt || step == TutorialStepKind.MoveToTable;
        Time.timeScale = isMoveStep ? 1f : 0f;

        onStepStarted?.Invoke(step);
        onHighlightTarget?.Invoke(highlight);
    }

    private void CompleteStep()
    {
        AllowsInteraction = false;
        onStepCompleted?.Invoke(CurrentStep, stepElapsed, stepWrongKeys);
    }

    // ---------------------------------------------------------------- esperas

    /// <summary>Espera não escalada que também alimenta o cronômetro do passo.</summary>
    private IEnumerator WaitRealtime(float seconds)
    {
        float remaining = seconds;

        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            stepElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Trava até a tecla pedida (a seta ou a letra WASD equivalente). Tecla errada não avança:
    /// só conta, avisa a camada visual e repete o áudio da letra.
    /// </summary>
    private IEnumerator WaitForDirectionKey(KeyCode expected, TutorialRound round)
    {
        while (true)
        {
            KeyCode pressed = GetFirstKeyDown();

            if (pressed != KeyCode.None)
            {
                if (IsAcceptedKey(expected, pressed)) yield break;

                stepWrongKeys++;
                onWrongKey?.Invoke(pressed);
                PlayLetterAudio(round);
            }

            stepElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Trava até o Space acertar a interação. O director é o dono do input no tutorial: o
    /// trecho do Space em PlayerControllerCL.Update não roda enquanto IsActive.
    /// </summary>
    private IEnumerator WaitForInteraction(TutorialRound round, bool expectCarrying)
    {
        if (player == null)
        {
            // Sem player não há como validar o Space: pular o passo é melhor do que travar a
            // tela congelada para sempre.
            Debug.LogError("[TutorialDirector] PlayerControllerCL ausente: passo de interação pulado.");
            yield break;
        }

        while (true)
        {
            KeyCode pressed = GetFirstKeyDown();

            if (pressed == KeyCode.Space)
            {
                bool interacted = player.TryInteractWithClosest();

                if (interacted && player.isCarrying == expectCarrying) yield break;

                // Space no lugar errado (ou pegou/colocou o que não era): não avança.
                stepWrongKeys++;
                onWrongKey?.Invoke(KeyCode.Space);
                PlayLetterAudio(round);
            }
            else if (pressed != KeyCode.None)
            {
                stepWrongKeys++;
                onWrongKey?.Invoke(pressed);
                PlayLetterAudio(round);
            }

            stepElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Auto-andar com timeScale 1. Chegada: !pathPending e remainingDistance <= stoppingDistance,
    /// com timeout não escalado que faz Warp para a âncora e segue — o tutorial nunca pode ficar
    /// preso esperando o NavMesh.
    /// </summary>
    private IEnumerator MoveTo(Transform anchor, int roundIndex, string fieldName)
    {
        if (anchor == null)
        {
            Debug.LogError($"[TutorialDirector] Rodada {roundIndex + 1}: '{fieldName}' não atribuída, auto-andar ignorado.");
            yield break;
        }

        if (agent == null)
        {
            Debug.LogError("[TutorialDirector] Sem NavMeshAgent: auto-andar ignorado.");
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            agent.Warp(anchor.position);
            yield break;
        }

        agent.ResetPath();
        agent.isStopped = false;
        agent.SetDestination(anchor.position);

        float elapsed = 0f;

        while (true)
        {
            // Um frame de folga para o path ser calculado antes da primeira checagem de chegada.
            stepElapsed += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            yield return null;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) break;

            if (elapsed >= moveTimeout)
            {
                Debug.LogWarning(
                    $"[TutorialDirector] Timeout de {moveTimeout:F0}s no auto-andar até '{anchor.name}' " +
                    $"({fieldName} da rodada {roundIndex + 1}): teleportando o robô para a âncora e seguindo.");

                agent.ResetPath();
                agent.Warp(anchor.position);
                break;
            }
        }

        if (agent.isOnNavMesh) agent.ResetPath();
    }

    // ---------------------------------------------------------------- teclas

    /// <summary>
    /// Eixo dominante de (to - from) no plano XZ, com o mesmo mapeamento das setas do jogo:
    /// +Z → UpArrow/W, -Z → DownArrow/S, -X → LeftArrow/A, +X → RightArrow/D.
    /// </summary>
    public static KeyCode ResolveDirectionKey(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
        {
            return delta.x >= 0f ? KeyCode.RightArrow : KeyCode.LeftArrow;
        }

        return delta.z >= 0f ? KeyCode.UpArrow : KeyCode.DownArrow;
    }

    /// <summary>A tecla do passo: o override da rodada quando não é KeyCode.None, senão a geometria.</summary>
    private KeyCode ResolveStepKey(KeyCode overrideKey, Transform anchor)
    {
        if (overrideKey != KeyCode.None) return overrideKey;

        Vector3 from = player != null ? player.transform.position : transform.position;
        Vector3 to = anchor != null ? anchor.position : from;

        return ResolveDirectionKey(from, to);
    }

    /// <summary>A seta e a letra WASD equivalente valem igual, como no jogo normal.</summary>
    private static bool IsAcceptedKey(KeyCode expected, KeyCode pressed)
    {
        return pressed == expected || pressed == EquivalentKey(expected);
    }

    private static KeyCode EquivalentKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.UpArrow: return KeyCode.W;
            case KeyCode.W: return KeyCode.UpArrow;
            case KeyCode.DownArrow: return KeyCode.S;
            case KeyCode.S: return KeyCode.DownArrow;
            case KeyCode.LeftArrow: return KeyCode.A;
            case KeyCode.A: return KeyCode.LeftArrow;
            case KeyCode.RightArrow: return KeyCode.D;
            case KeyCode.D: return KeyCode.RightArrow;
            default: return KeyCode.None;
        }
    }

    private static KeyCode[] TrackedKeys
    {
        get
        {
            if (trackedKeys == null)
            {
                List<KeyCode> keys = new List<KeyCode>();

                foreach (KeyCode code in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
                {
                    if (code == KeyCode.None) continue;
                    if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6) continue;

                    keys.Add(code);
                }

                trackedKeys = keys.ToArray();
            }

            return trackedKeys;
        }
    }

    /// <summary>A primeira tecla pressionada neste frame, ou KeyCode.None. Funciona com timeScale 0.</summary>
    private static KeyCode GetFirstKeyDown()
    {
        if (!Input.anyKeyDown) return KeyCode.None;

        foreach (KeyCode code in TrackedKeys)
        {
            if (Input.GetKeyDown(code)) return code;
        }

        return KeyCode.None;
    }

    // ---------------------------------------------------------------- áudio

    /// <summary>Repete o áudio da letra da rodada atual.</summary>
    public void ReplayLetterAudio()
    {
        PlayLetterAudio(CurrentRound);
    }

    private void PlayLetterAudio(TutorialRound round)
    {
        if (audioSource == null || round == null || round.letterAudio == null) return;

        // AudioSource não é afetado por Time.timeScale: toca normalmente com a tela congelada.
        audioSource.Stop();
        audioSource.clip = round.letterAudio;
        audioSource.Play();
    }
}
