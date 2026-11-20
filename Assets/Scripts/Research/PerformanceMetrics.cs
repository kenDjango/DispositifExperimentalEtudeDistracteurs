using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Mesures de performance d'une partie pour l'étude : morts, temps de réaction aux attaques ennemies, précision des
// sorts, esquive, et le score de performance qui les résume (recalculé en temps réel ; affiché seulement si le HUD
// l'autorise). Les attaques ennemies qui se déroulent pendant la protection d'une réapparition (joueur
// invulnérable) sont exclues des mesures. Détail des calculs : Docs/Metriques_performance.md.
public class PerformanceMetrics : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private ClickToMove playerMover;
    [SerializeField] private AbilityCaster playerCaster;

    [Header("Temps de réaction")]
    // Réponse plus rapide que ce délai après l'apparition de l'attaque : anticipation, non retenue (ms).
    [SerializeField] private float minReactionMs = 100f;
    // Au-delà de ce délai, ou si l'attaque est arrivée avant toute réponse : omission (ms).
    [SerializeField] private float maxReactionMs = 1500f;
    // Une réponse n'est une esquive que si le déplacement demandé s'écarte de la trajectoire de l'attaque d'au
    // moins cet angle (degrés), du côté où se trouve le joueur. 0 : tout déplacement qui s'en éloigne.
    [SerializeField, Range(0f, 90f)] private float minEvadeAngle = 30f;

    // Essai de temps de réaction : une attaque ennemie (stimulus) et la première réponse d'évitement.
    private class Trial
    {
        public AttackRecord Attack;
        // < 0 : pas encore de réponse retenue.
        public float ReactionMs = -1f;
    }

    [Header("Score en temps réel")]
    // Intervalle entre deux recalculs du score pendant la partie (secondes).
    [SerializeField] private float liveInterval = 0.25f;

    private readonly List<Trial> trials = new List<Trial>();
    private readonly List<AttackRecord> playerAttacks = new List<AttackRecord>();
    // Dégâts subis par le joueur (instant, montant), pour le score sur fenêtre glissante.
    private readonly List<(float time, float amount)> damageTaken = new List<(float, float)>();
    // Débuts des protections après une réapparition (Time.time).
    private readonly List<float> protections = new List<float>();
    private Health playerHealth;
    private float nextLiveUpdate;

    // Score de performance de la partie en cours (final une fois la partie terminée).
    public PerformanceScore Live { get; private set; } = PerformanceScore.Neutral;

    public event Action<PerformanceReport> Completed;

    private void OnEnable()
    {
        AttackRecord.Launched += OnAttackLaunched;
        if (playerMover != null)
            playerMover.MoveOrdered += OnEvasiveResponse;
        if (playerCaster != null)
            playerCaster.AbilityKeyPressed += OnAbilityKey;
        playerHealth = playerMover != null ? playerMover.GetComponent<Health>() : null;
        if (playerHealth != null)
            playerHealth.Damaged += OnPlayerDamaged;
        game.GameEnded += OnGameEnded;
        game.PlayerRespawned += OnPlayerRespawned;
    }

    private void OnDisable()
    {
        AttackRecord.Launched -= OnAttackLaunched;
        if (playerMover != null)
            playerMover.MoveOrdered -= OnEvasiveResponse;
        if (playerCaster != null)
            playerCaster.AbilityKeyPressed -= OnAbilityKey;
        if (playerHealth != null)
            playerHealth.Damaged -= OnPlayerDamaged;
        if (game != null)
        {
            game.GameEnded -= OnGameEnded;
            game.PlayerRespawned -= OnPlayerRespawned;
        }
    }

    private void OnPlayerRespawned()
    {
        protections.Add(Time.time);
    }

    // Vrai si l'attaque s'est déroulée (même en partie) pendant une protection de réapparition : le joueur ne
    // pouvait pas être blessé, elle ne dit rien de son esquive. L'attaque qui a tué le joueur, résolue à l'instant
    // même de la réapparition, reste comptée.
    private bool Protected(AttackRecord attack, float now)
    {
        float end = attack.IsResolved ? attack.ResolveTime : now;
        foreach (float start in protections)
            if (attack.LaunchTime < start + game.RespawnProtection && end > start)
                return true;
        return false;
    }

    private void OnAttackLaunched(AttackRecord attack)
    {
        if (game.IsOver || !(attack.Ability is FireballAbility || attack.Ability is LaserAbility))
            return;

        if (attack.CasterTeam == Team.Player)
            playerAttacks.Add(attack);
        else if (attack.CasterTeam == Team.Enemy)
            trials.Add(new Trial { Attack = attack });
    }

    private void OnPlayerDamaged(float amount, float remaining)
    {
        if (!game.IsOver)
            damageTaken.Add((Time.time, amount));
    }

    // Réponses possibles : ordre de déplacement (vers sa destination) ou touche du dash (vers la souris, même en
    // recharge : l'appui traduit la réaction).
    private void OnAbilityKey(int slot)
    {
        if (playerCaster.Slots[slot].ability is DashAbility)
            OnEvasiveResponse(playerCaster.GetMouseGroundPoint());
    }

    private void OnEvasiveResponse(Vector3 destination)
    {
        if (game.IsOver)
            return;

        float now = Time.time;
        Vector3 position = playerMover.transform.position;
        foreach (Trial trial in trials)
        {
            if (trial.ReactionMs >= 0f || (trial.Attack.IsResolved && trial.Attack.ResolveTime < now))
                continue;
            float elapsed = (now - trial.Attack.LaunchTime) * 1000f;
            if (elapsed >= minReactionMs && elapsed <= maxReactionMs && MovesAway(trial.Attack, position, destination))
                trial.ReactionMs = elapsed;
        }
    }

    // Vrai si aller de position à destination éloigne le joueur de la trajectoire de l'attaque : la part latérale
    // du déplacement (perpendiculaire à la trajectoire, du côté où se trouve le joueur) correspond à un angle d'au
    // moins minEvadeAngle avec la trajectoire. Reculer ou avancer le long de la trajectoire n'est pas une esquive.
    private bool MovesAway(AttackRecord attack, Vector3 position, Vector3 destination)
    {
        Vector3 move = destination - position;
        move.y = 0f;
        if (move.sqrMagnitude < 0.01f)
            return false;
        Vector3 side = Vector3.Cross(Vector3.up, attack.Direction);
        Vector3 offset = position - attack.Origin;
        offset.y = 0f;
        float lateral = Vector3.Dot(offset, side);
        float sideways = Vector3.Dot(move.normalized, side);
        // Joueur presque sur la trajectoire : s'écarter d'un côté ou de l'autre convient.
        float away = Mathf.Abs(lateral) < 0.1f ? Mathf.Abs(sideways) : sideways * Mathf.Sign(lateral);
        return away >= Mathf.Sin(minEvadeAngle * Mathf.Deg2Rad) && away > 0f;
    }

    // Score de performance en cours, recalculé pendant la partie, puis figé sur le résultat final.
    private void Update()
    {
        if (game.IsOver || Time.time < nextLiveUpdate)
            return;
        nextLiveUpdate = Time.time + liveInterval;
        Live = PerformanceScore.Compute(Measure(Time.time));
    }

    private void OnGameEnded(ScoreBreakdown result)
    {
        PerformanceReport report = Measure(game.EndTime);
        Live = report.Score;
        // Y compris l'entraînement (GameIndex = 0) : même palier pour tous, c'est la mesure de base de l'étude.
        Completed?.Invoke(report);
    }

    // Mesures sur la période allant du début de la partie à l'instant end (fin de partie, ou maintenant pour le
    // score en temps réel). Les attaques dont l'issue n'est pas encore connue sont exclues.
    private PerformanceReport Measure(float end)
    {
        bool ResolvedInGame(AttackRecord attack) => attack.IsResolved && attack.ResolveTime <= end;

        var report = new PerformanceReport
        {
            // Numéro de la partie dans la session (effet d'apprentissage).
            GameIndex = game.GameIndex,
            GamesPerSession = game.GamesPerSession,
            Deaths = game.Deaths,
            Duration = game.Duration,
            ReactionWindowMs = maxReactionMs,
        };

        // Temps de réaction : essais dont l'issue est connue avant la fin (réponse ou omission).
        var reactions = new List<float>();
        foreach (Trial trial in trials)
        {
            if (Protected(trial.Attack, end))
                continue;
            if (trial.ReactionMs >= 0f)
                reactions.Add(trial.ReactionMs);
            else if (ResolvedInGame(trial.Attack) || (end - trial.Attack.LaunchTime) * 1000f > maxReactionMs)
                report.ReactionOmissions++;
        }
        report.ReactionResponses = reactions.Count;
        reactions.Sort();
        if (reactions.Count > 0)
        {
            int n = reactions.Count;
            report.ReactionMedianMs = Median(reactions);
            float mean = reactions.Average();
            report.ReactionMeanMs = mean;
            if (n > 1)
                report.ReactionSdMs = Mathf.Sqrt(reactions.Sum(r => (r - mean) * (r - mean)) / (n - 1));
        }

        // Précision : sorts offensifs du joueur dont l'issue est connue avant la fin.
        foreach (AttackRecord attack in playerAttacks.Where(ResolvedInGame))
        {
            bool hit = attack.Hits > 0;
            report.SpellsCast++;
            report.SpellsHit += hit ? 1 : 0;
            if (attack.Ability is FireballAbility)
            {
                report.FireballsCast++;
                report.FireballsHit += hit ? 1 : 0;
            }
            else
            {
                report.LasersCast++;
                report.LasersHit += hit ? 1 : 0;
            }
        }

        // Esquive : attaques ennemies arrivées à leur terme avant la fin, sans toucher le joueur.
        foreach (Trial trial in trials.Where(t => ResolvedInGame(t.Attack) && !Protected(t.Attack, end)))
        {
            report.EnemyAttacks++;
            report.EnemyAttacksAvoided += trial.Attack.Hits == 0 ? 1 : 0;
        }
        report.Score = PerformanceScore.Compute(report);
        return report;
    }

    // Score de performance des dernières secondes (fenêtre glissante), de 0 à 1, utilisé par le système de
    // distraction, sur les seuls événements de la fenêtre :
    // - esquive et précision : attaques dont l'issue est connue dans la fenêtre (Laplace) ;
    // - réactivité : 1 − médiane / fenêtre de réponse, une omission comptant comme la fenêtre, sur les attaques
    //   ennemies lancées dans la fenêtre dont l'issue est connue (0,5 sans essai). Volontairement différente de la
    //   note de fin de partie (PerformanceScore) : sur peu d'essais, elle réagit vite, et les seuils y sont réglés ;
    // - dégâts subis : 1 − dégâts / (damageReference × vie maximale), damageReference étant la part de la vie dont la perte sur la
    //   fenêtre donne 0.
    // Calcul détaillé : Docs/Parametrage_distraction.md.
    public WindowScore MeasureWindow(float seconds, float damageReference)
    {
        float now = Time.time;
        float from = now - seconds;
        bool InWindow(AttackRecord attack) => attack.IsResolved && attack.ResolveTime > from && attack.ResolveTime <= now;

        int attacks = 0, avoided = 0;
        var reactions = new List<float>();
        foreach (Trial trial in trials)
        {
            if (Protected(trial.Attack, now))
                continue;
            if (InWindow(trial.Attack))
            {
                attacks++;
                avoided += trial.Attack.Hits == 0 ? 1 : 0;
            }
            if (trial.Attack.LaunchTime <= from)
                continue;
            if (trial.ReactionMs >= 0f)
                reactions.Add(trial.ReactionMs);
            else if (trial.Attack.IsResolved || (now - trial.Attack.LaunchTime) * 1000f > maxReactionMs)
                reactions.Add(maxReactionMs);
        }

        int cast = 0, hit = 0;
        foreach (AttackRecord attack in playerAttacks.Where(InWindow))
        {
            cast++;
            hit += attack.Hits > 0 ? 1 : 0;
        }

        float reactivity = 0.5f;
        if (reactions.Count > 0)
        {
            reactions.Sort();
            reactivity = Mathf.Clamp01(1f - Median(reactions) / maxReactionMs);
        }

        float damage = damageTaken.Where(d => d.time > from).Sum(d => d.amount);
        float reference = playerHealth != null ? playerHealth.Max * damageReference : 0f;
        float damageNote = reference > 0f ? Mathf.Clamp01(1f - damage / reference) : 1f;

        return new WindowScore(damageNote, PerformanceScore.Laplace(avoided, attacks), PerformanceScore.Laplace(hit, cast), reactivity);
    }

    // Médiane d'une liste triée non vide.
    private static float Median(List<float> sorted)
    {
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
    }
}

