using UnityEngine;

// Enchaînement d'une session d'étude : partie d'entraînement (résultat, puis première partie mesurée), puis après
// chaque partie mesurée, écran de résultat puis questionnaire NASA-TLX ; les
// réponses partent aussitôt avec les mesures de la partie (rien n'est perdu si le participant s'arrête là), puis la
// partie suivante commence, ou, après la dernière, la question ouverte puis l'écran de remerciement s'affichent (la
// réponse complète la ligne déjà envoyée).
public class StudySession : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private GameOverScreen gameOverScreen;
    [SerializeField] private WorkloadForm workloadForm;
    // Question ouverte de fin de session (absente : directement le remerciement).
    [SerializeField] private AwarenessForm awarenessForm;
    [SerializeField] private MetricsReporter reporter;
    // Écran de remerciement de fin de session (désactivé jusque-là).
    [SerializeField] private CanvasGroup thanksPanel;
    [SerializeField] private float fadeDuration = 0.6f;

    private float fade = -1f;

    private void Awake()
    {
        thanksPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        gameOverScreen.Continued += OnResultClosed;
        workloadForm.Submitted += OnWorkloadSubmitted;
        if (awarenessForm != null)
            awarenessForm.Submitted += OnAwarenessSubmitted;
    }

    private void OnDisable()
    {
        if (gameOverScreen != null)
            gameOverScreen.Continued -= OnResultClosed;
        if (workloadForm != null)
            workloadForm.Submitted -= OnWorkloadSubmitted;
        if (awarenessForm != null)
            awarenessForm.Submitted -= OnAwarenessSubmitted;
    }

    private void Update()
    {
        if (fade < 0f || fade >= 1f)
            return;
        fade = fadeDuration > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeDuration) : 1f;
        thanksPanel.alpha = fade;
    }

    private void OnResultClosed()
    {
        // Après l'entraînement : directement la première partie mesurée, sans questionnaire.
        if (game.IsPractice)
        {
            game.Restart();
            return;
        }
        workloadForm.Show(reporter != null ? reporter.Participant : "", game.GameIndex, game.GamesPerSession);
    }

    private void OnWorkloadSubmitted(int[] ratings)
    {
        if (reporter != null)
            reporter.Send(ratings);

        if (!game.IsLastGame)
            game.Restart();
        else if (awarenessForm != null)
            awarenessForm.Show();
        else
            ShowThanks();
    }

    private void OnAwarenessSubmitted(string answer)
    {
        if (reporter != null)
            reporter.SendRemark(answer);
        ShowThanks();
    }

    private void ShowThanks()
    {
        thanksPanel.gameObject.SetActive(true);
        thanksPanel.alpha = 0f;
        fade = 0f;
    }
}
