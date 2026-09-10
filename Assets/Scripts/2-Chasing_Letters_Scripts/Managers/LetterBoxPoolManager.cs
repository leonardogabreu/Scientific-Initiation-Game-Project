using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LetterBoxPool : MonoBehaviour
{
    [SerializeField] private GameObject letterBoxPrefab;
    [SerializeField] private int numberOfInstances = 15;

    private List<GameObject> letterBoxesInstances = new List<GameObject>();

    void Awake()
    {
        for (int i = 0; i < numberOfInstances; i++)
        {
            GameObject newBox = Instantiate(letterBoxPrefab, transform.position, Quaternion.identity);
            newBox.SetActive(false);
            letterBoxesInstances.Add(newBox);
        }
    }

    void OnEnable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged += HandleStateChange;
        }
    }

    void OnDisable()
    {
        if (GameCycleManager.Instance != null)
        {
            GameCycleManager.Instance.onGameStateChanged -= HandleStateChange;
        }
    }

    private void HandleStateChange(GameStateCL newState)
    {
        if (newState != GameStateCL.Playing) return;
        DeactivateAll();
    }

    public void DeactivateAll()
    {
        letterBoxesInstances.ForEach(box => box?.SetActive(false));
    }

    public GameObject GetLetterBoxInstance()
    {
        return letterBoxesInstances.FirstOrDefault(box => box != null && !box.activeInHierarchy);
    }
}