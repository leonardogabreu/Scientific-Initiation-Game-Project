using UnityEngine;
using TMPro;

public class DeliverTablesManager : MonoBehaviour
{
    public GameObject evenTablesParent;
    public GameObject oddTablesParent;
    private GameObject[] evenTablesList;
    private GameObject[] oddTablesList;

    [SerializeField] private ChasingLettersGameManager gameManager;

    void Start()
    {
        FillTablesLists();
        ResetAllTables();
        SpawnTables();
    }

    private void FillTablesLists()
    {
        evenTablesList = BuildTablesList(evenTablesParent);
        oddTablesList = BuildTablesList(oddTablesParent);
    }

    private GameObject[] BuildTablesList(GameObject parent)
    {
        if (parent == null) return null;

        int count = parent.transform.childCount;
        GameObject[] tables = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            tables[i] = parent.transform.GetChild(i).gameObject;
            tables[i].SetActive(false);
        }

        return tables;
    }

    // Decide qual lista (par/ímpar) usar com base no tamanho da palavra atual.
    private GameObject[] GetTablesListForWord()
    {
        if (gameManager == null || gameManager.targetWord == null) return null;

        bool isEven = gameManager.targetWord.Length % 2 == 0;
        return isEven ? evenTablesList : oddTablesList;
    }

    public void SpawnTables()
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return;

        int wordLength = gameManager.targetWord.Length;

        if (wordLength > tables.Length)
        {
            Debug.LogError($"[DeliverTablesManager] Palavra '{gameManager.targetWord}' ({wordLength} letras) excede o número de mesas disponíveis ({tables.Length}).");
            return;
        }

        // Centralization
        int startIndex = (tables.Length - wordLength) / 2;

        for (int i = 0; i < wordLength; i++)
        {
            tables[startIndex + i].SetActive(true);
        }
    }

    public bool CheckSubmit()
{
    GameObject[] tables = GetTablesListForWord();
    if (tables == null) return false;

    int wordLength = gameManager.targetWord.Length;
    int startIndex = (tables.Length - wordLength) / 2;
    int limit = startIndex + wordLength;

    string builtWord = "";

    for (int i = startIndex; i < limit; i++)
    {
        GameObject letterBox = tables[i].transform.GetChild(0).gameObject;
        InteractableCL interactableCL = letterBox.GetComponent<InteractableCL>();
        TMP_Text textComponent = interactableCL != null ? interactableCL.LetterText : null;

        if (textComponent != null)
        {
            builtWord += textComponent.text;
        }
    }

    return gameManager.targetWord == builtWord.ToUpper();
}

    public void ResetAllTables()
    {
        ResetTablesList(evenTablesList);
        ResetTablesList(oddTablesList);
    }

    private void ResetTablesList(GameObject[] tables)
    {
        if (tables == null) return;

        foreach (GameObject table in tables)
        {
            if (table == null) continue;

            if (table.transform.childCount > 0)
            {
                table.transform.GetChild(0).gameObject.SetActive(false);
            }

            table.SetActive(false);
        }
    }
}