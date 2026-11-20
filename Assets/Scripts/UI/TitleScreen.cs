using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Écran de lancement : explique les commandes et lance la partie (bouton « Jouer », Entrée ou Espace).
public class TitleScreen : MonoBehaviour
{
    [SerializeField] private string gameScene = "SampleScene";
    [SerializeField] private Button playButton;
    // Contenu qui apparaît en fondu au lancement.
    [SerializeField] private CanvasGroup content;
    [SerializeField] private float appearDuration = 0.5f;
    // Voile noir qui recouvre l'écran avant de charger la partie.
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private float fadeDuration = 0.4f;

    private float appear;
    // < 0 : la partie n'est pas encore lancée.
    private float fade = -1f;

    private void Awake()
    {
        Time.timeScale = 1f;
        playButton.onClick.AddListener(Play);
        content.alpha = 0f;
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
    }

    private void Start()
    {
        playButton.Select();
    }

    private void Update()
    {
        appear = appearDuration > 0f ? Mathf.Min(1f, appear + Time.unscaledDeltaTime / appearDuration) : 1f;
        content.alpha = 1f - Mathf.Pow(1f - appear, 3f);

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            Play();

        if (fade < 0f)
            return;
        fade = fadeDuration > 0f ? Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeDuration) : 1f;
        fader.alpha = fade;
        if (fade >= 1f)
        {
            enabled = false;
            SceneManager.LoadScene(gameScene);
        }
    }

    private void Play()
    {
        if (fade >= 0f)
            return;
        fade = 0f;
        fader.blocksRaycasts = true;
    }
}
