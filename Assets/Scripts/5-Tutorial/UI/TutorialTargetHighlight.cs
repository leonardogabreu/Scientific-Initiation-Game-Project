using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Destaque no mundo sobre o alvo do passo atual do tutorial: uma seta flutuando logo acima do
/// objeto e um halo em volta dele. Reage ao evento <c>onHighlightTarget</c> do
/// <see cref="TutorialDirector"/> e se esconde quando o argumento é <c>null</c>.
///
/// É montado em código, sobre um Canvas em World Space (e não com SpriteRenderer), para não
/// depender de material autorado: o projeto usa URP, e os shaders de UI vêm prontos.
///
/// A flutuação e o pulso usam <see cref="Time.unscaledDeltaTime"/>, porque o tutorial roda
/// congelado (<c>Time.timeScale = 0</c>) justamente nos passos em que o destaque precisa se mover.
/// O movimento é lento e contínuo de propósito: o público é criança com síndrome de Down, e
/// piscar rápido causa desconforto.
///
/// O <see cref="TutorialOverlayView"/> cria um destes em runtime se a cena não tiver nenhum, então
/// um GameObject com o overlay já traz a camada visual inteira.
/// </summary>
[DisallowMultipleComponent]
public class TutorialTargetHighlight : MonoBehaviour
{
    [Header("Seta")]
    public bool showArrow = true;
    [Tooltip("Lado da seta, em unidades do mundo.")]
    public float arrowSize = 0.9f;
    public Color arrowColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Altura da seta acima do topo do alvo, em unidades do mundo.")]
    public float heightAboveTarget = 0.55f;
    [Tooltip("Quanto a seta sobe e desce, em unidades do mundo.")]
    public float bobAmplitude = 0.18f;
    [Tooltip("Duração de um ciclo completo da flutuação, em segundos.")]
    public float bobDuration = 1.8f;

    [Header("Halo")]
    public bool showHalo = true;
    [Tooltip("Tamanho do halo em relação à largura do alvo.")]
    public float haloSizeMultiplier = 1.5f;
    [Tooltip("Tamanho mínimo do halo, em unidades do mundo.")]
    public float haloMinSize = 1.1f;
    public Color haloColor = new Color(1f, 0.82f, 0.25f, 0.55f);
    [Tooltip("Quanto o halo cresce no pico do pulso (0.08 = 8%).")]
    public float haloPulseScale = 0.08f;

    [Header("Geral")]
    [Tooltip("Tempo de aparecer/desaparecer do destaque.")]
    public float fadeDuration = 0.35f;
    [Tooltip("Ordem de desenho do canvas do destaque.")]
    public int sortingOrder = 400;

    private const float WorldPerPixel = 0.001f;

    private Canvas canvas;
    private RectTransform billboard;
    private CanvasGroup group;
    private RectTransform arrowRect;
    private Image arrowImage;
    private RectTransform haloRect;
    private Image haloImage;

    private bool built;
    private bool subscribed;
    private Transform target;
    private Camera targetCamera;
    private Bounds targetBounds;
    private bool hasBounds;
    private float alpha;
    private float phase;

    /// <summary>Alvo destacado agora, ou <c>null</c> quando o destaque está escondido.</summary>
    public Transform CurrentTarget => target;

