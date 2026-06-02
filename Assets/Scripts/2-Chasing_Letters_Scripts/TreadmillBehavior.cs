using UnityEngine;

public class TreadmillBehavior : MonoBehaviour
{
 
    public float treadmillSpeed = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("LetterBox"))
        {
            collision.gameObject.transform.Translate(Vector3.forward * Time.deltaTime * treadmillSpeed);
        }
    }
}
