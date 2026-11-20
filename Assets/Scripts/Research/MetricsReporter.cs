using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Envoie les mesures de performance de chaque partie à une Google Sheet, via une application web Apps Script
// (Tools/GoogleSheets/ScoreReceiver.gs à la racine du projet). Sans URL, rien n'est envoyé. Une ligne par partie :
// l'entraînement (partie 0, mesure de base) part dès sa fin ; une partie mesurée part à la validation de son
// questionnaire NASA-TLX, et la réponse à la question ouverte complète ensuite sa ligne.
public class MetricsReporter : MonoBehaviour
{
    [SerializeField] private PerformanceMetrics metrics;
    // Système de distraction (mode et temps passé dans chaque palier).
    [SerializeField] private DistractionDirector distraction;
    // URL de l'application web Apps Script (se termine par /exec).
    [SerializeField] private string webAppUrl = "";
    [SerializeField] private bool sendInEditor = true;
    [SerializeField] private int timeoutSeconds = 30;

    [Header("Étude")]
    // Code du participant (vide : identifiant anonyme tiré au premier lancement sur ce poste).
    [SerializeField] private string participantId = "";
    // Condition expérimentale de la session (ex. « sans distracteur », « distracteurs visuels »).
    [SerializeField] private string condition = "";

    private const string InstallIdKey = "OEOFD.PlayerId";

    // Mesures de la partie terminée, en attente du questionnaire.
    private PerformanceReport pending;
    // Numéro de la dernière partie envoyée : la ligne que complète la question ouverte.
    private int lastSentGame;

    // Code affiché sur le questionnaire et envoyé dans la colonne « participant ».
    public string Participant => string.IsNullOrWhiteSpace(participantId) ? InstallId() : participantId;

    private void OnEnable()
    {
        metrics.Completed += OnCompleted;
    }

    private void OnDisable()
    {
        if (metrics != null)
            metrics.Completed -= OnCompleted;
    }

    // Mode Balanced : demande dès le début de la session (entraînement) le mode adaptatif ou fixe à la feuille.
    private void Start()
    {
        if (distraction != null && distraction.IsBalanced && SendAllowed)
            ModeAssignment.Request(webAppUrl, Participant);
    }

    private bool SendAllowed => !string.IsNullOrWhiteSpace(webAppUrl) && (!Application.isEditor || sendInEditor);

    private void OnCompleted(PerformanceReport report)
    {
        pending = report;
        // Entraînement : pas de questionnaire, la mesure de base part aussitôt.
        if (report.GameIndex == 0)
            Send(null);
    }

