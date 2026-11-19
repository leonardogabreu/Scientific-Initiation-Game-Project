using System.Collections.Generic;
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

        return GetTablesListForLength(ChasingLettersGameManager.Instance.TargetPieces.Count);
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

        int wordLength = ChasingLettersGameManager.Instance.TargetPieces.Count;
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

        int wordLength = ChasingLettersGameManager.Instance.TargetPieces.Count;

        if (wordLength > tables.Length)
        {
            Debug.LogError($"[DeliverTablesManager] Palavra '{ChasingLettersGameManager.Instance.targetWord}' ({wordLength} peças) excede o número de mesas disponíveis ({tables.Length}).");
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
        return string.Concat(GetBuiltPieces());
    }

    public List<string> GetBuiltPieces()
    {
        List<string> builtPieces = new List<string>();

        GameObject[] tables = GetTablesListForWord();
        if (tables == null) return builtPieces;

        int wordLength = ChasingLettersGameManager.Instance.TargetPieces.Count;
        if (wordLength <= 0 || wordLength > tables.Length) return builtPieces;

        int startIndex = (tables.Length - wordLength) / 2;
        int limit = startIndex + wordLength;

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

            if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
            {
                builtPieces.Add(textComponent.text.ToUpper());
            }
        }

        return builtPieces;
    }

    // Classifica a letra que o player está segurando
    public bool IsHeldLetterUsefull (string text, PlayerControllerCL pc)
    {
        if(string.IsNullOrEmpty(text)) return false;

        bool result = IsLetterUsefull(text, pc, false);

        return result;
    }

    // Classifica letras próximas
    public bool IsNearbyLetterUsefull(InteractableCL letter, PlayerControllerCL pc)
    {
        if (letter == null || string.IsNullOrEmpty(letter.LetterText.text)) return false;

        return IsLetterUsefull(letter.LetterText.text, pc, true);
    }

    // Helper; mesma lógica para a letra segurada e para a letra próxima: só muda como pegar esses textos
    private bool IsLetterUsefull(string piece, PlayerControllerCL pc, bool discountHeldLetter)
    {
        if(pc == null) pc = FindAnyObjectByType<PlayerControllerCL>();
        if(pc == null) return false;

        piece = piece.ToUpper();

        List<string> targetPieces = ChasingLettersGameManager.Instance.TargetPieces;
        List<string> builtPieces = GetBuiltPieces();

        int countTarget = 0;
        foreach (string p in targetPieces)
        {
            if (p == piece)
                countTarget++;
        }

        int countBuilt = 0;
        foreach (string p in builtPieces)
        {
            if (p == piece)
                countBuilt++;
        }

        // Usado para desativar letras próximas se o player estiver segurando uma letra útil
        if (!discountHeldLetter) return countBuilt < countTarget;

        if(pc.carriedLetter == null || !pc.isCarrying) return countBuilt < countTarget;

        string heldLetter = pc.carriedLetterText.text;

        if(string.IsNullOrEmpty(heldLetter)) return countBuilt < countTarget;

        if(heldLetter.ToUpper() == piece) return countBuilt + 1 < countTarget;
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
