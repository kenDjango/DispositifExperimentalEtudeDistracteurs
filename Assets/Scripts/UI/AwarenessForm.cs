using System;
using UnityEngine;
using UnityEngine.UI;

// Question ouverte de fin de session, après le dernier NASA-TLX : le joueur a-t-il remarqué quelque chose pendant
// les parties (système de distraction, sans le nommer) ? Réponse libre, facultative.
public class AwarenessForm : MonoBehaviour
{
    public const string Question = "Avez-vous remarqué quelque chose de particulier ou d'inhabituel pendant les parties ?";

    // Voile plein écran contenant la fenêtre (désactivé jusque-là).
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private RectTransform window;
    [SerializeField] private InputField answerField;
    [SerializeField] private Button submitButton;
    [SerializeField] private float fadeDuration = 0.35f;

    private float fade = -1f;

    // Réponse validée (vide si le joueur n'a rien écrit).
    public event Action<string> Submitted;

    private void Awake()
    {
        panel.gameObject.SetActive(false);
        submitButton.onClick.AddListener(Submit);
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

    public void Show()
    {
        answerField.text = "";
        submitButton.interactable = true;
        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        fade = 0f;
        answerField.ActivateInputField();
    }

    private void Submit()
    {
        submitButton.interactable = false;
        panel.gameObject.SetActive(false);
        Submitted?.Invoke(answerField.text.Trim());
    }
}
