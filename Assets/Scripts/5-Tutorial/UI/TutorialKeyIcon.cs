using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desenha o ícone da tecla pedida pelo passo atual do tutorial: as quatro setas e a barra de
/// espaço, com o pulso contínuo que diz "o jogo está esperando você" e o reforço mais forte
/// quando a criança aperta a tecla errada.
///
/// Monta a própria hierarquia em código (capa da tecla, contorno, halo e glifo) e não depende de
/// prefab nem de sprite importado. Toda a animação usa <see cref="Time.unscaledDeltaTime"/>,
/// porque o tutorial roda com <c>Time.timeScale = 0</c> na maior parte dos passos: com
/// <c>Time.deltaTime</c> o ícone ficaria parado exatamente quando precisa pulsar.
///
/// Quem liga este componente aos eventos do director é o <see cref="TutorialOverlayView"/>.
/// </summary>
[DisallowMultipleComponent]
public class TutorialKeyIcon : MonoBehaviour
{
    [Header("Tamanho (px na resolução de referência do Canvas)")]
    [Tooltip("Lado da capa da tecla. Ícone grande: o público precisa enxergar de longe e sem esforço.")]
    public float keySize = 190f;
    [Tooltip("Quantas vezes mais larga a barra de espaço é em relação a uma tecla de seta.")]
    public float spaceWidthMultiplier = 3.2f;
    [Tooltip("Tamanho do triângulo da seta em relação à capa (0-1).")]
    [Range(0.2f, 0.9f)] public float glyphScale = 0.52f;
    [Tooltip("Espessura do contorno escuro da capa, que garante contraste sobre qualquer fundo.")]
    public float outlineWidth = 8f;

    [Header("Cores")]
    public Color capColor = new Color(1f, 1f, 1f, 1f);
    public Color outlineColor = new Color(0.07f, 0.08f, 0.12f, 1f);
    public Color glyphColor = new Color(0.08f, 0.12f, 0.35f, 1f);
    [Tooltip("Halo que só aparece no reforço da tecla errada. Alpha controla a intensidade máxima.")]
    public Color haloColor = new Color(1f, 0.85f, 0.25f, 0.55f);

    [Header("Pulso (segundos, tempo não escalado)")]
    [Tooltip("Duração de um ciclo completo do pulso. Movimento lento de propósito: nada de piscar rápido.")]
    public float pulseDuration = 1.3f;
    [Tooltip("Quanto o ícone cresce no pico do pulso normal (0.12 = 12%).")]
    public float pulseScale = 0.12f;
    [Tooltip("Crescimento extra no reforço depois de uma tecla errada.")]
    public float wrongKeyPulseScale = 0.26f;
    [Tooltip("Quanto tempo o reforço leva para voltar ao pulso normal.")]
    public float wrongKeyDuration = 1.8f;
    [Tooltip("Deslocamento do ícone na direção da seta durante o reforço, em px.")]
    public float wrongKeyNudge = 16f;
    [Tooltip("Tempo de aparecer/desaparecer do ícone.")]
    public float fadeDuration = 0.25f;

    [Header("Fonte")]
    [Tooltip("Opcional. Usada só no rótulo de emergência, quando a tecla pedida não é seta nem espaço.")]
    public TMP_FontAsset font;

    private RectTransform visual;
    private RectTransform capRect;
    private CanvasGroup group;
    private Image outlineImage;
    private Image capImage;
    private Image haloImage;
    private RectTransform glyphRect;
    private Image glyphImage;
    private RectTransform barRect;
    private Image barImage;
    private TextMeshProUGUI fallbackLabel;

    private bool built;
    private bool visible;
    private float pulsePhase;
    private float reinforce;
    private float alpha;
    private Vector2 nudgeDirection = Vector2.zero;

    /// <summary>Tecla desenhada agora. <see cref="KeyCode.None"/> quando o ícone está escondido.</summary>
    public KeyCode CurrentKey { get; private set; } = KeyCode.None;

    /// <summary>true enquanto o ícone está pedindo uma tecla (visível e pulsando).</summary>
    public bool IsVisible => visible;

    void Awake()
    {
        EnsureBuilt();
        ApplyStyle();
        ApplyAlpha(0f);
    }

    void OnValidate()
    {
        // Permite ao pesquisador ajustar cores, tamanhos e duração do pulso com o jogo rodando.
        if (built && Application.isPlaying)
        {
            ApplyStyle();
        }
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;

        float targetAlpha = visible ? 1f : 0f;
        float fadeStep = fadeDuration > 0.01f ? delta / fadeDuration : 1f;
        ApplyAlpha(Mathf.MoveTowards(alpha, targetAlpha, fadeStep));

        if (reinforce > 0f)
        {
            reinforce = Mathf.Max(0f, reinforce - (wrongKeyDuration > 0.01f ? delta / wrongKeyDuration : 1f));
        }

        if (alpha <= 0f)
        {
            return;
        }

        float cycle = Mathf.Max(0.6f, pulseDuration);
        pulsePhase += delta / cycle;

        if (pulsePhase > 1f)
        {
            pulsePhase -= Mathf.Floor(pulsePhase);
        }

        // Swell suave de 0 a 1 e de volta: cresce e encolhe sem nenhum degrau, sem piscada.
        float swell = 0.5f * (1f - Mathf.Cos(pulsePhase * Mathf.PI * 2f));
        float amplitude = pulseScale + wrongKeyPulseScale * reinforce;

        if (visual != null)
        {
            visual.localScale = Vector3.one * (1f + amplitude * swell);
            visual.anchoredPosition = nudgeDirection * (wrongKeyNudge * reinforce * swell);
        }

        if (haloImage != null)
        {
            Color halo = haloColor;
            halo.a = haloColor.a * reinforce * Mathf.Lerp(0.55f, 1f, swell);
            haloImage.color = halo;
        }
    }

