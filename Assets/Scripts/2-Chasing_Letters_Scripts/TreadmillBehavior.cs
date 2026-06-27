using UnityEngine;

public class TreadmillBehavior : MonoBehaviour
{
 
    public float treadmillSpeed = 5f;
    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("LetterBox"))
        {
            collision.gameObject.transform.Translate(Vector3.forward * Time.deltaTime * treadmillSpeed);
        }
    }
}
