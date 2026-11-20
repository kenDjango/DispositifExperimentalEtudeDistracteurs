using System.Collections.Generic;
using UnityEngine;

// Questionnaire démographique rempli sur l'écran de présentation de l'étude, et réponses du participant, gardées
// pour toute la session (les scènes se succèdent) et jointes à chaque ligne envoyée à la Google Sheet.
public static class ParticipantProfile
{
    public class Question
    {
        // Nom de la colonne dans la Google Sheet.
        public string Key;
        public string Title;
        // Précision affichée sous l'intitulé (peut être vide).
        public string Detail;
        public string[] Options;
        // Échelle numérique : libellés des extrémités, et la note (1 à N) est envoyée au lieu du libellé.
        public string Low;
        public string High;

        public bool IsScale => Low != null;
    }

    // Questions dans l'ordre d'affichage (et des colonnes).
    public static readonly Question[] Questions =
    {
        new Question
        {
            Key = "age", Title = "Âge",
            Options = new[] { "Moins de 18 ans", "18 à 24 ans", "25 à 34 ans", "35 à 44 ans", "45 à 54 ans", "55 ans et +" },
        },
        new Question
        {
            Key = "temps_jeu_semaine", Title = "Temps de jeu vidéo par semaine",
            Options = new[] { "Moins de 2 h", "2 à 5 h", "5 à 10 h", "10 à 20 h", "Plus de 20 h" },
        },
        new Question
        {
            Key = "experience_moba", Title = "Expérience des MOBA", Detail = "League of Legends, Dota 2…",
            Options = new[] { "1", "2", "3", "4", "5" }, Low = "Aucune", High = "Très grande",
        },
        new Question
        {
            Key = "experience_hack_n_slash", Title = "Expérience des hack'n'slash", Detail = "Diablo, Path of Exile…",
            Options = new[] { "1", "2", "3", "4", "5" }, Low = "Aucune", High = "Très grande",
        },
        new Question
        {
            Key = "daltonien", Title = "Êtes-vous daltonien ?",
            Options = new[] { "Oui", "Non" },
        },
    };

    // Choix de chaque question (1 à N, 0 : pas de réponse), dans l'ordre de Questions.
    private static int[] answers;
    private static bool consent;

    public static bool IsComplete => answers != null && consent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        answers = null;
        consent = false;
    }

    public static void Set(int[] choices, bool consented)
    {
        answers = (int[])choices.Clone();
        consent = consented;
    }

    // Colonnes envoyées (cellules vides si le questionnaire n'a pas été rempli, ex. partie lancée dans l'éditeur).
    public static IEnumerable<(string, object)> Fields()
    {
        for (int i = 0; i < Questions.Length; i++)
        {
            Question q = Questions[i];
            int choice = answers != null && i < answers.Length ? answers[i] : 0;
            object value = choice <= 0 ? null : q.IsScale ? choice : (object)q.Options[choice - 1];
            yield return (q.Key, value);
        }
        yield return ("consentement", consent ? "oui" : null);
    }
}