    /// <summary>Mostra o ícone da tecla pedida e começa o pulso contínuo.</summary>
    public void Show(KeyCode key)
    {
        EnsureBuilt();

        CurrentKey = key;
        visible = true;
        pulsePhase = 0f;

        ConfigureForKey(key);
    }

    /// <summary>Esconde o ícone (fade curto) e zera o reforço.</summary>
    public void Hide()
    {
        visible = false;
        reinforce = 0f;
        CurrentKey = KeyCode.None;

        if (visual != null)
        {
            visual.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Reforça o pedido depois de uma tecla errada: pulso mais forte, halo quente e um empurrãozinho
    /// do ícone na direção da seta. Sem som de erro e sem nada negativo — decisão de produto.
    /// </summary>
    public void Reinforce()
    {
        if (!visible) return;

        reinforce = 1f;
        pulsePhase = 0f;
    }

    /// <summary>Mapeia a tecla para a rotação do triângulo e a direção do empurrão. Aceita setas e WASD.</summary>
    public static bool TryGetArrowDirection(KeyCode key, out float zRotation, out Vector2 direction)
    {
        switch (key)
        {
            case KeyCode.UpArrow:
            case KeyCode.W:
                zRotation = 0f;
                direction = Vector2.up;
                return true;
            case KeyCode.DownArrow:
            case KeyCode.S:
                zRotation = 180f;
                direction = Vector2.down;
                return true;
            case KeyCode.LeftArrow:
            case KeyCode.A:
                zRotation = 90f;
                direction = Vector2.left;
                return true;
            case KeyCode.RightArrow:
            case KeyCode.D:
                zRotation = -90f;
                direction = Vector2.right;
                return true;
        }

        zRotation = 0f;
        direction = Vector2.zero;
        return false;
    }

    private void ConfigureForKey(KeyCode key)
    {
        bool isArrow = TryGetArrowDirection(key, out float zRotation, out Vector2 direction);
        bool isSpace = key == KeyCode.Space;

        nudgeDirection = direction;

        float width = isSpace ? keySize * Mathf.Max(1f, spaceWidthMultiplier) : keySize;
        capRect.sizeDelta = new Vector2(width, keySize);
        ((RectTransform)outlineImage.transform).sizeDelta = new Vector2(width + outlineWidth * 2f, keySize + outlineWidth * 2f);
        ((RectTransform)haloImage.transform).sizeDelta = new Vector2(width + keySize * 0.55f, keySize * 1.55f);

        glyphImage.enabled = isArrow;
        glyphRect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        glyphRect.sizeDelta = Vector2.one * (keySize * glyphScale);

        barImage.enabled = isSpace;
        barRect.sizeDelta = new Vector2(width * 0.62f, keySize * 0.17f);

        bool needsLabel = !isArrow && !isSpace;
        fallbackLabel.enabled = needsLabel;

        if (needsLabel)
        {
            // Rede de segurança: se o pesquisador sobrescrever a tecla da rodada por algo que não é
            // seta nem espaço, o ícone mostra o nome em vez de ficar vazio.
            fallbackLabel.text = key == KeyCode.None ? "?" : key.ToString().ToUpperInvariant();
            fallbackLabel.fontSize = keySize * 0.34f;
        }
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        RectTransform self = GetComponent<RectTransform>();

        if (self == null)
        {
            self = gameObject.AddComponent<RectTransform>();
        }

        visual = CreateChild("Visual", self);
        visual.sizeDelta = Vector2.zero;

        group = visual.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform haloTransform = CreateChild("Halo", visual);
        haloImage = AddImage(haloTransform, TutorialUIShapes.Ring);

        RectTransform outlineTransform = CreateChild("Outline", visual);
        outlineImage = AddImage(outlineTransform, TutorialUIShapes.RoundedRect);

        capRect = CreateChild("Cap", visual);
        capImage = AddImage(capRect, TutorialUIShapes.RoundedRect);

        glyphRect = CreateChild("Glyph", capRect);
        glyphImage = AddImage(glyphRect, TutorialUIShapes.Triangle);

        barRect = CreateChild("SpaceBar", capRect);
        barImage = AddImage(barRect, TutorialUIShapes.RoundedRect);

        RectTransform labelTransform = CreateChild("FallbackLabel", capRect);
        labelTransform.sizeDelta = new Vector2(keySize * 2f, keySize);
        fallbackLabel = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
        fallbackLabel.alignment = TextAlignmentOptions.Center;
        fallbackLabel.raycastTarget = false;
        fallbackLabel.enabled = false;

        ConfigureForKey(KeyCode.Space);
    }

    /// <summary>Reaplica cores e tamanhos. O overlay chama depois de copiar o estilo dele para cá.</summary>
    public void ApplyStyle()
    {
        EnsureBuilt();

        capImage.color = capColor;
        outlineImage.color = outlineColor;
        glyphImage.color = glyphColor;
        barImage.color = glyphColor;
        fallbackLabel.color = glyphColor;

        if (font != null)
        {
            fallbackLabel.font = font;
        }

        Color halo = haloColor;
        halo.a = 0f;
        haloImage.color = halo;

        ConfigureForKey(CurrentKey == KeyCode.None ? KeyCode.Space : CurrentKey);
    }

    private void ApplyAlpha(float value)
    {
        alpha = value;

        if (group != null)
        {
            group.alpha = value;
        }
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
