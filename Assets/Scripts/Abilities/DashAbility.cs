using UnityEngine;

// Dash rapide vers la souris, limité à une distance maximale.
[CreateAssetMenu(menuName = "Abilities/Dash", fileName = "Dash")]
public class DashAbility : Ability
{
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private float duration = 0.15f;
    [SerializeField] private DashVfx vfxPrefab;

    public override void Cast(AbilityContext context)
    {
        DashMotor motor = context.Caster.GetComponent<DashMotor>();
        if (motor == null)
            motor = context.Caster.AddComponent<DashMotor>();

        Vector3 toAim = context.AimPoint - context.Origin;
        toAim.y = 0f;
        Vector3 direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : context.Caster.transform.forward;
        float distance = Mathf.Min(toAim.magnitude, maxDistance);

        motor.Dash(context.Origin + direction * distance, duration, vfxPrefab);
    }
}
