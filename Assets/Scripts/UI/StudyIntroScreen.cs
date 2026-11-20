using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Écran de présentation de l'étude, avant l'écran des commandes : message d'information, questionnaire
// démographique (ParticipantProfile) et consentement. « Continuer » n'est actif qu'une fois tout rempli.
public class StudyIntroScreen : MonoBehaviour
{
    [SerializeField] private string nextScene = "TitleScreen";
    [SerializeField] private Text messageText;
    [TextArea(4, 10)]
    [SerializeField] private string message =
        "L'objectif est ici d'étudier comment l'affichage d'une scène de jeu influence la performance. " +
        "La durée de la session est d'environ 6 à 8 minutes. Il vous sera demandé de répondre à des questions et de jouer à un mini-jeu.\n" +
        "Des données de performance et démographiques sont enregistrées ; elles sont complètement anonymes. " +
        "Aucune information personnelle n'est récoltée.";
    // Une échelle de choix par question, dans l'ordre de ParticipantProfile.Questions.
    [SerializeField] private RatingScale[] questions;
    [SerializeField] private Toggle consentToggle;
    [SerializeField] private Button continueButton;
    // Rappel de ce qui manque avant de pouvoir continuer.
    [SerializeField] private Text hintText;
    [SerializeField] private CanvasGroup content;
    [SerializeField] private float appearDuration = 0.5f;
    // Voile noir qui recouvre l'écran avant de charger la scène suivante.
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Textes")]
    [SerializeField] private string remainingFormat = "Encore {0} question(s) sans réponse";
    [SerializeField] private string consentMissing = "Cochez la case de consentement pour continuer";

    private float appear;
    // < 0 : pas encore validé.
    private float fade = -1f;

    private void Awake()
    {
        Time.timeScale = 1f;
        messageText.text = message;
        content.alpha = 0f;
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
        foreach (RatingScale question in questions)
            question.Changed += _ => Refresh();
        consentToggle.isOn = false;
        consentToggle.onValueChanged.AddListener(_ => Refresh());
        continueButton.onClick.AddListener(Continue);
        Refresh();
    }

    private void Update()
    {
        appear = appearDuration > 0f ? Mathf.Min(1f, appear + Time.unscaledDeltaTime / appearDuration) : 1f;
        content.alpha = 1f - Mathf.Pow(1f - appear, 3f);

        if (fade < 0f)
            return;
        fade = fadeDuration > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeDuration) : 1f;
        fader.alpha = fade;
        if (fade >= 1f)
        {
            enabled = false;
            SceneManager.LoadScene(nextScene);
        }
    }

    private int Remaining()
    {
        int remaining = 0;
        foreach (RatingScale question in questions)
            remaining += question.Value == 0 ? 1 : 0;
        return remaining;
    }

    private void Refresh()
    {
        int remaining = Remaining();
        continueButton.interactable = remaining == 0 && consentToggle.isOn;
        hintText.text = remaining > 0 ? string.Format(remainingFormat, remaining) : !consentToggle.isOn ? consentMissing : "";
    }

    private void Continue()
    {
        if (fade >= 0f || Remaining() > 0 || !consentToggle.isOn)
            return;
        var answers = new int[questions.Length];
        for (int i = 0; i < questions.Length; i++)
            answers[i] = questions[i].Value;
        ParticipantProfile.Set(answers, consentToggle.isOn);

        continueButton.interactable = false;
        fade = 0f;
        fader.blocksRaycasts = true;
    }
}