    // Envoie les mesures de la partie avec les notes du questionnaire (ordre de WorkloadForm.Dimensions).
    public void Send(int[] workload)
    {
        PerformanceReport report = pending;
        pending = null;
        if (report == null || !SendAllowed)
            return;
        lastSentGame = report.GameIndex;

        // L'ordre des champs est celui des colonnes de la feuille (la date est ajoutée par le script).
        var fields = new List<(string, object)>
        {
            ("participant", Participant),
            ("condition", condition),
            // Relie la ligne à l'onglet « Affectations » : « participant » désigne un navigateur, pas une personne.
            ("session", ModeAssignment.SessionId),
            // Champ en « _ » : lu par le script (fin de session pour l'affectation des modes), sans colonne.
            ("_derniere_partie", report.GameIndex >= report.GamesPerSession ? 1 : 0),
            ("partie", report.GameIndex),
            ("morts", report.Deaths),
            ("duree_partie_s", report.Duration),
            ("tr_mediane_ms", Round(report.ReactionMedianMs, 1)),
            ("tr_moyenne_ms", Round(report.ReactionMeanMs, 1)),
            ("tr_ecart_type_ms", Round(report.ReactionSdMs, 1)),
            ("tr_reponses", report.ReactionResponses),
            ("tr_omissions", report.ReactionOmissions),
            ("precision", Round(report.Accuracy, 3)),
            ("sorts_lances", report.SpellsCast),
            ("sorts_touches", report.SpellsHit),
            ("precision_boule_de_feu", Round(report.FireballAccuracy, 3)),
            ("precision_laser", Round(report.LaserAccuracy, 3)),
            ("attaques_ennemies", report.EnemyAttacks),
            ("attaques_evitees", report.EnemyAttacksAvoided),
            ("taux_esquive", Round(report.AvoidanceRate, 3)),
            // Score de performance et ses quatre notes (PerformanceScore).
            ("note_morts", Round(report.Score.Deaths, 3)),
            ("note_esquive", Round(report.Score.Avoidance, 3)),
            ("note_precision", Round(report.Score.Accuracy, 3)),
            ("note_reactivite", Round(report.Score.Reactivity, 3)),
            ("score_performance", Round(report.Score.Total, 1)),
        };

        // Système de distraction : mode de la partie et temps passé dans chaque palier.
        if (distraction != null)
        {
            fields.Add(("lod_mode", distraction.ModeLabel));
            // Mode Balanced : « feuille » (mode donné par la Google Sheet) ou « hasard » (sans réponse à temps).
            fields.Add(("lod_affectation", distraction.IsBalanced ? ModeAssignment.Source : ""));
            fields.Add(("lod_temps_high_s", Round(distraction.TimeIn(DistractionTier.High), 1)));
            fields.Add(("lod_temps_medium_s", Round(distraction.TimeIn(DistractionTier.Medium), 1)));
            fields.Add(("lod_temps_low_s", Round(distraction.TimeIn(DistractionTier.Low), 1)));
        }

        // Questionnaire démographique de l'écran de présentation (avant le NASA-TLX).
        fields.AddRange(ParticipantProfile.Fields());

        // NASA-TLX : notes brutes de 1 à 10, puis score global (moyenne des six, « Raw TLX »). Sur ce questionnaire,
        // une performance élevée est une réussite : elle est inversée dans le score global pour que, comme les
        // autres dimensions, une note haute traduise une charge élevée.
        float total = 0f;
        for (int i = 0; i < WorkloadForm.Dimensions.Length; i++)
        {
            int rating = workload != null && i < workload.Length ? workload[i] : 0;
            string key = WorkloadForm.Dimensions[i].Key;
            fields.Add((key, rating > 0 ? rating : (object)null));
            total += key == "tlx_performance" ? WorkloadForm.MaxRating + 1 - rating : rating;
        }
        bool complete = workload != null && workload.Length == WorkloadForm.Dimensions.Length && Array.TrueForAll(workload, r => r > 0);
        fields.Add(("tlx_global", complete ? Round(total / WorkloadForm.Dimensions.Length, 2) : null));
        // Question ouverte : colonne réservée ici, remplie ensuite par SendRemark sur la ligne de la dernière partie.
        fields.Add(("remarque_libre", null));
        fields.Add(("version", Application.version));

        SheetPost.Send(webAppUrl, ToJson(fields), $"Mesures de la partie {report.GameIndex}", timeoutSeconds);
    }

    // Complète la ligne de la dernière partie avec la réponse à la question ouverte (rien si elle est vide).
    public void SendRemark(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer) || !SendAllowed)
            return;
        var fields = new List<(string, object)>
        {
            ("session", ModeAssignment.SessionId),
            ("partie", lastSentGame),
            ("_remarque", answer),
        };
        SheetPost.Send(webAppUrl, ToJson(fields), "Remarque libre", timeoutSeconds);
    }

    private static float? Round(float? value, int decimals)
    {
        return value.HasValue ? (float)Math.Round(value.Value, decimals) : (float?)null;
    }

    // Objet JSON plat ; une valeur null (mesure non calculable) laisse la cellule vide.
    private static string ToJson(List<(string key, object value)> fields)
    {
        var json = new StringBuilder("{");
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0)
                json.Append(',');
            json.Append('"').Append(fields[i].key).Append("\":");
            switch (fields[i].value)
            {
                case null:
                    json.Append("null");
                    break;
                case string text:
                    json.Append('"');
                    // Texte libre (question ouverte) : retours à la ligne et caractères de contrôle échappés.
                    foreach (char c in text)
                    {
                        if (c == '\\' || c == '"')
                            json.Append('\\').Append(c);
                        else if (c == '\n')
                            json.Append("\\n");
                        else if (c == '\r')
                            json.Append("\\r");
                        else if (c == '\t')
                            json.Append("\\t");
                        else if (c < ' ')
                            json.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            json.Append(c);
                    }
                    json.Append('"');
                    break;
                case float number:
                    json.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                default:
                    json.Append(Convert.ToString(fields[i].value, CultureInfo.InvariantCulture));
                    break;
            }
        }
        return json.Append('}').ToString();
    }

    private static string InstallId()
    {
        string id = PlayerPrefs.GetString(InstallIdKey, "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString(InstallIdKey, id);
            PlayerPrefs.Save();
        }
        return id;
    }
}
