using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Boucle de jeu : partie chronométrée, score des ennemis tués, fin quand le temps est écoulé, puis ralenti et écran
// de résultat. Le joueur qui meurt réapparaît sur place avec toute sa vie, protégé quelques secondes : la partie dure
// toujours jusqu'au bout (mesures sur la même durée pour tous), chaque mort coûte des points et est comptée.
// Une session d'étude commence par une partie d'entraînement, puis compte gamesPerSession parties mesurées
// (voir StudySession).
public class GameManager : MonoBehaviour
{
    [Header("Partie")]
    // Durée de la partie mesurée (secondes) : elle se joue toujours jusqu'au bout, le joueur réapparaît après une mort.
    [SerializeField] private float duration = 150f;
    // Parties mesurées d'une session d'étude ; après la dernière, écran de remerciement au lieu d'une nouvelle partie.
    [SerializeField] private int gamesPerSession = 1;
    // Partie de prise en main en début de session : ni mesures ni questionnaire (0 : pas d'entraînement).
    [SerializeField] private float practiceDuration = 60f;
    // Ralenti (temps réel) jusqu'à l'arrêt du jeu avant l'affichage du résultat.
    [SerializeField] private float endSlowMotion = 1.2f;
    // Clé PlayerPrefs du meilleur score.
    [SerializeField] private string bestScoreKey = "OEOFD.BestScore";

