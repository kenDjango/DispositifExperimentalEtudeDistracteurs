using System;
using UnityEngine;

// Camp d'une entité : les compétences ne blessent pas les alliés (Neutral n'a pas d'alliés).
public enum Team
{
    Neutral,
    Player,
    Enemy,
}

// Points de vie d'une entité qui peut recevoir des dégâts.
public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private Team team = Team.Neutral;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead => Current <= 0f;
    public Team Team => team;
    // Ignore les dégâts (ex. joueur à la fin de la partie, pendant le ralenti).
    public bool Invulnerable { get; set; }

    // (dégâts reçus, vie restante)
    public event Action<float, float> Damaged;
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    // Réglage à l'apparition (ex. ennemis créés par l'EnemyManager).
    public void Init(float max, Team team)
    {
        maxHealth = max;
        Current = max;
        this.team = team;
    }

    // Retour à la vie avec tous ses points de vie (réapparition du joueur).
    public void Revive()
    {
        Current = maxHealth;
    }

    // Vrai si ce camp est celui donné (et n'est pas neutre).
    public bool IsAllyOf(Team other)
    {
        return team != Team.Neutral && team == other;
    }

    // Camp d'un objet (Neutral s'il n'a pas de Health).
    public static Team TeamOf(GameObject go)
    {
        Health health = go != null ? go.GetComponentInParent<Health>() : null;
        return health != null ? health.team : Team.Neutral;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || Invulnerable || amount <= 0f)
            return;

        Current = Mathf.Max(0f, Current - amount);
        Debug.Log($"{name} prend {amount} dégâts ({Current}/{maxHealth})");
        Damaged?.Invoke(amount, Current);

        if (IsDead)
        {
            Died?.Invoke();
            if (destroyOnDeath)
                Destroy(gameObject);
        }
    }
}
