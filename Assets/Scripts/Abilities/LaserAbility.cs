using UnityEngine;

// Rayon laser vers la souris : traverse toutes les cibles sur sa longueur et inflige des dégâts à chacune.
[CreateAssetMenu(menuName = "Abilities/Laser", fileName = "Laser")]
public class LaserAbility : Ability
{
    [SerializeField] private LaserBeam beamPrefab;
    [SerializeField] private float damage = 100f;
    [SerializeField] private float length = 12f;
    [SerializeField] private float width = 0.8f;
    // Temps de charge (ligne de visée) avant le tir.
    [SerializeField] private float chargeTime = 0.25f;
    [SerializeField] private float castHeight = 1f;

    // Le rayon part à la fin de la charge et touche instantanément.
    public override float HitDelay => chargeTime;

    public override void Cast(AbilityContext context)
    {
        Vector3 direction = context.AimPoint - context.Origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = context.Caster.transform.forward;
        direction.Normalize();

        Vector3 origin = context.Origin + direction * 0.5f;
        origin.y = castHeight;

        LaserBeam beam = Instantiate(beamPrefab, origin, Quaternion.LookRotation(direction));
        beam.Fire(context.Caster, origin, direction, length, width, damage, chargeTime, AttackRecord.Begin(context.Caster, this, origin, direction));
    }
}