// Score sur fenêtre glissante (MeasureWindow) : ses quatre notes sur 1 et leur moyenne.
public readonly struct WindowScore
{
    public readonly float Damage;
    public readonly float Avoidance;
    public readonly float Accuracy;
    public readonly float Reactivity;

    public float Total => (Damage + Avoidance + Accuracy + Reactivity) / 4f;

    public WindowScore(float damage, float avoidance, float accuracy, float reactivity)
    {
        Damage = damage;
        Avoidance = avoidance;
        Accuracy = accuracy;
        Reactivity = reactivity;
    }
}

// Résultat des mesures d'une partie. Valeur null : non calculable (aucun essai).
public class PerformanceReport
{
    // Numéro de la partie mesurée ; 0 pour l'entraînement.
    public int GameIndex;
    // Parties mesurées de la session (la dernière a GameIndex = GamesPerSession).
    public int GamesPerSession;
    // Morts du joueur pendant la partie (il réapparaît à chaque fois).
    public int Deaths;
    public float Duration;

    public int ReactionResponses;
    public int ReactionOmissions;
    public float? ReactionMedianMs;
    public float? ReactionMeanMs;
    public float? ReactionSdMs;
    public float ReactionWindowMs;

    public int SpellsCast;
    public int SpellsHit;
    public int FireballsCast;
    public int FireballsHit;
    public int LasersCast;
    public int LasersHit;

    public int EnemyAttacks;
    public int EnemyAttacksAvoided;

    // Score de performance tiré de ces mesures.
    public PerformanceScore Score;

    public float? Accuracy => Ratio(SpellsHit, SpellsCast);
    public float? FireballAccuracy => Ratio(FireballsHit, FireballsCast);
    public float? LaserAccuracy => Ratio(LasersHit, LasersCast);
    public float? AvoidanceRate => Ratio(EnemyAttacksAvoided, EnemyAttacks);

    private static float? Ratio(int part, int total) => total > 0 ? part / (float)total : (float?)null;
}
