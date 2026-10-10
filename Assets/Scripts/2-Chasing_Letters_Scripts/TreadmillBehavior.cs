using UnityEngine;

public class TreadmillBehavior : MonoBehaviour
{
    private const float BaselineProfileSpeed = 5f;

    private float baseSpeed = 5f;
    private float difficultyMultiplier = 1f; // Por enquanto, está como um multiplicador por conta da opção normal/reduzida no menu

    void Start()
    {
        baseSpeed = PlayerPrefs.GetFloat("treadmillSpeed", BaselineProfileSpeed);
    }

    void OnEnable()
    {
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.onDifficultyChanged += ApplyDifficultyProfile;
            ApplyDifficultyProfile(DifficultyManager.Instance.CurrentProfile);
        }
    }

    void OnDisable()
    {
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.onDifficultyChanged -= ApplyDifficultyProfile;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("LetterBox"))
        {
            float effectiveSpeed = baseSpeed * difficultyMultiplier;
            collision.gameObject.transform.Translate(Vector3.forward * Time.deltaTime * effectiveSpeed);
        }
    }

    private void ApplyDifficultyProfile(DifficultyProfile profile)
    {
        difficultyMultiplier = profile.treadmillSpeed / BaselineProfileSpeed;
    }
}
