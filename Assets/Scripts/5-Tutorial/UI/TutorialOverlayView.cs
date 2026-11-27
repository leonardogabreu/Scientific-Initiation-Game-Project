using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camada visual do modo tutorial: o que a criança vê em cada passo. Monta a própria UI em código
/// (canvas em overlay, painel de escurecimento, card grande com a letra, ícone da tecla pedida e
/// rótulo de progresso "1 de 3") e reage apenas aos eventos públicos do
/// <see cref="TutorialDirector"/> — nunca chama nada que mude o estado do tutorial, exceto
/// <see cref="TutorialDirector.ReplayLetterAudio"/>, que o contrato expõe de propósito.
///
/// Basta um GameObject vazio com este componente na cena do tutorial: o canvas e o destaque no
/// mundo são criados em runtime.
///
/// Decisões que vêm do público (crianças com síndrome de Down em alfabetização):
/// letra enorme e de alto contraste, informação no ícone e não em frase escrita, movimento lento e
/// contínuo, nenhuma piscada rápida (desconforto e risco de fotossensibilidade) e nenhum sinal
/// negativo quando a criança erra a tecla.
///
/// Tempo: todas as animações usam <see cref="Time.unscaledDeltaTime"/> e as esperas usam
/// <c>yield return null</c>/<see cref="WaitForSecondsRealtime"/>, porque o tutorial roda com
/// <c>Time.timeScale = 0</c> na maior parte dos passos.
/// </summary>
[DisallowMultipleComponent]
public class TutorialOverlayView : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("Ordem de desenho do canvas do tutorial. Acima do HUD do jogo.")]
    public int canvasSortOrder = 500;
    [Tooltip("Resolução de referência: todos os tamanhos em px deste componente são nessa escala.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("Tempo de aparecer/desaparecer de toda a camada do tutorial.")]
    public float fadeDuration = 0.4f;

    [Header("Escurecimento da tela")]
    public Color dimColor = new Color(0.03f, 0.04f, 0.10f, 1f);
    [Tooltip("Opacidade no passo da letra: escuro para a letra ser a única coisa na tela.")]
    [Range(0f, 1f)] public float dimAlphaCue = 0.80f;
    [Tooltip("Opacidade enquanto espera a tecla: a criança precisa ver o alvo destacado no mundo.")]
    [Range(0f, 1f)] public float dimAlphaAwait = 0.42f;
    [Tooltip("Opacidade enquanto o robô anda sozinho: quase limpo, o movimento é a informação.")]
    [Range(0f, 1f)] public float dimAlphaMove = 0.12f;
    public float dimFadeDuration = 0.5f;

    [Header("Card da letra")]
    public Vector2 cardSize = new Vector2(560f, 560f);
    public Color cardColor = new Color(1f, 0.99f, 0.94f, 1f);
    public Color cardOutlineColor = new Color(0.07f, 0.08f, 0.12f, 1f);
    public float cardOutlineWidth = 12f;
    [Tooltip("Tamanho da letra. Grande de propósito: é o elemento mais importante da tela.")]
    public float letterFontSize = 420f;
    public Color letterColor = new Color(0.08f, 0.12f, 0.35f, 1f);
    [Tooltip("Opcional. Sem nada aqui, usa a fonte padrão do TextMeshPro do projeto.")]
    public TMP_FontAsset letterFont;

    [Header("Card da letra — lembrete")]
    [Tooltip("Depois do passo da letra, o card encolhe e vai para um canto em vez de desaparecer, " +
             "para a criança continuar vendo qual letra é a da rodada.")]
    public bool keepLetterReminder = true;
    public Vector2 reminderPosition = new Vector2(-690f, 300f);
    [Range(0.1f, 1f)] public float reminderScale = 0.34f;
    [Tooltip("Tempo que o card leva para ir do centro ao canto e voltar. Movimento lento e suave.")]
    public float cardMoveDuration = 0.6f;

    [Header("Progresso")]
    [Tooltip("{0} = rodada atual, {1} = total de rodadas.")]
    public string progressFormat = "{0} de {1}";
    public float progressFontSize = 62f;
    public Color progressColor = new Color(1f, 1f, 1f, 0.92f);
    public Vector2 progressPosition = new Vector2(0f, -70f);
    public float progressDotSize = 46f;
    public float progressDotSpacing = 28f;
    public Color progressDotEmptyColor = new Color(1f, 1f, 1f, 0.28f);
    public Color progressDotFilledColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Usado só se o director ainda não informou o total de rodadas.")]
    public int fallbackTotalRounds = 3;

    [Header("Ícone da tecla")]
    [Tooltip("Opcional: um TutorialKeyIcon já montado na cena. Vazio = criado em runtime.")]
    public TutorialKeyIcon keyIcon;
    public Vector2 keyIconPosition = new Vector2(0f, -330f);
    public float keyIconSize = 190f;
    public Color keyCapColor = new Color(1f, 1f, 1f, 1f);
    public Color keyGlyphColor = new Color(0.08f, 0.12f, 0.35f, 1f);
    public Color keyHaloColor = new Color(1f, 0.85f, 0.25f, 0.55f);
    [Tooltip("Duração de um ciclo do pulso do ícone, em segundos.")]
    public float keyPulseDuration = 1.3f;
    [Tooltip("Crescimento do ícone no pico do pulso normal (0.12 = 12%).")]
    public float keyPulseScale = 0.12f;
    [Tooltip("Crescimento extra do pulso de reforço, quando a criança aperta a tecla errada.")]
    public float keyWrongPulseScale = 0.26f;

    [Header("Tecla errada")]
    [Tooltip("Repetir o áudio da letra quando a criança aperta a tecla errada. O TutorialDirector " +
             "também repete por conta própria; as duas chamadas no mesmo frame só reiniciam o mesmo " +
             "clipe, mas deixe desmarcado se quiser um dono só para a repetição.")]
    public bool replayLetterAudioOnWrongKey = true;

    [Header("Comemoração da rodada")]
    public Color celebrationRingColor = new Color(0.35f, 0.90f, 0.52f, 0.85f);
    public float celebrationRingStartSize = 340f;
    public float celebrationRingEndSize = 1150f;
    [Tooltip("Comemoração curta: uma onda verde que cresce e some, sem piscar.")]
    public float celebrationDuration = 1.0f;
    [Tooltip("Quanto o card da letra cresce junto com a comemoração.")]
    public float celebrationCardPunch = 0.12f;

    [Header("Destaque no mundo")]
    [Tooltip("Cria um TutorialTargetHighlight em runtime se a cena não tiver um.")]
    public bool autoCreateTargetHighlight = true;
    [Tooltip("Opcional: um TutorialTargetHighlight já presente na cena.")]
    public TutorialTargetHighlight targetHighlight;

    private Canvas canvas;
    private GameObject canvasRoot;
    private CanvasGroup rootGroup;
    private RectTransform dimRect;
    private Image dimImage;
    private RectTransform cardRect;
    private Image cardImage;
    private Image cardOutlineImage;
    private TextMeshProUGUI letterText;
    private RectTransform celebrationRect;
    private Image celebrationImage;
    private RectTransform keyIconRect;
    private TextMeshProUGUI progressLabel;
    private RectTransform progressDotsRect;
    private Image[] progressDots = new Image[0];
    private TutorialTargetHighlight createdHighlight;

    private const float CardPunchDuration = 0.55f;

    private bool built;
    private bool subscribed;
    private bool shown;
    private float rootAlpha;
    private float dimAlpha;
    private float dimTargetAlpha;
    private float cardBlend;        // 0 = card grande no centro, 1 = lembrete pequeno no canto
    private float cardTargetBlend;
    private float cardPunchAmplitude;
    private float cardPunchTimer;
    private float cardPunch;
    private bool cardVisible;
    private float cardAlpha;
    private int totalRounds;
    private int filledDots;
    private float waitingForDirector;
    private bool warnedMissingDirector;
    private Coroutine celebrationRoutine;

    void Awake()
    {
        totalRounds = Mathf.Max(1, fallbackTotalRounds);

        Build();
        EnsureTargetHighlight();
        ApplyStyle();
        HideEverything();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // OnEnable pode rodar antes do Awake do director (a ordem entre MonoBehaviours da cena não é
        // garantida). Start roda depois de todos os Awake, então é a segunda chance de assinar antes
        // do primeiro passo acontecer.
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
            canvasRoot = null;
        }

        if (createdHighlight != null)
        {
            Destroy(createdHighlight.gameObject);
            createdHighlight = null;
        }
    }

    void OnValidate()
    {
        // Deixa o pesquisador ajustar cores, tamanhos e durações com o jogo rodando.
        if (built && Application.isPlaying)
        {
            ApplyStyle();
        }
    }

    void Update()
    {
        if (!subscribed)
        {
            // O TutorialDirector é um MonoBehaviour da mesma cena: a ordem de Awake não é garantida,
            // então a assinatura é tentada até o Instance existir (mesmo espírito dos guards de
            // DifficultyManager.HasInstance usados no resto do projeto).
            TrySubscribe();

            if (!subscribed)
            {
                waitingForDirector += Time.unscaledDeltaTime;

                if (!warnedMissingDirector && waitingForDirector > 3f)
                {
                    warnedMissingDirector = true;
                    Debug.LogWarning("[TutorialOverlayView] Nenhum TutorialDirector encontrado nesta cena: a camada visual do tutorial vai ficar escondida.", this);
                }
            }
        }

        Animate(Time.unscaledDeltaTime);
    }

    // ---------------------------------------------------------------- eventos do director

    private void TrySubscribe()
    {
        if (subscribed) return;

        TutorialDirector director = TutorialDirector.Instance;

        if (director == null) return;

        director.onCue += HandleCue;
        director.onAwaitKey += HandleAwaitKey;
        director.onAwaitInteract += HandleAwaitInteract;
        director.onWrongKey += HandleWrongKey;
        director.onStepStarted += HandleStepStarted;
        director.onStepCompleted += HandleStepCompleted;
        director.onRoundCompleted += HandleRoundCompleted;
        director.onTutorialFinished += HandleTutorialFinished;

        subscribed = true;

        RefreshTotalRounds();
        RefreshProgressLabel();
        SyncToCurrentState(director);
    }

    /// <summary>
    /// Se a assinatura aconteceu depois do tutorial já ter começado, desenha o passo atual em vez de
    /// esperar o próximo evento — senão a tela ficaria vazia até o passo seguinte.
    /// </summary>
    private void SyncToCurrentState(TutorialDirector director)
    {
        if (!director.IsRunning) return;

        string letter = director.CurrentLetter;

        if (!string.IsNullOrEmpty(letter))
        {
            letterText.text = letter.ToUpperInvariant();
        }

        HandleStepStarted(director.CurrentStep);
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        TutorialDirector director = TutorialDirector.Instance;

        if (director != null)
        {
            director.onCue -= HandleCue;
            director.onAwaitKey -= HandleAwaitKey;
            director.onAwaitInteract -= HandleAwaitInteract;
            director.onWrongKey -= HandleWrongKey;
            director.onStepStarted -= HandleStepStarted;
            director.onStepCompleted -= HandleStepCompleted;
            director.onRoundCompleted -= HandleRoundCompleted;
            director.onTutorialFinished -= HandleTutorialFinished;
        }

        subscribed = false;
    }

    /// <summary>Passo 1: a letra da rodada, enorme no centro, enquanto o áudio dela toca.</summary>
    private void HandleCue(string letter)
    {
        Show();
        RefreshTotalRounds();
        RefreshProgressLabel();

        letterText.text = string.IsNullOrEmpty(letter) ? string.Empty : letter.ToUpperInvariant();

        cardVisible = true;
        cardTargetBlend = 0f;
        dimTargetAlpha = dimAlphaCue;

        if (keyIcon != null) keyIcon.Hide();
    }

    /// <summary>Passos 2 e 5: o ícone da tecla de direção pedida, pulsando sem parar.</summary>
    private void HandleAwaitKey(KeyCode key)
    {
        Show();
        ShowReminderCard();
        dimTargetAlpha = dimAlphaAwait;

        if (keyIcon != null) keyIcon.Show(key);
    }

    /// <summary>Passos 4 e 7: o mesmo pedido, com o ícone da barra de espaço.</summary>
    private void HandleAwaitInteract()
    {
        Show();
        ShowReminderCard();
        dimTargetAlpha = dimAlphaAwait;

        if (keyIcon != null) keyIcon.Show(KeyCode.Space);
    }

    /// <summary>
    /// Tecla errada: reforça o pedido (pulso mais forte no ícone da tecla certa) e repete o áudio da
    /// letra. Sem som de erro, sem punição, sem mensagem negativa — decisão de produto já fechada.
    /// </summary>
    private void HandleWrongKey(KeyCode pressedKey)
    {
        if (keyIcon != null) keyIcon.Reinforce();

        // Chama o card de novo para o canto da tela: um empurrãozinho de atenção, nada negativo.
        Punch(celebrationCardPunch * 0.6f);

        if (replayLetterAudioOnWrongKey)
        {
            TutorialDirector.Instance?.ReplayLetterAudio();
        }
    }

    /// <summary>Passo novo: esconde os prompts do passo anterior e ajusta o escurecimento.</summary>
    private void HandleStepStarted(TutorialStepKind step)
    {
        Show();

        if (keyIcon != null) keyIcon.Hide();

        switch (step)
        {
            case TutorialStepKind.Cue:
                cardVisible = true;
                cardTargetBlend = 0f;
                dimTargetAlpha = dimAlphaCue;
                break;

            case TutorialStepKind.MoveToBelt:
            case TutorialStepKind.MoveToTable:
                ShowReminderCard();
                dimTargetAlpha = dimAlphaMove;
                break;

            default:
                ShowReminderCard();
                dimTargetAlpha = dimAlphaAwait;
                break;
        }

        RefreshProgressLabel();
    }

    /// <summary>
    /// Acertou o passo: o ícone sai de cena na hora. O ícone desaparecer é a confirmação visual —
    /// não precisa de texto nem de som extra.
    /// </summary>
    private void HandleStepCompleted(TutorialStepKind step, float secondsToSucceed, int wrongKeyCount)
    {
        if (keyIcon != null) keyIcon.Hide();
    }

    /// <summary>Rodada concluída: comemoração curta e positiva, e o progresso avança um ponto.</summary>
    private void HandleRoundCompleted(int roundIndex)
    {
        RefreshTotalRounds();

        if (keyIcon != null) keyIcon.Hide();

        filledDots = Mathf.Clamp(roundIndex + 1, 0, totalRounds);
        ApplyProgressDots();
        RefreshProgressLabel();

        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
        }

        celebrationRoutine = StartCoroutine(CelebrationRoutine());
    }

    /// <summary>Tutorial terminou: esconde tudo.</summary>
    private void HandleTutorialFinished()
    {
        if (keyIcon != null) keyIcon.Hide();

        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
            celebrationRoutine = null;
        }

        celebrationImage.color = WithAlpha(celebrationRingColor, 0f);

        shown = false;
        cardVisible = false;
        dimTargetAlpha = 0f;
    }

    // ---------------------------------------------------------------- animação

    private IEnumerator CelebrationRoutine()
    {
        // Onda verde que cresce e some uma única vez, devagar. Nada de flash nem de repetição rápida.
        float duration = Mathf.Max(0.3f, celebrationDuration);
        float elapsed = 0f;

        Punch(celebrationCardPunch);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            float size = Mathf.Lerp(celebrationRingStartSize, celebrationRingEndSize, eased);

            celebrationRect.sizeDelta = new Vector2(size, size);
            celebrationImage.color = WithAlpha(celebrationRingColor, celebrationRingColor.a * (1f - t));

            yield return null;
        }

        celebrationImage.color = WithAlpha(celebrationRingColor, 0f);
        celebrationRoutine = null;
    }

    private void Animate(float delta)
    {
        float rootTarget = shown ? 1f : 0f;
        rootAlpha = Mathf.MoveTowards(rootAlpha, rootTarget, fadeDuration > 0.01f ? delta / fadeDuration : 1f);
        rootGroup.alpha = rootAlpha;

        dimAlpha = Mathf.MoveTowards(dimAlpha, shown ? dimTargetAlpha : 0f, dimFadeDuration > 0.01f ? delta / dimFadeDuration : 1f);
        dimImage.color = WithAlpha(dimColor, dimAlpha);

        cardBlend = Mathf.MoveTowards(cardBlend, cardTargetBlend, cardMoveDuration > 0.01f ? delta / cardMoveDuration : 1f);
        cardAlpha = Mathf.MoveTowards(cardAlpha, cardVisible ? 1f : 0f, fadeDuration > 0.01f ? delta / fadeDuration : 1f);

        // O "punch" cresce e volta em meia volta de seno: nada de degrau nem de piscada.
        if (cardPunchTimer > 0f)
        {
            cardPunchTimer = Mathf.Max(0f, cardPunchTimer - delta);
            float progress = 1f - cardPunchTimer / CardPunchDuration;
            cardPunch = cardPunchAmplitude * Mathf.Sin(progress * Mathf.PI);
        }
        else
        {
            cardPunch = 0f;
        }

        // SmoothStep deixa a ida ao canto com entrada e saída suaves, sem arranque.
        float blend = Mathf.SmoothStep(0f, 1f, cardBlend);
        float scale = Mathf.Lerp(1f, Mathf.Clamp(reminderScale, 0.1f, 1f), blend) * (1f + cardPunch);

        cardRect.anchoredPosition = Vector2.Lerp(Vector2.zero, reminderPosition, blend);
        cardRect.localScale = Vector3.one * scale;

        SetGraphicAlpha(cardImage, cardColor, cardAlpha);
        SetGraphicAlpha(cardOutlineImage, cardOutlineColor, cardAlpha);
        SetGraphicAlpha(letterText, letterColor, cardAlpha);
    }

    private void Show()
    {
        shown = true;
    }

    private void Punch(float amplitude)
    {
        cardPunchAmplitude = Mathf.Max(0f, amplitude);
        cardPunchTimer = CardPunchDuration;
    }

    private void ShowReminderCard()
    {
        cardVisible = keepLetterReminder;
        cardTargetBlend = 1f;
    }

    private void HideEverything()
    {
        shown = false;
        cardVisible = false;
        dimTargetAlpha = 0f;
        rootAlpha = 0f;
        dimAlpha = 0f;
        cardAlpha = 0f;
        cardBlend = 1f;
        cardTargetBlend = 1f;
        cardPunch = 0f;
        cardPunchTimer = 0f;
        rootGroup.alpha = 0f;
        dimImage.color = WithAlpha(dimColor, 0f);
        celebrationImage.color = WithAlpha(celebrationRingColor, 0f);

        if (keyIcon != null) keyIcon.Hide();
    }

    // ---------------------------------------------------------------- progresso

    private void RefreshTotalRounds()
    {
        int directorTotal = TutorialDirector.Instance != null ? TutorialDirector.Instance.TotalRounds : 0;
        int desired = directorTotal > 0 ? directorTotal : Mathf.Max(1, fallbackTotalRounds);

        if (desired == totalRounds && progressDots.Length == desired) return;

        totalRounds = desired;
        BuildProgressDots();
        ApplyProgressDots();
    }

    private void RefreshProgressLabel()
    {
        int round = TutorialDirector.Instance != null ? TutorialDirector.Instance.CurrentRoundIndex + 1 : filledDots + 1;
        round = Mathf.Clamp(round, 1, totalRounds);

        progressLabel.text = string.Format(string.IsNullOrEmpty(progressFormat) ? "{0} de {1}" : progressFormat, round, totalRounds);
    }

    private void ApplyProgressDots()
    {
        for (int i = 0; i < progressDots.Length; i++)
        {
            if (progressDots[i] == null) continue;

            progressDots[i].color = i < filledDots ? progressDotFilledColor : progressDotEmptyColor;
        }
    }

    private void BuildProgressDots()
    {
        for (int i = 0; i < progressDots.Length; i++)
        {
            if (progressDots[i] != null)
            {
                Destroy(progressDots[i].gameObject);
            }
        }

        progressDots = new Image[totalRounds];

        float step = progressDotSize + progressDotSpacing;
        float startX = -step * (totalRounds - 1) * 0.5f;

        for (int i = 0; i < totalRounds; i++)
        {
            RectTransform dot = CreateChild("Dot" + i, progressDotsRect);
            dot.sizeDelta = Vector2.one * progressDotSize;
            dot.anchoredPosition = new Vector2(startX + step * i, 0f);

            progressDots[i] = AddImage(dot, TutorialUIShapes.Circle);
        }
    }

    // ---------------------------------------------------------------- construção da UI

    private void Build()
    {
        if (built) return;
        built = true;

        // O canvas nasce na raiz da cena, e não como filho deste GameObject: um canvas em
        // Screen Space - Overlay não deve herdar escala nem posição de ninguém.
        canvasRoot = new GameObject("TutorialOverlayCanvas");

        canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Sem GraphicRaycaster e com raycastTarget desligado em tudo: a camada é informativa e não
        // deve roubar nenhum clique do jogo.
        rootGroup = canvasRoot.AddComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        rootGroup.alpha = 0f;

        RectTransform canvasRect = (RectTransform)canvasRoot.transform;

        dimRect = CreateChild("Dim", canvasRect);
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dimImage = AddImage(dimRect, TutorialUIShapes.Pixel);

        celebrationRect = CreateChild("Celebration", canvasRect);
        celebrationImage = AddImage(celebrationRect, TutorialUIShapes.Ring);

        cardRect = CreateChild("LetterCard", canvasRect);

        RectTransform cardOutlineRect = CreateChild("Outline", cardRect);
        cardOutlineImage = AddImage(cardOutlineRect, TutorialUIShapes.RoundedRect);

        RectTransform cardFaceRect = CreateChild("Face", cardRect);
        cardImage = AddImage(cardFaceRect, TutorialUIShapes.RoundedRect);

        RectTransform letterRect = CreateChild("Letter", cardRect);
        letterText = letterRect.gameObject.AddComponent<TextMeshProUGUI>();
        letterText.alignment = TextAlignmentOptions.Center;
        letterText.raycastTarget = false;
        letterText.text = string.Empty;

        RectTransform progressRect = CreateChild("Progress", canvasRect);
        progressRect.anchorMin = new Vector2(0.5f, 1f);
        progressRect.anchorMax = new Vector2(0.5f, 1f);
        progressRect.pivot = new Vector2(0.5f, 1f);

        RectTransform progressLabelRect = CreateChild("Label", progressRect);
        progressLabelRect.pivot = new Vector2(0.5f, 1f);
        progressLabelRect.sizeDelta = new Vector2(700f, 90f);
        progressLabel = progressLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        progressLabel.alignment = TextAlignmentOptions.Top;
        progressLabel.raycastTarget = false;

        progressDotsRect = CreateChild("Dots", progressRect);
        progressDotsRect.pivot = new Vector2(0.5f, 1f);

        keyIconRect = CreateChild("KeyIcon", canvasRect);

        if (keyIcon == null)
        {
            keyIcon = keyIconRect.gameObject.AddComponent<TutorialKeyIcon>();
        }

        BuildProgressDots();
    }

    private void ApplyStyle()
    {
        if (!built) return;

        canvas.sortingOrder = canvasSortOrder;

        cardRect.sizeDelta = cardSize;
        ((RectTransform)cardImage.transform).sizeDelta = cardSize;
        ((RectTransform)cardOutlineImage.transform).sizeDelta = cardSize + Vector2.one * (cardOutlineWidth * 2f);
        cardImage.color = cardColor;
        cardOutlineImage.color = cardOutlineColor;

        RectTransform letterRect = (RectTransform)letterText.transform;
        letterRect.sizeDelta = cardSize;
        letterText.fontSize = letterFontSize;
        letterText.color = letterColor;

        if (letterFont != null)
        {
            letterText.font = letterFont;
        }

        progressLabel.fontSize = progressFontSize;
        progressLabel.color = progressColor;

        if (letterFont != null)
        {
            progressLabel.font = letterFont;
        }

        RectTransform progressRect = (RectTransform)progressLabel.transform.parent;
        progressRect.anchoredPosition = progressPosition;
        progressDotsRect.anchoredPosition = new Vector2(0f, -(progressFontSize + 24f));
        progressDotsRect.sizeDelta = new Vector2(0f, progressDotSize);

        celebrationRect.sizeDelta = Vector2.one * celebrationRingStartSize;
        celebrationImage.color = WithAlpha(celebrationRingColor, 0f);

        keyIconRect.anchoredPosition = keyIconPosition;

        if (keyIcon != null)
        {
            keyIcon.keySize = keyIconSize;
            keyIcon.capColor = keyCapColor;
            keyIcon.glyphColor = keyGlyphColor;
            keyIcon.haloColor = keyHaloColor;
            keyIcon.pulseDuration = keyPulseDuration;
            keyIcon.pulseScale = keyPulseScale;
            keyIcon.wrongKeyPulseScale = keyWrongPulseScale;
            keyIcon.outlineColor = cardOutlineColor;

            if (letterFont != null)
            {
                keyIcon.font = letterFont;
            }

            keyIcon.ApplyStyle();
        }

        ApplyProgressDots();
    }

    private void EnsureTargetHighlight()
    {
        if (!autoCreateTargetHighlight || targetHighlight != null) return;

        targetHighlight = FindAnyObjectByType<TutorialTargetHighlight>();

        if (targetHighlight != null) return;

        GameObject highlightObject = new GameObject("TutorialTargetHighlight");
        createdHighlight = highlightObject.AddComponent<TutorialTargetHighlight>();
        targetHighlight = createdHighlight;
    }

    // ---------------------------------------------------------------- utilitários

    private static void SetGraphicAlpha(Graphic graphic, Color baseColor, float alpha)
    {
        if (graphic == null) return;

        graphic.color = WithAlpha(baseColor, baseColor.a * alpha);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static RectTransform CreateChild(string name, RectTransform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        return rect;
    }

    private static Image AddImage(RectTransform target, Sprite sprite)
    {
        Image image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;

        return image;
    }
}
