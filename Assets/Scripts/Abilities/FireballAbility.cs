using UnityEngine;

// Lance une boule de feu en ligne droite vers la souris ; inflige des dégâts au premier ennemi touché.
[CreateAssetMenu(menuName = "Abilities/Fireball", fileName = "Fireball")]
public class FireballAbility : Ability
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float damage = 60f;
    [SerializeField] private float speed = 18f;
    [SerializeField] private float range = 14f;
    [SerializeField] private float radius = 0.4f;
    // Hauteur de la boule de feu au-dessus du sol.
    [SerializeField] private float castHeight = 1f;

    public override float ProjectileSpeed => speed;

    public override void Cast(AbilityContext context)
    {
        Vector3 origin = context.Origin;
        origin.y = castHeight;

        Vector3 direction = context.AimPoint - context.Origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = context.Caster.transform.forward;
        direction.Normalize();

        Vector3 start = origin + direction * 0.6f;
        Projectile projectile = Instantiate(projectilePrefab, start, Quaternion.LookRotation(direction));
        projectile.Launch(context.Caster, direction, speed, range, radius, damage, AttackRecord.Begin(context.Caster, this, start, direction));
    }
}
