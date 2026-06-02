using UnityEngine;
using TMPro;

public class FloatingFrameRotation : MonoBehaviour
{
    public TextMeshPro letterText; 
    
    private float switchRotationTimer = 0f;
    public float velocidadeAsa = 0.2f;

    void Update()
    {
        switchRotationTimer += Time.deltaTime;
        if (switchRotationTimer >= velocidadeAsa)
        {
            // Creates the impression of flapping wings
            transform.Rotate(180, 0, 0);
            switchRotationTimer = 0f; 
        }

        if (letterText != null)
        {
            // Makes the letter always face the camera
            letterText.transform.rotation = Camera.main.transform.rotation;
        }
    }
}