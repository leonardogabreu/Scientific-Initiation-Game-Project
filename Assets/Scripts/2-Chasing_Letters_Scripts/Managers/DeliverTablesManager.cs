using UnityEngine;
using TMPro;

public class DeliverTablesManager : MonoBehaviour
{
    public GameObject evenTablesParent;
    public GameObject oddTablesParent;
    private GameObject[] evenTablesList;
    private GameObject[] oddTablesList;

    void Awake()
    {
        FillTablesLists();
        ResetAllTables();

        ChasingLettersGameManager.Instance?.RegisterTables(this);
    }

    void OnEnable()
    {
        if (ChasingLettersGameManager.Instance != null)
        {
            ChasingLettersGameManager.Instance.onWordChanged += HandleWordChanged;
        }
    }

    void OnDisable()
    {
        if (ChasingLettersGameManager.Instance != null)
        {
            ChasingLettersGameManager.Instance.onWordChanged -= HandleWordChanged;
        }
    }

    private void HandleWordChanged()
    {
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
        if (ChasingLettersGameManager.Instance == null || ChasingLettersGameManager.Instance.targetWord == null) return null;

        return GetTablesListForLength(ChasingLettersGameManager.Instance.targetWord.Length);
    }

    private GameObject[] GetTablesListForLength(int wordLength)
    {
        return wordLength % 2 == 0 ? evenTablesList : oddTablesList;
    }

    /// <summary>
    /// Posição da mesa dentro da palavra da rodada (0 = primeira letra), ou -1 se a mesa não
    /// faz parte da palavra atual. É o que dá sentido à trajetória: saber que a letra foi
    /// colocada no slot errado, e não só que a entrega deu errado no fim.
    /// </summary>
    public int GetSlotIndex(GameObject table)
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null || table == null) return -1;

        int wordLength = ChasingLettersGameManager.Instance.targetWord.Length;
        if (wordLength <= 0 || wordLength > tables.Length) return -1;

        int startIndex = (tables.Length - wordLength) / 2;

        for (int i = startIndex; i < startIndex + wordLength; i++)
        {
            if (tables[i] == table) return i - startIndex;
        }

        return -1;
    }

    /// <summary>Se uma palavra desse tamanho cabe nas mesas da paridade correspondente.</summary>
    public bool CanFitWord(int wordLength)
    {
        GameObject[] tables = GetTablesListForLength(wordLength);

        return tables != null && wordLength > 0 && wordLength <= tables.Length;
    }

    public void SpawnTables()
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return;

        int wordLength = ChasingLettersGameManager.Instance.targetWord.Length;

        if (wordLength > tables.Length)
        {
            Debug.LogError($"[DeliverTablesManager] Palavra '{ChasingLettersGameManager.Instance.targetWord}' ({wordLength} letras) excede o número de mesas disponíveis ({tables.Length}).");
            return;
        }

        // Centralization
        int startIndex = (tables.Length - wordLength) / 2;

        for (int i = 0; i < wordLength; i++)
        {
            tables[startIndex + i].SetActive(true);
        }
    }

    /// <summary>A palavra montada nas mesas, ou string vazia se ainda não há mesas ativas.</summary>
    public string GetBuiltWord()
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return "";

        int wordLength = ChasingLettersGameManager.Instance.targetWord.Length;
        if (wordLength <= 0 || wordLength > tables.Length) return "";

        int startIndex = (tables.Length - wordLength) / 2;
        int limit = startIndex + wordLength;

        string builtWord = "";

        for (int i = startIndex; i < limit; i++)
        {
            GameObject letterBox = tables[i].transform.GetChild(0).gameObject;

            // Slot vazio não contribui letra nenhuma. Isto era implícito enquanto a busca era
            // GetComponentInChildren (que devolve null em objeto inativo); com o LetterText
            // serializado do InteractableCL o texto continua acessível com a caixa desligada,
            // então a checagem passou a ser explícita.
            if (!letterBox.activeInHierarchy) continue;

            InteractableCL interactableCL = letterBox.GetComponent<InteractableCL>();
            TMP_Text textComponent = interactableCL != null ? interactableCL.LetterText : null;

            if (textComponent != null)
            {
                builtWord += textComponent.text;
            }
        }

        return builtWord.ToUpper();
    }

    public bool IsLetterUsefull(InteractableCL letter, PlayerControllerCL pc)
    {
        if (letter == null) return false;

        string letterText = letter.LetterText.text;
        char letterChar = letterText[0];
        string targetWord = ChasingLettersGameManager.Instance.targetWord;
        string builtWord = GetBuiltWord();

        int countTarget = 0;
        foreach (char c in targetWord)
        {
            if (c == letterChar)
                countTarget++;
        }

        int countBuilt = 0;
        foreach (char c in builtWord)
        {
            if (c == letterChar)
                countBuilt++;
        }

        if(pc.carriedLetter == null || !pc.carriedLetter.activeInHierarchy) return countBuilt < countTarget;

        string heldLetter = pc.carriedLetterText.text;

        if(string.IsNullOrEmpty(heldLetter)) return countBuilt < countTarget;

        char heldChar = heldLetter[0];

        if(heldChar == letterChar) return countBuilt + 1 < countTarget;
        return countBuilt < countTarget;
    }

    public bool CheckSubmit()
    {
        if (ChasingLettersGameManager.Instance == null || string.IsNullOrEmpty(ChasingLettersGameManager.Instance.targetWord)) return false;

        return ChasingLettersGameManager.Instance.targetWord == GetBuiltWord();
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
