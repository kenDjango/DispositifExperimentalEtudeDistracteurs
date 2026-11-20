using System;
using UnityEngine;
using UnityEngine.UI;

// Questionnaire NASA-TLX (charge de travail perçue) rempli après chaque partie : six dimensions notées de 1
// (faible) à 10 (élevée). Le bouton « Valider » n'est actif qu'une fois toutes les questions notées.
public class WorkloadForm : MonoBehaviour
{
    // Dimensions du questionnaire, dans l'ordre d'affichage ; Key est le nom de la colonne dans la Google Sheet.
    public static readonly (string Key, string Title, string Question)[] Dimensions =
    {
        ("tlx_exigence_mentale", "Exigence mentale",
            "Quelle a été l'importance de l'activité mentale et intellectuelle requise ? La tâche vous a-t-elle paru simple, nécessitant peu d'attention (faible) ou complexe, nécessitant beaucoup d'attention (élevée) ?"),
        ("tlx_exigence_physique", "Exigence physique",
            "Quelle a été l'importance de l'activité physique requise ? La tâche vous a-t-elle paru facile, peu fatigante, calme (faible) ou pénible, fatigante, active (élevée) ?"),
        ("tlx_exigence_temporelle", "Exigence temporelle",
            "Quelle a été l'importance de la pression temporelle causée par la rapidité nécessitée pour l'accomplissement de la tâche ? Était-ce un rythme lent et tranquille (faible) ou rapide et précipité (élevé) ?"),
        ("tlx_performance", "Performance",
            "Quelle réussite pensez-vous avoir eue dans l'accomplissement de votre tâche ? Comment pensez-vous avoir atteint les objectifs déterminés par la tâche ?"),
        ("tlx_effort", "Effort",
            "Quel degré d'effort avez-vous dû fournir pour exécuter la tâche demandée (mentalement et physiquement) ?"),
        ("tlx_frustration", "Frustration",
            "Pendant l'exécution de la tâche, vous êtes-vous senti satisfait, relaxé, sûr de vous (niveau de frustration faible), ou plutôt découragé, irrité, stressé, sans assurance (niveau de frustration élevé) ?"),
    };

    public const int MaxRating = 10;

    // Voile plein écran contenant la fenêtre (désactivé pendant la partie).
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private RectTransform window;
    // Une échelle par dimension, dans l'ordre de Dimensions.
    [SerializeField] private RatingScale[] scales;
    [SerializeField] private Text participantText;
    [SerializeField] private Text taskText;
    [SerializeField] private Text dateText;
    [SerializeField] private Button submitButton;
    // Rappel affiché tant que des questions restent sans note.
    [SerializeField] private Text hintText;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Textes")]
    [SerializeField] private string taskFormat = "Partie {0} sur {1}";
    // Session à une seule partie mesurée.
    [SerializeField] private string singleTask = "Partie de jeu";
    [SerializeField] private string remainingFormat = "Encore {0} question(s) sans réponse";

    private float fade = -1f;

    // Notes validées, dans l'ordre de Dimensions.
    public event Action<int[]> Submitted;

    private void Awake()
    {
        panel.gameObject.SetActive(false);
        submitButton.onClick.AddListener(Submit);
        foreach (RatingScale scale in scales)
            scale.Changed += _ => RefreshSubmit();
    }

    private void Update()
    {
        if (fade < 0f || fade >= 1f)
            return;
        fade = fadeDuration > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeDuration) : 1f;
        float k = 1f - Mathf.Pow(1f - fade, 3f);
        panel.alpha = k;
        window.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
    }

    public void Show(string participant, int gameIndex, int gameCount)
    {
        participantText.text = participant;
        taskText.text = gameCount > 1 ? string.Format(taskFormat, gameIndex, gameCount) : singleTask;
        dateText.text = DateTime.Now.ToString("dd/MM/yyyy");
        foreach (RatingScale scale in scales)
            scale.Clear();
        RefreshSubmit();

        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        fade = 0f;
    }

    private void RefreshSubmit()
    {
        int remaining = 0;
        foreach (RatingScale scale in scales)
            remaining += scale.Value == 0 ? 1 : 0;
        submitButton.interactable = remaining == 0;
        hintText.text = remaining > 0 ? string.Format(remainingFormat, remaining) : "";
    }

    private void Submit()
    {
        var ratings = new int[scales.Length];
        for (int i = 0; i < scales.Length; i++)
        {
            ratings[i] = scales[i].Value;
            if (ratings[i] == 0)
                return;
        }
        submitButton.interactable = false;
        panel.gameObject.SetActive(false);
        Submitted?.Invoke(ratings);
    }
}
