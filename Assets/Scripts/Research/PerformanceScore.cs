using UnityEngine;

// Score de performance de l'étude (0 à 100) : moyenne de quatre notes sur 1, sans poids ni donnée de référence.
// - Morts      M = 1 / (1 + nombre de morts)                                (le joueur réapparaît après une mort)
// - Esquive    E = (attaques évitées + 1) / (attaques ennemies + 2)          (règle de succession de Laplace)
// - Précision  A = (sorts qui touchent + 1) / (sorts lancés + 2)            (idem)
// - Réactivité R = taux de réponse × (1 − médiane des TR / fenêtre)         (0,5 sans aucun essai)
// Calcul détaillé : Docs/Metriques_performance.md, section 6.
public readonly struct PerformanceScore
{
    public readonly float Deaths;
    public readonly float Avoidance;
    public readonly float Accuracy;
    public readonly float Reactivity;

    public float Total => 100f * (Deaths + Avoidance + Accuracy + Reactivity) / 4f;

    // Début de partie : aucune mort, rien d'autre n'est encore mesuré.
    public static readonly PerformanceScore Neutral = new PerformanceScore(1f, 0.5f, 0.5f, 0.5f);

    public PerformanceScore(float deaths, float avoidance, float accuracy, float reactivity)
    {
        Deaths = deaths;
        Avoidance = avoidance;
        Accuracy = accuracy;
        Reactivity = reactivity;
    }

    public static PerformanceScore Compute(PerformanceReport report)
    {
        return new PerformanceScore(1f / (1f + report.Deaths),
            Laplace(report.EnemyAttacksAvoided, report.EnemyAttacks),
            Laplace(report.SpellsHit, report.SpellsCast),
            ComputeReactivity(report));
    }

    // Réagir souvent et vite : la note baisse progressivement avec les omissions, sans plancher à 0 dès qu'elles
    // dépassent la moitié des essais (ce que donnait une médiane qui comptait les omissions).
    private static float ComputeReactivity(PerformanceReport report)
    {
        int trials = report.ReactionResponses + report.ReactionOmissions;
        if (trials == 0 || report.ReactionWindowMs <= 0f)
            return 0.5f;
        if (!report.ReactionMedianMs.HasValue)
            return 0f;
        float responseRate = report.ReactionResponses / (float)trials;
        return responseRate * Mathf.Clamp01(1f - report.ReactionMedianMs.Value / report.ReactionWindowMs);
    }

    // Proportion de succès avec un succès et un échec fictifs : prudente sur peu d'essais, 0,5 sans essai.
    public static float Laplace(int successes, int trials) => (successes + 1f) / (trials + 2f);
}
