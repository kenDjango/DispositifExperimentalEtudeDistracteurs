using System;
using UnityEngine;

// Suivi d'une attaque (boule de feu, laser), du lancement à son issue : a-t-elle touché un adversaire ?
// Sert aux mesures de performance (précision du joueur, esquive des attaques ennemies).
public class AttackRecord
{
    // Toute attaque lancée, quel que soit le lanceur.
    public static event Action<AttackRecord> Launched;

    public GameObject Caster { get; }
    public Team CasterTeam { get; }
    public Ability Ability { get; }
    // Apparition de l'attaque à l'écran (projectile ou ligne de visée du laser), en Time.time.
    public float LaunchTime { get; }
    // Trajectoire (plan XZ) : point de départ et direction. Sert à reconnaître une réponse d'évitement
    // (déplacement qui s'écarte de la trajectoire).
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    // Adversaires touchés (ni alliés ni neutres).
    public int Hits { get; private set; }
    public bool IsResolved { get; private set; }
    public float ResolveTime { get; private set; }

    private AttackRecord(GameObject caster, Ability ability, Vector3 origin, Vector3 direction)
    {
        Caster = caster;
        CasterTeam = Health.TeamOf(caster);
        Ability = ability;
        LaunchTime = Time.time;
        Origin = new Vector3(origin.x, 0f, origin.z);
        Direction = new Vector3(direction.x, 0f, direction.z).normalized;
    }

    public static AttackRecord Begin(GameObject caster, Ability ability, Vector3 origin, Vector3 direction)
    {
        var record = new AttackRecord(caster, ability, origin, direction);
        Launched?.Invoke(record);
        return record;
    }

    public void RegisterHit(Health target)
    {
        if (IsResolved || target == null || target.Team == Team.Neutral || target.IsAllyOf(CasterTeam))
            return;
        Hits++;
    }

    // Issue connue (impact, portée atteinte, fin du tir). À appeler avant d'infliger les dégâts : un coup
    // fatal peut terminer la partie, et l'attaque doit être comptée avant.
    public void Resolve()
    {
        if (IsResolved)
            return;
        IsResolved = true;
        ResolveTime = Time.time;
    }
}
