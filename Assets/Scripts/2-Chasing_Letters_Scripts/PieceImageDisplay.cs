using UnityEngine;
using TMPro;

public class PieceImageDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text pieceText;
    [SerializeField] private SpriteRenderer pieceImage;

    [Header("Face")]
    [SerializeField] private float faceHeight = 0.5f;
    [SerializeField] private float faceForward = 0f;
    [SerializeField] private float imageSize = 1f;

    [Header("Letter Background")]
    [SerializeField] private bool showLetterBackground = true;
    [SerializeField] private Color letterBackgroundColor = Color.black;
    [SerializeField] private float letterBackgroundPadding = 0.15f;

    private const float LetterBackgroundDepth = 0.05f;
    private static Sprite letterBackgroundSprite;

    private SpriteRenderer letterBackground;
    private Renderer boxRenderer;
    private Camera mainCamera;
    private string shownPiece;

    void Awake()
    {
        boxRenderer = GetComponent<Renderer>();
    }

    void LateUpdate()
    {
        if (pieceText == null) return;

        UpdateImage();
        PlaceFace();
    }

    private void UpdateImage()
    {
        if (pieceImage == null || pieceText.text == shownPiece) return;

        shownPiece = pieceText.text;

        Sprite sprite = ChasingLettersGameManager.Instance != null ? ChasingLettersGameManager.Instance.GetPieceImage(shownPiece) : null;

        pieceImage.sprite = sprite;
        pieceImage.enabled = sprite != null;
        pieceText.enabled = sprite == null;
    }

    private void PlaceFace()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 facePosition = transform.position + Vector3.up * faceHeight;

        if (boxRenderer != null)
        {
            Bounds bounds = boxRenderer.bounds;
            facePosition = bounds.center + Vector3.up * (bounds.extents.y + faceHeight);
        }

        Vector3 towardCamera = -Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        facePosition += towardCamera * faceForward;

        Quaternion faceRotation = mainCamera.transform.rotation;
        Vector3 faceUp = faceRotation * Vector3.up;

        pieceText.transform.rotation = faceRotation;

        Bounds textBounds = pieceText.textBounds;
        bool hasVisibleText = pieceText.enabled && textBounds.extents.y > 0f;

        if (hasVisibleText)
        {
            float textHalfHeight = textBounds.extents.y * pieceText.transform.lossyScale.y;
            pieceText.transform.position = facePosition + faceUp * textHalfHeight - pieceText.transform.TransformVector(textBounds.center);
        }
        else
        {
            pieceText.transform.position = facePosition;
        }

        PlaceLetterBackground(hasVisibleText, textBounds, faceRotation);

        if (pieceImage == null || pieceImage.sprite == null) return;

        pieceImage.transform.SetPositionAndRotation(facePosition + faceUp * (imageSize / 2f), faceRotation);

        Vector3 spriteSize = pieceImage.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        float parentScale = pieceImage.transform.parent != null ? pieceImage.transform.parent.lossyScale.x : 1f;

        pieceImage.transform.localScale = Vector3.one * (imageSize / (largestSide * parentScale));
    }

    private void PlaceLetterBackground(bool hasVisibleText, Bounds textBounds, Quaternion faceRotation)
    {
        if (!showLetterBackground || !hasVisibleText)
        {
            if (letterBackground != null) letterBackground.enabled = false;
            return;
        }

        if (letterBackground == null) CreateLetterBackground();

        Vector3 textScale = pieceText.transform.lossyScale;
        float side = Mathf.Max(textBounds.size.x * textScale.x, textBounds.size.y * textScale.y) + letterBackgroundPadding * 2f;
        Vector3 textCenter = pieceText.transform.TransformPoint(textBounds.center);

        letterBackground.enabled = true;
        letterBackground.color = letterBackgroundColor;
        letterBackground.transform.SetPositionAndRotation(textCenter + mainCamera.transform.forward * LetterBackgroundDepth, faceRotation);
        letterBackground.transform.localScale = Vector3.one * (side / transform.lossyScale.x);
    }

    private void CreateLetterBackground()
    {
        if (letterBackgroundSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            letterBackgroundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        }

        GameObject backgroundObject = new GameObject("LetterBackground");
        backgroundObject.transform.SetParent(transform, false);

        letterBackground = backgroundObject.AddComponent<SpriteRenderer>();
        letterBackground.sprite = letterBackgroundSprite;

        Renderer textRenderer = pieceText.GetComponent<Renderer>();

        if (textRenderer != null)
        {
            letterBackground.sortingLayerID = textRenderer.sortingLayerID;
            letterBackground.sortingOrder = textRenderer.sortingOrder - 1;
        }

        if (pieceImage != null)
        {
            letterBackground.sharedMaterial = pieceImage.sharedMaterial;
        }
    }
}
