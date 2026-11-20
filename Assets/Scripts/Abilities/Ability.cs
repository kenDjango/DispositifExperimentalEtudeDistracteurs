using UnityEngine;

// Infos transmises à une compétence au moment du lancement.
public struct AbilityContext
{
    public GameObject Caster;
    public Vector3 Origin;
    // Point visé au sol (position de la souris).
    public Vector3 AimPoint;
}

// Base de toutes les compétences. Chaque compétence est un asset (clic droit > Create > Abilities).
public abstract class Ability : ScriptableObject
{
    [SerializeField] private string displayName = "Compétence";
    [SerializeField] private Sprite icon;
    [SerializeField] private float cooldown = 5f;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;

    // Moment de l'impact, pour viser en anticipant : délai fixe après le lancement (ex. charge du laser)
    // puis trajet du projectile (vitesse 0 = touche instantanément).
    public virtual float HitDelay => 0f;
    public virtual float ProjectileSpeed => 0f;

    public abstract void Cast(AbilityContext context);
}
