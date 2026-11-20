using System;
using UnityEngine;
using UnityEngine.Networking;

// Affectation de la session au mode adaptatif ou fixe (mode Balanced du DistractionDirector). La Google Sheet
// (Tools/GoogleSheets/ScoreReceiver.gs) donne le mode qui compte le moins de sessions, pour garder 50 % / 50 %.
// Demandée au début de la session (partie d'entraînement) ; sans réponse au début de la première partie mesurée
// (pas de réseau, pas d'URL), le mode est tiré au hasard. Le mode reste le même pour toute la session.
public static class ModeAssignment
{
    // Identifiant de la session, envoyé avec la demande et avec chaque ligne de mesures.
    public static string SessionId { get; private set; }
    // Mode donné par la feuille ou tiré au hasard ; null tant qu'il n'est pas décidé.
    public static bool? Adaptive { get; private set; }
    // Origine du mode : « feuille » ou « hasard » (vide tant qu'il n'est pas décidé).
    public static string Source { get; private set; } = "";

    private static bool requested;

    // Sans rechargement du domaine à l'entrée en Play mode, les statiques survivent d'une session à l'autre.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        Adaptive = null;
        Source = "";
        requested = false;
    }

    // Demande le mode à la feuille (une seule fois par session).
    public static void Request(string webAppUrl, string participant)
    {
        if (requested || string.IsNullOrWhiteSpace(webAppUrl))
            return;
        requested = true;
        string url = $"{webAppUrl}?action=assign&session={UnityWebRequest.EscapeURL(SessionId)}&participant={UnityWebRequest.EscapeURL(participant)}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 15;
        request.SendWebRequest().completed += _ =>
        {
            string text = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : "";
            // Réponse arrivée trop tard : le mode a déjà été tiré au hasard, il ne change plus.
            if (Adaptive == null && (text.Contains("\"mode\":\"adaptatif\"") || text.Contains("\"mode\":\"fixe\"")))
            {
                Adaptive = text.Contains("\"mode\":\"adaptatif\"");
                Source = "feuille";
                Debug.Log($"Distraction : la Google Sheet affecte la session au mode {(Adaptive.Value ? "adaptatif" : "fixe")} ({text}).");
            }
            else if (Adaptive == null)
            {
                Debug.LogWarning($"Distraction : pas d'affectation par la Google Sheet ({request.responseCode}) : {request.error} {text}");
            }
            request.Dispose();
        };
    }

    // Mode de la session ; tiré au hasard s'il n'est pas encore connu.
    public static bool Resolve()
    {
        if (Adaptive == null)
        {
            Adaptive = UnityEngine.Random.value < 0.5f;
            Source = "hasard";
            Debug.Log($"Distraction : pas de réponse de la Google Sheet, mode {(Adaptive.Value ? "adaptatif" : "fixe")} tiré au hasard.");
        }
        return Adaptive.Value;
    }
}
