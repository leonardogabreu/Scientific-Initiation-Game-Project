using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public enum GroupType
{
    Luck,
    Skill,
    Memory,
    Knowledge
}

// O que é MEDIDO sobre a criança (entrada). Os parâmetros AJUSTADOS (saída) continuam no DifficultyProfile.
public enum MetricType
{
    UsefulLettersSpawned,   // Luck: Nro de letras úteis spawnadas (ao spawnar letras) -> provavelmente remover essa
    TimeToCompleteWord,     // Tudo? : Tempo até completar uma palavra -> apenas registrar por enquanto, pra ver a média de tempo de conclusão
    ReachableLetterCaught,  // Skill: letra útil ao alcance foi pega (true) ou queimou (false)
    TimeUntilHint,          // Memory: segundos até a criança pedir a dica -> registrar a partir do fim do cooldown
    NumberOfHints,          // Memory: Numero de dicas pedidas (ao finalizar uma palavra) -> remover (ou só deixar registrado),
                            //  dá pra tirar isso a partir da métrica acima. 
    MissedAttemps,          // Knowledge: Número de tentativas erradas () 
                            // filtrar, para evitar que a criança fica spammando o botão pra tentar enviar (talvez colocar um cooldown)
    RightLetterInputs        // Knowledge: Nro de letras corretas ou erradas colocadas para formar a palavra. (true é correta, false é errada)

}

[Serializable]
public class Metric // 1 métrica faz parte de 1 MetricGroup e tem 1 MetricType
{
    public MetricType metricType;
    [Tooltip("Limite de eventos a serem coletados para essa métrica")]
    public int dataResultsLimit = 5;
    [Tooltip("Meta na unidade da métrica: 0.75 = 75% de acerto, 30 = 30 segundos...")]
    public float meta = 0.7f;
    [Tooltip("Converte a unidade da métrica em tamanho de correção (ex.: 30 para segundos).")]
    public float scale = 1f;
    [Tooltip("Desmarque quando um valor menor for o desejado (ex.: número de erros).")]
    public bool higherIsBetter = true;
    [Tooltip("Zona de aceitação da accuracy - nem muito fácil, nem muito difícil")]
    public float metaDeadzone = 0.15f;
    [Tooltip("Lista de resultados dos últimos eventos capturados")]
    [NonSerialized] public List<float> dataResultsList = new();
}

[Serializable]
public class MetricGroup // 1 MetricGroup tem várias Metrics, e faz parte de um GroupType
{
    public GroupType group;
    public List<Metric> metricsList = new List<Metric>();
}

/// Mantém o DifficultyProfile ativo, recebe os dados coletados no jogo e ajusta os parâmetros de cada grupo.
public class DifficultyManager : MonoBehaviour
{
    private static DifficultyManager _instance;
    public static DifficultyManager Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindAnyObjectByType<DifficultyManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DifficultyManager");
                    _instance = go.AddComponent<DifficultyManager>();
                }
            }
            return _instance;
        }
    }

    [Tooltip("Valores iniciais dos parâmetros. Se vazio, usa Resources/DifficultyProfiles/Medium.")]
    [SerializeField] private DifficultyProfile startingProfile;

    public DifficultyProfile CurrentProfile { get; private set; }
    public event Action<DifficultyProfile> onDifficultyChanged;

    public List<MetricGroup> metricGroupsList = new List<MetricGroup>
    {
        new MetricGroup
        {
            group = GroupType.Luck,
            metricsList =
            {
                new Metric {  }
            }
        },
        new MetricGroup
        {
            group = GroupType.Skill,
            metricsList =
            {
                new Metric { metricType = MetricType.ReachableLetterCaught, dataResultsLimit = 8, meta = 0.75f }
            }
        },
        new MetricGroup
        {
            group = GroupType.Memory,
            metricsList =
            {
                new Metric { metricType = MetricType.TimeUntilHint, dataResultsLimit = 3, meta = 0.5f, scale = 60f },
                new Metric { metricType = MetricType.NumberOfHints, dataResultsLimit = 3, meta = 100f, scale = 1/30f }
            }
        },
        new MetricGroup
        {
            group = GroupType.Knowledge,
            metricsList =
            {
                new Metric { metricType = MetricType.MissedAttemps, dataResultsLimit = 5, meta = 0.6f, scale = 1f },
                new Metric { metricType = MetricType.MissedAttemps, dataResultsLimit = 5, meta = 0.6f, scale = 1f }
            }
        }
    };

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (startingProfile == null)
        {
            startingProfile = Resources.Load<DifficultyProfile>("DifficultyProfiles/Medium");
        }

        CurrentProfile = Instantiate(startingProfile);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void RegisterCollectedData(MetricType metricType, bool acertou)
    {
        RegisterCollectedData(metricType, acertou ? 1f : 0f);
    }

    public void RegisterCollectedData(MetricType metricType, int numero)
    {
        RegisterCollectedData(metricType, (float)numero);
    }

    public void RegisterCollectedData(MetricType metricType, float capturedData)
    {
        foreach (MetricGroup metricGroup in metricGroupsList)
        {
            Metric metric = metricGroup.metricsList.Find(m => m.metricType == metricType);
            if (metric == null) continue;

            metric.dataResultsList.Add(capturedData);
            if (metric.dataResultsList.Count < metric.dataResultsLimit) return;

            metric.dataResultsList.Clear();


            return;
        }

        Debug.LogWarning($"[DifficultyManager] Nenhum grupo tem a métrica {metricType}.");
    }

    private float CalculoReajuste(GroupType group)
    {
            // float correction = (metric.meta - average) / metric.scale;   // <- mudar conta para cada métrica especificamente
            // if (!metric.higherIsBetter) correction = -correction;
            // if (Mathf.Abs(correction) <= metric.metaDeadzone) return;
        float result = 0;

        switch (group)
        {
            
            case GroupType.Luck:
                // Calculo especifico para grupo
                return result;
             case GroupType.Skill:
                // Calculo especifico para grupo
                return result;
            case GroupType.Memory:
                // Calculo especifico para grupo
                return result;
            case GroupType.Knowledge:
                // Calculo especifico para grupo
                return result;
            default:
                return result;
        }   
    }
    // Usar apenas ao fim de uma palavra (submit)
    public void AdjustGroup(GroupType group, float correction)
    {
        switch (group)
        {
            case GroupType.Luck:
                //lógica do ajuste de dificuldade no grupo luck
                break;
            case GroupType.Skill:
                //lógica do ajuste de dificuldade no grupo Skill
                break;
            case GroupType.Memory:
                //lógica do ajuste de dificuldade no grupo Memory
                break;
            case GroupType.Knowledge:
                //lógica do ajuste de dificuldade no grupo Knowledge
                break;
        }
        onDifficultyChanged?.Invoke(CurrentProfile);
    }
}