    [Header("Mort du joueur")]
    // Invulnérabilité après la réapparition (secondes) ; le personnage clignote pendant ce temps.
    [SerializeField] private float respawnProtection = 2f;
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("Score")]
    [SerializeField] private ScoreRules scoreRules = new ScoreRules();

    [Header("Visée des ennemis")]
    // Les ennemis anticipent les déplacements du joueur (modifiable en cours de partie).
    [SerializeField] private AimPredictionSettings aimPrediction = new AimPredictionSettings();

    [Header("Références")]
    [SerializeField] private Health player;
    [SerializeField] private EnemyManager enemyManager;

    // Parties lancées depuis le démarrage du jeu (la scène est rechargée à chaque partie).
    private static int gamesStarted;

    private int fireballKills;
    private int laserKills;
    private bool tookDamage;

    // Durée de cette partie (celle de l'entraînement pour la partie d'entraînement).
    public float Duration { get; private set; }
    // Partie d'entraînement : non mesurée, sans questionnaire.
    public bool IsPractice { get; private set; }
    // Numéro de cette partie mesurée dans la session (à partir de 1 ; 0 pour l'entraînement).
    public int GameIndex { get; private set; }
    public int GamesPerSession => gamesPerSession;
    public bool IsLastGame => GameIndex >= gamesPerSession;
    public float TimeRemaining { get; private set; }
    // Score en cours de partie (ennemis tués) ; les bonus de fin s'ajoutent au résultat.
    public int Score => scoreRules.KillPoints(fireballKills, laserKills);
    public bool IsOver { get; private set; }
    // Morts du joueur depuis le début de la partie.
    public int Deaths { get; private set; }
    public float RespawnProtection => respawnProtection;
    // Moment (Time.time) où la partie s'est terminée, avant le ralenti.
    public float EndTime { get; private set; }
    public ScoreRules Rules => scoreRules;

    // (points gagnés)
    public event Action<int> ScoreGained;
    // Après le ralenti de fin, avec le détail du score.
    public event Action<ScoreBreakdown> GameEnded;
    // Le joueur vient de mourir et de réapparaître (début de sa protection).
    public event Action PlayerRespawned;

    // Sans rechargement du domaine à l'entrée en Play mode, les statiques survivent d'une session à l'autre.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        gamesStarted = 0;
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        gamesStarted++;
        bool practice = practiceDuration > 0f;
        IsPractice = practice && gamesStarted == 1;
        GameIndex = IsPractice ? 0 : gamesStarted - (practice ? 1 : 0);
        Duration = IsPractice ? practiceDuration : duration;
        TimeRemaining = Duration;

        // Réglages partagés : les modifier dans l'inspecteur agit sur les tirs suivants.
        if (enemyManager != null)
            enemyManager.AimPrediction = aimPrediction;
        // L'anticipation s'appuie sur le déplacement mesuré du joueur.
        if (player != null && player.GetComponent<MotionTracker>() == null)
            player.gameObject.AddComponent<MotionTracker>();
    }

    private void Start()
    {
        if (player != null)
        {
            player.Died += OnPlayerDied;
            player.Damaged += OnPlayerDamaged;
        }
        if (enemyManager != null)
            enemyManager.EnemyKilled += OnEnemyKilled;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (IsOver)
            return;

        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        if (TimeRemaining <= 0f)
            End();
    }

    private void OnEnemyKilled(EnemyController enemy)
    {
        if (IsOver)
            return;

        bool laser = enemy.Ability is LaserAbility;
        if (laser)
            laserKills++;
        else
            fireballKills++;
        ScoreGained?.Invoke(laser ? scoreRules.laserEnemyPoints : scoreRules.fireballEnemyPoints);
    }

    private void OnPlayerDamaged(float amount, float remaining)
    {
        if (!IsOver)
            tookDamage = true;
    }

    private void OnPlayerDied()
    {
        if (IsOver)
            return;
        Deaths++;
        if (enemyManager != null)
            enemyManager.PlayTeleportVfx(player.transform.position);
        player.Revive();
        PlayerRespawned?.Invoke();
        StartCoroutine(RespawnProtectionRoutine());
    }

    // Invulnérable et clignotant le temps de la protection.
    private IEnumerator RespawnProtectionRoutine()
    {
        player.Invulnerable = true;
        for (float t = 0f; t < respawnProtection; t += blinkInterval)
        {
            SetPlayerVisible(Mathf.FloorToInt(t / blinkInterval) % 2 == 1);
            yield return new WaitForSeconds(blinkInterval);
        }
        SetPlayerVisible(true);
        player.Invulnerable = false;
    }

    // Maillages du personnage (pas ses effets de particules).
    private void SetPlayerVisible(bool visible)
    {
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            if (!(r is ParticleSystemRenderer))
                r.enabled = visible;
    }

    private void End()
    {
        IsOver = true;
        EndTime = Time.time;
        if (enemyManager != null)
            enemyManager.StopSpawning();
        // Protection de réapparition interrompue : le personnage redevient visible (il reste invulnérable, ci-dessous).
        StopAllCoroutines();
        if (player != null)
            SetPlayerVisible(true);

        if (player != null)
        {
            // Le résultat est figé : plus de dégâts pendant le ralenti, et le joueur ne peut plus agir.
            player.Invulnerable = true;
            foreach (Behaviour control in new Behaviour[] { player.GetComponent<ClickToMove>(), player.GetComponent<AbilityCaster>() })
                if (control != null)
                    control.enabled = false;
        }

        ScoreBreakdown result = scoreRules.Compute(fireballKills, laserKills,
            player != null ? player.Current : 0f, player != null ? player.Max : 0f, Deaths, tookDamage);

        // L'entraînement, plus court, ne compte pas pour le meilleur score.
        int best = PlayerPrefs.GetInt(bestScoreKey, 0);
        result.NewRecord = !IsPractice && result.Total > best;
        result.BestScore = IsPractice ? best : Mathf.Max(best, result.Total);
        if (result.NewRecord)
        {
            PlayerPrefs.SetInt(bestScoreKey, result.Total);
            PlayerPrefs.Save();
        }

        Debug.Log($"Temps écoulé : {Deaths} mort(s), score {result.Total}.");
        StartCoroutine(EndRoutine(result));
    }

    private IEnumerator EndRoutine(ScoreBreakdown result)
    {
        for (float t = 0f; t < endSlowMotion; t += Time.unscaledDeltaTime)
        {
            Time.timeScale = 1f - t / endSlowMotion;
            yield return null;
        }
        Time.timeScale = 0f;
        GameEnded?.Invoke(result);
    }

    // Recharge la scène : nouvelle partie depuis le début.
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
