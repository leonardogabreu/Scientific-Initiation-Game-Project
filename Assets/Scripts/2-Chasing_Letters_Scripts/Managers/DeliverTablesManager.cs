using UnityEngine;
using TMPro;

public class DeliverTablesManager : MonoBehaviour
{
    public GameObject evenTablesParent;
    public GameObject oddTablesParent;
    private GameObject[] evenTablesList;
    private GameObject[] oddTablesList;

    [SerializeField] private ChasingLettersGameManager gameManager;

    void Awake()
    {
        // Antes de qualquer Start: a escolha de palavra precisa saber quantas mesas existem
        // para descartar palavras da plataforma que não caberiam.
        FillTablesLists();
        ResetAllTables();

        gameManager?.RegisterTables(this);
    }

    void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.onWordChanged += HandleWordChanged;
        }
    }

    void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.onWordChanged -= HandleWordChanged;
        }
    }

    // As mesas só são montadas quando a palavra da rodada existe de fato. Montar antes disso
    // deixava o número de mesas fora de sincronia com a palavra que chegava da API depois.
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
        if (gameManager == null || gameManager.targetWord == null) return null;

        return GetTablesListForLength(gameManager.targetWord.Length);
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

        int wordLength = gameManager.targetWord.Length;
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

    /// <summary>A palavra montada nas mesas, ou string vazia se ainda não há mesas ativas.</summary>
    public string GetBuiltWord()
    {
        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return "";

        int wordLength = gameManager.targetWord.Length;
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

    public bool CheckSubmit()
    {
        if (gameManager == null || string.IsNullOrEmpty(gameManager.targetWord)) return false;

        return gameManager.targetWord == GetBuiltWord();
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