    void Awake()
    {
        Build();
        ApplyStyle();
        SetAlpha(0f);
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Segunda chance de assinar: OnEnable pode rodar antes do Awake do director.
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnValidate()
    {
        if (built && Application.isPlaying)
        {
            ApplyStyle();
        }
    }

    void LateUpdate()
    {
        if (!subscribed)
        {
            // Mesma razão do overlay: a ordem de Awake entre MonoBehaviours da cena não é garantida.
            TrySubscribe();
        }

        float delta = Time.unscaledDeltaTime;

        bool wants = target != null;
        float step = fadeDuration > 0.01f ? delta / fadeDuration : 1f;
        SetAlpha(Mathf.MoveTowards(alpha, wants ? 1f : 0f, step));

        if (alpha <= 0f)
        {
            canvas.enabled = false;
            return;
        }

        canvas.enabled = true;

        if (wants)
        {
            RefreshBounds();
        }

        float cycle = Mathf.Max(0.8f, bobDuration);
        phase += delta / cycle;

        if (phase > 1f)
        {
            phase -= Mathf.Floor(phase);
        }

        // Swell suave de 0 a 1 e de volta, sem nenhum degrau.
        float swell = 0.5f * (1f - Mathf.Cos(phase * Mathf.PI * 2f));

        PlaceBillboard();
        PlaceArrow(swell);
        PlaceHalo(swell);
    }

    /// <summary>Mostra o destaque sobre o alvo. <c>null</c> esconde.</summary>
    public void SetTarget(Transform newTarget)
    {
        if (target == newTarget) return;

        target = newTarget;
        hasBounds = false;
        phase = 0f;

        if (target != null)
        {
            RefreshBounds();
            PlaceBillboard();
        }
    }

    /// <summary>Reaplica cores e tamanhos.</summary>
    public void ApplyStyle()
    {
        if (!built) return;

        canvas.sortingOrder = sortingOrder;

        arrowImage.enabled = showArrow;
        arrowImage.color = arrowColor;
        arrowRect.sizeDelta = Vector2.one * ToPixels(Mathf.Max(0.05f, arrowSize));
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, 180f);  // aponta para baixo, para o alvo

        haloImage.enabled = showHalo;
        haloImage.color = haloColor;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        TutorialDirector director = TutorialDirector.Instance;

        if (director == null) return;

        director.onHighlightTarget += SetTarget;
        director.onTutorialFinished += HandleTutorialFinished;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        TutorialDirector director = TutorialDirector.Instance;

        if (director != null)
        {
            director.onHighlightTarget -= SetTarget;
            director.onTutorialFinished -= HandleTutorialFinished;
        }

        subscribed = false;
    }

    private void HandleTutorialFinished()
    {
        SetTarget(null);
    }

    private void RefreshBounds()
    {
        if (target == null)
        {
            hasBounds = false;
            return;
        }

        // Recalculado a cada frame: a caixa de letra pode estar sobre a esteira, e a mesa pode ter
        // peças entrando e saindo, então o topo do alvo muda durante a rodada.
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool found = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (!found)
        {
            bounds = new Bounds(target.position, Vector3.one * 0.5f);
        }

        targetBounds = bounds;
        hasBounds = true;
    }

    private void PlaceBillboard()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || target == null) return;

        Vector3 center = hasBounds ? targetBounds.center : target.position;

        billboard.position = center;
        billboard.rotation = targetCamera.transform.rotation;
    }

    private void PlaceArrow(float swell)
    {
        if (!showArrow) return;

        float extentY = hasBounds ? targetBounds.extents.y : 0.25f;
        float height = extentY + Mathf.Max(0f, heightAboveTarget) + arrowSize * 0.5f + bobAmplitude * swell;

        arrowRect.anchoredPosition = new Vector2(0f, ToPixels(height));
    }

    private void PlaceHalo(float swell)
    {
        if (!showHalo) return;

        float width = hasBounds ? Mathf.Max(targetBounds.size.x, targetBounds.size.z) : haloMinSize;
        float size = Mathf.Max(haloMinSize, width * Mathf.Max(0.1f, haloSizeMultiplier));
        size *= 1f + haloPulseScale * swell;

        haloRect.sizeDelta = Vector2.one * ToPixels(size);
        haloRect.anchoredPosition = Vector2.zero;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        GameObject canvasObject = new GameObject("HighlightCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder;

        // Sem GraphicRaycaster: o destaque é informativo e não intercepta clique nenhum.
        group = canvasObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        billboard = (RectTransform)canvasObject.transform;
        billboard.sizeDelta = new Vector2(2000f, 2000f);
        billboard.localScale = Vector3.one * WorldPerPixel;

        haloRect = CreateChild("Halo", billboard);
        haloImage = AddImage(haloRect, TutorialUIShapes.Ring);

        arrowRect = CreateChild("Arrow", billboard);
        arrowImage = AddImage(arrowRect, TutorialUIShapes.Triangle);
    }

    private void SetAlpha(float value)
    {
        alpha = value;

        if (group != null)
        {
            group.alpha = value;
        }
    }

    private static float ToPixels(float worldUnits)
    {
        return worldUnits / WorldPerPixel;
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
