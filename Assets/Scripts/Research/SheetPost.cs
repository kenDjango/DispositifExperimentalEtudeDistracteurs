using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// File d'envoi vers la Google Sheet : les envois partent un par un, dans l'ordre (la remarque libre après la ligne
// qu'elle complète), avec de nouvelles tentatives en cas d'échec. Elle survit au rechargement de la scène. Le script
// de la feuille ignore une ligne déjà reçue : une tentative répétée ne crée pas de doublon.
public static class SheetPost
{
    // Attente avant chaque nouvelle tentative (secondes, temps réel : le jeu est à l'arrêt en fin de partie).
    private static readonly float[] RetryDelays = { 2f, 5f, 15f };

    private static readonly Queue<(string url, string json, string label, int timeout)> queue = new Queue<(string, string, string, int)>();
    private static Runner runner;
    private static bool draining;

    // Sans rechargement du domaine à l'entrée en Play mode, les statiques survivent d'une session à l'autre.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        queue.Clear();
        runner = null;
        draining = false;
    }

    public static void Send(string url, string json, string label, int timeoutSeconds)
    {
        queue.Enqueue((url, json, label, timeoutSeconds));
        if (draining)
            return;
        if (runner == null)
        {
            var host = new GameObject("SheetPost") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(host);
            runner = host.AddComponent<Runner>();
        }
        draining = true;
        runner.StartCoroutine(Drain());
    }

    private static IEnumerator Drain()
    {
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            for (int attempt = 0; ; attempt++)
            {
                // text/plain : évite la requête de vérification CORS en WebGL (Apps Script lit le corps tel quel).
                var request = new UnityWebRequest(item.url, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(item.json)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = item.timeout,
                };
#if !UNITY_WEBGL || UNITY_EDITOR
                // Apps Script enregistre la ligne puis répond par une redirection vers sa réponse : inutile de la
                // suivre hors navigateur (la renvoyer en POST échouerait). En WebGL, il faut au contraire la laisser
                // suivre : une limite de 0 y devient fetch(redirect: "error"), un échec alors que la ligne est écrite.
                request.redirectLimit = 0;
#endif
                request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
                yield return request.SendWebRequest();

                // Apps Script ne redirige qu'après une exécution réussie ; en cas d'erreur du script, il répond 200
                // avec une page d'erreur (seule la réponse du script contient "ok").
                long code = request.responseCode;
                bool sent = code == 302 || (code == 200 && request.downloadHandler.text.Contains("\"ok\":true"));
                string detail = $"({code}) : {request.error} {request.downloadHandler.text}";
                request.Dispose();

                if (sent)
                {
                    Debug.Log($"{item.label} : envoi à la Google Sheet réussi (réponse {code}).");
                    break;
                }
                if (attempt >= RetryDelays.Length)
                {
                    Debug.LogWarning($"{item.label} : échec de l'envoi à la Google Sheet, abandon après {attempt + 1} tentatives {detail}");
                    break;
                }
                Debug.LogWarning($"{item.label} : échec de l'envoi {detail}, nouvelle tentative dans {RetryDelays[attempt]:0} s.");
                yield return new WaitForSecondsRealtime(RetryDelays[attempt]);
            }
        }
        draining = false;
    }

    // Porte les coroutines, hors de la scène de jeu.
    private class Runner : MonoBehaviour
    {
    }
}
