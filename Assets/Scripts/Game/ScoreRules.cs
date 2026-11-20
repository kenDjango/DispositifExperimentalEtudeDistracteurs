using System;
using UnityEngine;

// Détail du score final, affiché sur l'écran de fin.
public struct ScoreBreakdown
{
    // Aucune mort pendant la partie.
    public bool Survived;
    public int Deaths;
    public int FireballKills;
    public int LaserKills;
    public int FireballPoints;
    public int LaserPoints;
    public float HealthRemaining;
    public float HealthMax;
    public int HealthBonus;
    public int SurvivalBonus;
    public int NoDamageBonus;
    // Points retirés pour les morts (positif).
    public int DeathPenalty;
    // Multiplicateur appliqué au sous-total (1 = aucun).
    public float Multiplier;
    public int Total;
    // Renseignés par le GameManager.
    public int BestScore;
    public bool NewRecord;
}

// Règles de calcul du score, inspirées des bonus de fin de niveau classiques :
// - points par ennemi selon son type d'attaque ;
// - « bonus de vitalité » : la vie restante est convertie en points (épreuves d'ESO, beat'em up type Streets of Rage) ;
// - bonus fixes pour avoir tenu jusqu'au bout sans mourir et pour une partie sans dégât ;
// - multiplicateur optionnel du total selon la part de vie restante (comme le modificateur des épreuves d'ESO) ;
// - malus par mort (le joueur réapparaît et la partie continue).
// Le joueur réapparaît avec toute sa vie : après une mort, la vie restante ne rapporte rien (sinon mourir juste avant
// la fin rapporterait plus que le malus).
[Serializable]
public class ScoreRules
{
    [Header("Ennemis tués")]
    [Min(0)] public int fireballEnemyPoints = 10;
    [Min(0)] public int laserEnemyPoints = 20;

    [Header("Vie restante")]
    // Points par point de vie restant à la fin (500 PV x 0,5 = 250 points à pleine vie ; 0 = désactivé).
    [Min(0f)] public float pointsPerHealthPoint = 0.5f;
    // Bonus du total selon la part de vie restante : 0,5 -> x1,5 à pleine vie, x1 sans vie (0 = désactivé).
    [Min(0f)] public float healthMultiplier = 0f;

    [Header("Bonus de fin")]
    // Le joueur a tenu jusqu'au bout sans mourir.
    [Min(0)] public int survivalBonus = 100;
    // Le joueur a tenu jusqu'au bout sans subir le moindre dégât.
    [Min(0)] public int noDamageBonus = 200;

    [Header("Morts")]
    // Points retirés à chaque mort.
    [Min(0)] public int deathPenalty = 50;

    public int KillPoints(int fireballKills, int laserKills)
    {
        return fireballKills * fireballEnemyPoints + laserKills * laserEnemyPoints;
    }

    public ScoreBreakdown Compute(int fireballKills, int laserKills, float health, float maxHealth, int deaths, bool tookDamage)
    {
        bool survived = deaths == 0;
        health = survived ? Mathf.Max(0f, health) : 0f;
        float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

        var result = new ScoreBreakdown
        {
            Survived = survived,
            Deaths = deaths,
            FireballKills = fireballKills,
            LaserKills = laserKills,
            FireballPoints = fireballKills * fireballEnemyPoints,
            LaserPoints = laserKills * laserEnemyPoints,
            HealthRemaining = health,
            HealthMax = maxHealth,
            HealthBonus = Mathf.RoundToInt(health * pointsPerHealthPoint),
            SurvivalBonus = survived ? survivalBonus : 0,
            NoDamageBonus = survived && !tookDamage ? noDamageBonus : 0,
            DeathPenalty = deaths * deathPenalty,
            Multiplier = 1f + ratio * healthMultiplier,
        };

        int subtotal = result.FireballPoints + result.LaserPoints + result.HealthBonus + result.SurvivalBonus + result.NoDamageBonus;
        result.Total = Mathf.Max(0, Mathf.RoundToInt(subtotal * result.Multiplier) - result.DeathPenalty);
        return result;
    }
}
