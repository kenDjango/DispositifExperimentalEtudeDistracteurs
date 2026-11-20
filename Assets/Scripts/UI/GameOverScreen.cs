using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Écran de fin : victoire (temps tenu sans mourir) ou nombre de morts, détail du score et bouton pour continuer la
// session (questionnaire de charge de travail, voir StudySession).
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private GameManager game;
    // Voile plein écran contenant le panneau (désactivé pendant la partie).
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private RectTransform window;
    [SerializeField] private Text titleText;
    // Détail sur deux colonnes : intitulés à gauche, points à droite (une ligne chacun).
    [SerializeField] private Text labelsText;
    [SerializeField] private Text valuesText;
    [SerializeField] private Text totalText;
    [SerializeField] private Text bestText;
    [SerializeField] private Button continueButton;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Textes")]
    [SerializeField] private string victoryTitle = "Victoire : vous avez tenu !";
    [SerializeField] private Color victoryColor = new Color(1f, 0.82f, 0.35f);
    // Partie terminée avec au moins une mort ({0} : nombre de morts).
    [SerializeField] private string deathsTitleFormat = "Temps écoulé : {0} mort(s)";
    [SerializeField] private Color defeatColor = new Color(0.95f, 0.3f, 0.25f);
    [SerializeField] private string totalFormat = "Score  {0}";
    [SerializeField] private string bestFormat = "Meilleur score : {0}";
    [SerializeField] private string newRecordText = "Nouveau record !";
    [SerializeField] private string practiceTitle = "Entraînement terminé";
    [SerializeField] private Color practiceColor = new Color(0.55f, 0.8f, 1f);
    [SerializeField] private string practiceNote = "La vraie partie commence maintenant.";

    private float fade = -1f;

    // Le joueur a quitté l'écran de résultat.
    public event Action Continued;

    private void Awake()
    {
        panel.gameObject.SetActive(false);
        continueButton.onClick.AddListener(Continue);
    }

    private void Continue()
    {
        panel.gameObject.SetActive(false);
        fade = -1f;
        Continued?.Invoke();
    }

    private void Start()
    {
        game.GameEnded += Show;
    }

    private void OnDestroy()
    {
        if (game != null)
            game.GameEnded -= Show;
    }

    // Apparition en fondu (le jeu est à l'arrêt : temps réel).
    private void Update()
    {
        if (fade < 0f || fade >= 1f)
            return;
        fade = fadeDuration > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeDuration) : 1f;
        float k = 1f - Mathf.Pow(1f - fade, 3f);
        panel.alpha = k;
        window.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
    }

    private void Show(ScoreBreakdown result)
    {
        titleText.text = game.IsPractice ? practiceTitle : result.Survived ? victoryTitle : string.Format(deathsTitleFormat, result.Deaths);
        titleText.color = game.IsPractice ? practiceColor : result.Survived ? victoryColor : defeatColor;

        ScoreRules rules = game.Rules;
        var labels = new StringBuilder();
        var values = new StringBuilder();
        void Line(string label, string value)
        {
            labels.AppendLine(label);
            values.AppendLine(value);
        }

        Line($"Ennemis boule de feu  × {result.FireballKills}", $"+{result.FireballPoints}");
        Line($"Ennemis laser  × {result.LaserKills}", $"+{result.LaserPoints}");
        if (rules.pointsPerHealthPoint > 0f)
            Line(result.Survived ? $"Vie restante  {Mathf.CeilToInt(result.HealthRemaining)} / {result.HealthMax:0} PV" : "Vie restante (après une mort)",
                result.Survived ? $"+{result.HealthBonus}" : "—");
        if (rules.survivalBonus > 0)
            Line("Aucune mort", result.Survived ? $"+{result.SurvivalBonus}" : "—");
        if (rules.noDamageBonus > 0)
            Line("Aucun dégât subi", result.NoDamageBonus > 0 ? $"+{result.NoDamageBonus}" : "—");
        if (rules.healthMultiplier > 0f)
            Line("Multiplicateur de vie", $"× {result.Multiplier:0.00}");
        if (result.Deaths > 0)
            Line($"Morts  × {result.Deaths}", $"−{result.DeathPenalty}");

        labelsText.text = labels.ToString();
        valuesText.text = values.ToString();
        totalText.text = string.Format(totalFormat, result.Total);
        bestText.text = game.IsPractice ? practiceNote : result.NewRecord ? newRecordText : string.Format(bestFormat, result.BestScore);

        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        fade = 0f;
        continueButton.Select();
    }
}
