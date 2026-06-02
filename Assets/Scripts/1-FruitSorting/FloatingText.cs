using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float fadeSpeed = 1.0f;
    
    private TextMeshPro textMesh;
    private Color textColor;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        textColor = textMesh.color;
    }

    public void Setup(int scoreValue)
    {
        if (scoreValue > 0)
        {
            textMesh.text = "+" + scoreValue;
            textMesh.color = Color.green;
        }
        else
        {
            textMesh.text = scoreValue.ToString();
            textMesh.color = Color.red;
        }
        textColor = textMesh.color;
    }

    void Update()
    {
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // Lowers transparency
        textColor.a -= fadeSpeed * Time.deltaTime;
        textMesh.color = textColor;

        if (textColor.a <= 0)
        {
            Destroy(gameObject);
        }
    }
}