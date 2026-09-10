using UnityEngine;
using TMPro;

public class DeliverTablesManager : MonoBehaviour
{
    public GameObject evenTablesParent;
    public GameObject oddTablesParent;
    private GameObject[] evenTablesList;
    private GameObject[] oddTablesList;

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
        if (WordSelectionManager.Instance == null || WordSelectionManager.Instance.TargetWord == null) return null;

        bool isEven = WordSelectionManager.Instance.TargetWord.Length % 2 == 0;
        return isEven ? evenTablesList : oddTablesList;
    }

    public void SpawnTables()
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return;

        int wordLength = WordSelectionManager.Instance.TargetWord.Length;

        if (wordLength > tables.Length)
        {
            Debug.LogError($"[DeliverTablesManager] Palavra '{WordSelectionManager.Instance.TargetWord}' ({wordLength} letras) excede o número de mesas disponíveis ({tables.Length}).");
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

    int wordLength = WordSelectionManager.Instance.TargetWord.Length;
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

    return WordSelectionManager.Instance.TargetWord == builtWord.ToUpper();
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