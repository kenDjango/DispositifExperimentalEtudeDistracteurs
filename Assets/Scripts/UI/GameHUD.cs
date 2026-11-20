using UnityEngine;
using UnityEngine.UI;

// Haut de l'écran : décompte du temps restant et score en cours (et bandeau pendant la partie d'entraînement),
// et, en option, le score de performance de l'étude en temps réel.
public class GameHUD : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private Text timerText;
    [SerializeField] private Text scoreText;
    // Affiché seulement pendant l'entraînement.
    [SerializeField] private GameObject practiceBadge;

    [Header("Score de performance")]
    // Affiche le score de performance de l'étude en temps réel, dans l'éditeur seulement : jamais dans le jeu
    // exporté (il ne doit pas influencer le participant), même coché. Modifiable en cours de partie.
    [SerializeField] private bool showPerformanceScore;
    [SerializeField] private PerformanceMetrics metrics;
    [SerializeField] private GameObject performancePanel;
    [SerializeField] private Text performanceText;
    [SerializeField] private string performanceFormat = "Performance  {0:0.0}\n<size=17>Morts {1:0.00}   Esquive {2:0.00}   Précision {3:0.00}   Réactivité {4:0.00}</size>";
    // Système de distraction : palier actif et, en mode adaptatif, score de la fenêtre glissante et état de la décision.
    [SerializeField] private DistractionDirector distraction;
    [SerializeField] private string scoreFormat = "Score  {0}";
    [SerializeField] private Color normalColor = Color.white;

    [Header("Fin proche")]
    // En dessous de ce temps restant, le chrono passe à la couleur d'alerte et pulse à chaque seconde.
    [SerializeField] private float warningTime = 30f;
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.25f);
    [SerializeField] private float warningPulse = 0.18f;

    [Header("Gain de points")]
    [SerializeField] private float punchScale = 1.25f;
    [SerializeField] private float punchDuration = 0.25f;
    [SerializeField] private Color gainColor = new Color(1f, 0.85f, 0.4f);

    private float punchTimer;

    private void Start()
    {
        if (practiceBadge != null)
            practiceBadge.SetActive(game.IsPractice);
        game.ScoreGained += OnScoreGained;
        Refresh();
    }

    private void OnDestroy()
    {
        if (game != null)
            game.ScoreGained -= OnScoreGained;
    }

    private void OnScoreGained(int points)
    {
        punchTimer = punchDuration;
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        float remaining = game.TimeRemaining;
        int seconds = Mathf.CeilToInt(remaining);
        timerText.text = $"{seconds / 60}:{seconds % 60:00}";

        // Pulsation qui démarre à chaque changement de seconde puis retombe.
        bool warning = remaining <= warningTime && !game.IsOver;
        float tick = remaining - Mathf.Floor(remaining);
        timerText.color = warning ? warningColor : normalColor;
        timerText.transform.localScale = Vector3.one * (warning ? 1f + warningPulse * tick * tick : 1f);

        scoreText.text = string.Format(scoreFormat, game.Score);
        punchTimer = Mathf.Max(0f, punchTimer - Time.unscaledDeltaTime);
        float k = punchDuration > 0f ? punchTimer / punchDuration : 0f;
        scoreText.transform.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, k);
        scoreText.color = Color.Lerp(normalColor, gainColor, k);

        bool showPerformance = showPerformanceScore && Application.isEditor && metrics != null && performancePanel != null;
        if (performancePanel != null && performancePanel.activeSelf != showPerformance)
            performancePanel.SetActive(showPerformance);
        if (showPerformance)
        {
            PerformanceScore p = metrics.Live;
            string text = string.Format(performanceFormat, p.Total, p.Deaths, p.Avoidance, p.Accuracy, p.Reactivity);
            if (distraction != null)
                foreach (string line in DistractionLine().Split('\n'))
                    // Retour à la ligne dans la balise : la ligne prend la hauteur de la petite police.
                    text += "<size=17>\n" + line + "</size>";
            performanceText.text = text;
        }
    }

    private string DistractionLine()
    {
        if (!distraction.IsAdaptive)
            return $"Palier {distraction.Tier} (fixe)";
        // Trois états : changement décidé qui attend la fin d'une attaque, verrou (aucun changement possible),
        // surveillance (le palier change dès que P franchit un seuil).
        DistractionTier tier = distraction.Tier;
        string lower = tier == DistractionTier.Low ? "—" : (tier + 1).ToString();
        string upper = tier == DistractionTier.High ? "—" : (tier - 1).ToString();
        string state = distraction.Pending.HasValue ? $"→ {distraction.Pending.Value} dans {Mathf.CeilToInt(distraction.PendingRemaining)} s (attaque en cours)"
                     : distraction.LockRemaining > 0f ? $"VERROU {Mathf.CeilToInt(distraction.LockRemaining)} s"
                     : $"surveillance : < {distraction.LowerThreshold:0.00} → {lower}   > {distraction.UpperThreshold:0.00} → {upper}";
        WindowScore w = distraction.WindowNotes;
        return $"Palier {tier}   P {w.Total:0.00}   {state}\n" +
               $"Sur {distraction.Window:0} s : Dégâts {w.Damage:0.00}   Esquive {w.Avoidance:0.00}   Précision {w.Accuracy:0.00}   Réactivité {w.Reactivity:0.00}";
    }
}
