using UnityEngine;

public class LetterBoxIncinerator : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("LetterBox"))
        {
            collision.gameObject.SetActive(false);
        }
    }
}
