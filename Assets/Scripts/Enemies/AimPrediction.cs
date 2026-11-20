using System;
using UnityEngine;

public enum AimPredictionMode
{
    // La cible garde sa vitesse actuelle, en ligne droite.
    Linear,
    // La cible garde sa vitesse et sa vitesse de virage : contre un joueur qui tourne en rond.
    Circular,
}

// Réglages de la visée anticipée des ennemis (fournis par le GameManager, modifiables en cours de partie).
[Serializable]
public class AimPredictionSettings
{
    public bool enabled = true;
    public AimPredictionMode mode = AimPredictionMode.Circular;
    // Attaques concernées.
    public bool fireball = true;
    public bool laser = true;
    // Part de l'anticipation appliquée, tirée au hasard à chaque tir (0 = position actuelle, 1 = point d'impact
    // calculé, au-delà = vise plus loin). Varier l'anticipation empêche de se protéger par une trajectoire régulière :
    // il faut lire le tir et esquiver.
    public Vector2 leadRange = new Vector2(0.6f, 1.15f);
    // Anticipation maximale (s) : au-delà, la prédiction n'a plus de sens.
    [Min(0f)] public float maxPredictionTime = 1.5f;
    // Le point visé reste dans la zone de jeu (la cible ne peut pas en sortir).
    public bool clampToArena = true;
    // Trace en Scene view la trajectoire prédite (jaune) et la visée (rouge).
    public bool debugDraw;
}

// Visée anticipée : simule la trajectoire de la cible pas à pas jusqu'au moment où l'attaque peut l'atteindre
// (méthode du « Circular Targeting » de Robocode ; en ligne droite, c'est la visée linéaire classique).
public static class AimPredictor
{
    private const float Step = 0.02f;

    public static Vector3 Aim(Vector3 origin, Transform target, MotionTracker motion, Ability ability, AimPredictionSettings settings, MovementBounds bounds)
    {
        Vector3 current = target.position;
        if (settings == null || !settings.enabled || motion == null || ability == null)
            return current;
        if (!(ability is LaserAbility ? settings.laser : settings.fireball))
            return current;

        Vector3 predicted = Predict(origin, current, motion, ability, settings, bounds);
        float lead = UnityEngine.Random.Range(settings.leadRange.x, settings.leadRange.y);
        Vector3 aim = current + (predicted - current) * lead;

        if (settings.debugDraw)
        {
            Debug.DrawLine(current, predicted, Color.yellow, 1f);
            Debug.DrawLine(origin, aim, Color.red, 1f);
        }
        return aim;
    }

    private static Vector3 Predict(Vector3 origin, Vector3 position, MotionTracker motion, Ability ability, AimPredictionSettings settings, MovementBounds bounds)
    {
        Vector3 velocity = motion.Velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (speed < 0.01f)
            return position;

        float heading = Mathf.Atan2(velocity.x, velocity.z);
        float turn = settings.mode == AimPredictionMode.Circular ? motion.TurnRate * Mathf.Deg2Rad : 0f;

        for (float t = 0f; t < settings.maxPredictionTime && HitTime(ability, origin, position) > t; t += Step)
        {
            heading += turn * Step;
            position += new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * speed * Step;
            if (settings.clampToArena && bounds != null)
                position = bounds.Clamp(position, 0.5f);
        }
        return position;
    }

    // Temps mis par l'attaque pour atteindre ce point.
    private static float HitTime(Ability ability, Vector3 origin, Vector3 point)
    {
        Vector3 offset = point - origin;
        offset.y = 0f;
        return ability.HitDelay + (ability.ProjectileSpeed > 0f ? offset.magnitude / ability.ProjectileSpeed : 0f);
    }
}
