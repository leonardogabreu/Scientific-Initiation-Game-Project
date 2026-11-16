using UnityEngine;
using TMPro;

public class PieceImageDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text pieceText;
    [SerializeField] private SpriteRenderer pieceImage;

    private string shownPiece;

    void LateUpdate()
    {
        if (pieceText == null || pieceImage == null) return;

        if (pieceText.text == shownPiece) return;

        shownPiece = pieceText.text;

        Sprite sprite = ChasingLettersGameManager.Instance != null ? ChasingLettersGameManager.Instance.GetPieceImage(shownPiece) : null;

        pieceImage.sprite = sprite;
        pieceImage.enabled = sprite != null;
        pieceText.enabled = sprite == null;
    }
}
