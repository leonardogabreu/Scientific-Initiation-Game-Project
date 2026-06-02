using Mono.Cecil;
using UnityEngine;

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
        resetAllTables();
        spawnTables();
    }

    private void FillTablesLists()
    {
        if (evenTablesParent != null)
        {
            int evenCount = evenTablesParent.transform.childCount;
            evenTablesList = new GameObject[evenCount]; 
            
            for (int i = 0; i < evenCount; i++)
            {
                evenTablesList[i] = evenTablesParent.transform.GetChild(i).gameObject;
                evenTablesList[i].SetActive(false);
            }
        }

        if (oddTablesParent != null)
        {
            int oddCount = oddTablesParent.transform.childCount;
            oddTablesList = new GameObject[oddCount]; 
            
            for (int i = 0; i < oddCount; i++)
            {
                oddTablesList[i] = oddTablesParent.transform.GetChild(i).gameObject;
                oddTablesList[i].SetActive(false);
            }
        }
    }

    public void spawnTables()
    {
        if (gameManager != null && gameManager.targetWord != null)
        {
            int wordLength = gameManager.targetWord.Length;

            if (wordLength % 2 == 0)
            {
                if (evenTablesList != null)
                {
                    // Matemática da centralização: (TamanhoTotal - TamanhoPalavra) / 2
                    int startIndex = (evenTablesList.Length - wordLength) / 2;
                    
                    for (int i = 0; i < wordLength; i++)
                    {
                        evenTablesList[startIndex + i].SetActive(true);
                    }
                }
            }
            else
            {
                if (oddTablesList != null)
                {
                    // Matemática da centralização: (TamanhoTotal - TamanhoPalavra) / 2
                    int startIndex = (oddTablesList.Length - wordLength) / 2;
                    
                    for (int i = 0; i < wordLength; i++)
                    {
                        oddTablesList[startIndex + i].SetActive(true);
                    }
                }
            }
        }
    }

    public bool checkSubmit()
    {
        if (evenTablesList != null && oddTablesList != null)
        {
            int wordLength = gameManager.targetWord.Length;
            string builtWord = "";
            if(wordLength % 2 == 0)
            {
                int index = (evenTablesList.Length - wordLength) / 2; // Gets the start index
                int limit = index + wordLength;
                TMPro.TMP_Text textComponent;
                
                for(; index < limit; index++)
                {
                    textComponent = evenTablesList[index].transform.GetChild(0).GetComponentInChildren<TMPro.TMP_Text>();

                    if (textComponent != null)
                    {
                        builtWord += textComponent.text;
                    }
                }
                if(gameManager.targetWord == builtWord.ToUpper()) return true;
                else return false;
            }
            else
            {
                int index = (oddTablesList.Length - wordLength) / 2; // Gets the start index
                int limit = index + wordLength;
                TMPro.TMP_Text textComponent;
                
                for(; index < limit; index++)
                {
                    textComponent = oddTablesList[index].transform.GetChild(0).GetComponentInChildren<TMPro.TMP_Text>();

                    if (textComponent != null)
                    {
                        builtWord += textComponent.text;
                    }
                }

                if(gameManager.targetWord == builtWord.ToUpper()) return true; // Podemos usar a palavra que a criança tentou colocar como um dado pra IA do Mateus
                else return false;
            }
        }
        return false;
    }

    public void resetAllTables()
    {
        // Limpa e oculta todo o vetor PAR
        if (evenTablesList != null)
        {
            for (int i = 0; i < evenTablesList.Length; i++)
            {
                if (evenTablesList[i] != null)
                {
                    // 1. Desativa a letra (filho)
                    GameObject letterObj = evenTablesList[i].transform.GetChild(0).gameObject;
                    if (letterObj != null)
                    {
                        letterObj.SetActive(false);
                    }

                    // 2. Oculta a mesa (pai)
                    evenTablesList[i].SetActive(false);
                }
            }
        }

        // Limpa e oculta todo o vetor ÍMPAR
        if (oddTablesList != null)
        {
            for (int i = 0; i < oddTablesList.Length; i++)
            {
                if (oddTablesList[i] != null)
                {
                    // 1. Desativa a letra (filho)
                    GameObject letterObj = oddTablesList[i].transform.GetChild(0).gameObject;
                    if (letterObj != null)
                    {
                        letterObj.SetActive(false);
                    }

                    // 2. Oculta a mesa (pai)
                    oddTablesList[i].SetActive(false);
                }
            }
        }
    }
}