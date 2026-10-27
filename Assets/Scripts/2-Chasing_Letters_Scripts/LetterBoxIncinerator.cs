using UnityEngine;

public class LetterBoxIncinerator : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("LetterBox"))
        {
            LetterStatus letterStatus = collision.gameObject.GetComponent<LetterStatus>();
            if(letterStatus != null)
            {
                letterStatus.NotifyLetterDisabled(false);
            }

            collision.gameObject.SetActive(false);

        }
    }
}
