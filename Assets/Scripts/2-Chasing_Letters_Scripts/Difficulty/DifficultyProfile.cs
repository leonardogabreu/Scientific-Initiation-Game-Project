using UnityEngine;

/// Um conjunto de valores para todos os parâmetros de dificuldade da primeira fase 
/// O DifficultyManager parte de uma cópia de um perfil e ajusta os valores durante o jogo.
/// Pensado para ser ajustado no Inspector sem precisar editar código.

[CreateAssetMenu(fileName = "NewDifficultyProfile", menuName = "Chasing Letters/Difficulty Profile")]
public class DifficultyProfile : ScriptableObject
{
    [Header("Letter box spawner")]
    [Range(0, 100)] public int correctLetterChance = 70;     
    public float minSpawnTime = 4.5f;
    public float maxSpawnTime = 8f;
    [Header("Treadmill Behavior")]
    public float treadmillSpeed = 5f;

    [Header("PlayerController")]
    public float playerSpeed = 18f;

    [Header("Hint Display")]
    public float hintDisplayTime = 10f;
    public float hintCooldown = 20f;

    [Header("Vendo ainda, sobre a dificuldade da palavra (provavelmente vai ser no DifficultyManager)")]
    public WordDifficulty wordDifficulty = WordDifficulty.Medium;
}
