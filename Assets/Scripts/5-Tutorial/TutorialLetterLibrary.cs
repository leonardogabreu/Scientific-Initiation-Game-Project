using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Descobre as letras que o tutorial pode sortear a partir dos <see cref="AudioWord"/> salvos em
/// <c>Assets/Resources/AudioWords</c>.
///
/// A lista não está escrita em lugar nenhum de propósito: gravar um áudio novo, criar o AudioWord
/// e salvá-lo nessa pasta já coloca a letra no sorteio, sem mexer em código nem no Inspector. Hoje
/// existem oito (as cinco vogais mais B, C e D), e o pool cresce junto com as gravações.
///
/// Só entram assets com áudio, de uma letra só e dentro do alfabeto do jogo: as palavras inteiras
/// que moram na mesma pasta (fases de escuta) ficam de fora.
/// </summary>
public static class TutorialLetterLibrary
{
    /// <summary>Pasta dentro de Resources, sem "Assets/Resources/".</summary>
    public const string ResourcesFolder = "AudioWords";

    public const string Vowels = "AEIOU";

    public class Entry
    {
        public string letter;
        public AudioClip clip;
    }

    public static bool IsVowel(string letter)
    {
        return !string.IsNullOrEmpty(letter) && Vowels.IndexOf(letter[0]) >= 0;
    }

    /// <summary>
    /// As letras disponíveis, vogais primeiro e cada grupo em ordem alfabética. A ordem importa
    /// porque as primeiras rodadas do tutorial sorteiam só vogais.
    /// </summary>
    public static List<Entry> Load()
    {
        List<Entry> vowels = new List<Entry>();
        List<Entry> consonants = new List<Entry>();

        AudioWord[] assets = Resources.LoadAll<AudioWord>(ResourcesFolder);

        foreach (AudioWord word in assets)
        {
            if (word == null || word.audioClip == null) continue;

            if (!ChasingLettersApiManager.TryNormalizeWord(word.targetWord, out string letter)) continue;

            // Palavra inteira não serve: o tutorial é de uma letra por rodada.
            if (letter.Length != 1) continue;

            if (ChasingLettersGameManager.Alphabet.IndexOf(letter[0]) < 0) continue;

            Entry entry = new Entry { letter = letter, clip = word.audioClip };

            if (IsVowel(letter)) vowels.Add(entry);
            else consonants.Add(entry);
        }

        vowels.Sort((a, b) => string.CompareOrdinal(a.letter, b.letter));
        consonants.Sort((a, b) => string.CompareOrdinal(a.letter, b.letter));

        List<Entry> all = new List<Entry>(vowels.Count + consonants.Count);
        all.AddRange(vowels);
        all.AddRange(consonants);

        return all;
    }
}
