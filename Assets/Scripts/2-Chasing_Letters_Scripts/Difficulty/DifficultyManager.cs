using System;
using System.Collections.Generic;
using System.Linq;
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
    public int minDataResultsAmount = 3;
    [Tooltip("Meta na unidade da métrica: 0.75 = 75% de acerto, 30 = 30 segundos... Não pode ser 0.")]
    public float meta = 0.7f;
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

    public static bool HasInstance => _instance != null;

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

    private const int minCorrectLetterChance = 40;
    private const int maxCorrectLetterChance = 90;
    private const float minMinSpawnTime = 2.5f;
    private const float maxMinSpawnTime = 6f;
    private const float minMaxSpawnTime = 5f;
    private const float maxMaxSpawnTime = 10f;
    private const int minLetterPoolSize = 10;
    private const int maxLetterPoolSize = 20;
    private const float minTreadmillSpeed = 3f;
    private const float maxTreadmillSpeed = 8f;
    private const float minPlayerSpeed = 12f;
    private const float maxPlayerSpeed = 26f;
    private const float minHintShowTime = 5f;
    private const float maxHintShowTime = 15f;
    private const float minHintCooldown = 10f;
    private const float maxHintCooldown = 35f;

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
                new Metric { metricType = MetricType.ReachableLetterCaught, meta = 0.75f } // 75% das letras pêgas
            }
        },
        new MetricGroup
        {
            group = GroupType.Memory,
            metricsList =
            {
                new Metric { metricType = MetricType.TimeUntilHint, minDataResultsAmount = 5, meta = 60f }, // 60segs até pedir nova dica
                new Metric { metricType = MetricType.NumberOfHints, minDataResultsAmount = 5, meta = 3f, higherIsBetter = false }   // 3 dicas por palavra
            }
        },
        new MetricGroup
        {
            group = GroupType.Knowledge,
            metricsList =
            {
                new Metric { metricType = MetricType.MissedAttemps, minDataResultsAmount = 1, meta = 2f, higherIsBetter = false }, // deu submit errado no max 2x (filtrar)
                new Metric { metricType = MetricType.RightLetterInputs, meta = 0.6f } // meta: colocar 60% das letras no lugar correto
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

    // Chamado para registrar um evento coletado. Interface da classe
    public void RegisterCollectedData(MetricType metricType, float capturedData)
    {
        foreach (MetricGroup metricGroup in metricGroupsList)
        {
            Metric metric = metricGroup.metricsList.Find(m => m.metricType == metricType);
            if (metric == null) continue;

            metric.dataResultsList.Add(capturedData);
            return;
        }

        Debug.LogWarning($"[DifficultyManager] Nenhum grupo tem a métrica {metricType}.");
    }

    // Faz a média dos resultados obtidos para todos os eventos coletados na lista de cada métrica,
    // e depois faz a média disso, para descobrir uma porcentagem de correção para os parâmetros de um grupo de métricas.
    private float GetCorrection(GroupType group)
    {
        List<float> results = new List<float>();
        foreach(Metric metric in metricGroupsList[(int)group].metricsList)
        {
            if(metric.dataResultsList.Count >= metric.minDataResultsAmount)
            {
                float correction = (metric.meta - metric.dataResultsList.Average()) / metric.meta;
                if (!metric.higherIsBetter) correction = -correction;
                if (Mathf.Abs(correction) <= metric.metaDeadzone) correction = 0;
                results.Add(correction);
                metric.dataResultsList.Clear();
            }
        }

        if (results.Count == 0) return 0;
        return results.Average();
    }

    // Helper
    private float ParamClamp(float param, float min, float max)
    {
        return Mathf.Clamp(param, min, max);
    }

    // Ajusta cada parâmetro individualmente
    private void AdjustParameter(string nomeDoParam, float min, float max, float correction, bool higherIsHarder, ref float param)
    {
        Debug.Log($"[DifficultyManager]: Previous {nomeDoParam} = {param}.");
                
        float delta = 0.5f * correction * (max - min);
        param = higherIsHarder ? ParamClamp(param - delta, min, max) : ParamClamp(param + delta, min, max);

        Debug.Log($"[DifficultyManager]: Correction = {correction}; Delta = {delta}; New {nomeDoParam} Value = {param}");
    }

    // Usado pelo DinamicDifficultyAdjutments (abaixo)
    private void AdjustGroup(GroupType group)
    {
        MetricGroup metricGroup = metricGroupsList[(int)group];
        float correction = GetCorrection(group);
        if (correction == 0) return;

        switch (group)
        {
            case GroupType.Luck:
                //lógica do ajuste de dificuldade no grupo luck
                break;
            case GroupType.Skill:
                AdjustParameter("treadmillSpeed", minTreadmillSpeed, maxTreadmillSpeed, correction, true, ref CurrentProfile.treadmillSpeed);
                
                break;
            case GroupType.Memory:
                //lógica do ajuste de dificuldade no grupo Memory
                break;
            case GroupType.Knowledge:
                //lógica do ajuste de dificuldade no grupo Knowledge
                break;
        }
    }

    public void DynamicDifficultyAdjustments()      // Chamado uma vez, ao acertar uma palavra
    {
        foreach(MetricGroup metricGroup in metricGroupsList)
        {
            AdjustGroup(metricGroup.group);
        }
        onDifficultyChanged?.Invoke(CurrentProfile);
    }
}
