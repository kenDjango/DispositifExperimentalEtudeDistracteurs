using UnityEngine;

// Recharge de dash laissée au sol par un ennemi laser vaincu : le joueur qui passe dessus récupère son dash
// immédiatement. Apparaît en grossissant, clignote avant de disparaître.
public class DashRecharge : MonoBehaviour
{
    // Partie visible (mise à l'échelle pour l'apparition, le clignotement et la disparition).
    [SerializeField] private Transform visual;
    [SerializeField] private GameObject collectVfx;
    // Distance au sol (plan XZ) à laquelle le joueur la ramasse.
    [SerializeField] private float pickupRadius = 1.1f;
    // Ramassée seulement si le dash est en recharge : sinon elle reste au sol pour plus tard.
    [SerializeField] private bool onlyWhenOnCooldown = true;
    [SerializeField] private float lifetime = 12f;
    // Clignote pendant les dernières secondes.
    [SerializeField] private float blinkTime = 3f;
    [SerializeField] private float blinkFrequency = 4f;
    [SerializeField] private float appearDuration = 0.35f;
    [SerializeField] private float disappearDuration = 0.25f;

    private Transform player;
    private Health playerHealth;
    private AbilityCaster caster;
    private float age;
    private float leavingTimer = -1f;

    public void Init(Transform player)
    {
        this.player = player;
        playerHealth = player != null ? player.GetComponent<Health>() : null;
        caster = player != null ? player.GetComponent<AbilityCaster>() : null;
    }

    private void Start()
    {
        visual.localScale = Vector3.zero;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (leavingTimer >= 0f)
        {
            leavingTimer += Time.deltaTime;
            float k = disappearDuration > 0f ? leavingTimer / disappearDuration : 1f;
            visual.localScale = Vector3.one * Mathf.Max(0f, 1f - k);
            if (k >= 1f)
                Destroy(gameObject);
            return;
        }

        if (age >= lifetime)
        {
            leavingTimer = 0f;
            return;
        }

        // Apparition avec léger dépassement, puis pulsation en fin de vie.
        float appear = appearDuration > 0f ? Mathf.Clamp01(age / appearDuration) : 1f;
        float scale = 1f - Mathf.Pow(1f - appear, 3f) + Mathf.Sin(appear * Mathf.PI) * 0.15f;
        if (lifetime - age < blinkTime)
            scale *= 0.8f + 0.2f * Mathf.Cos((lifetime - age) * blinkFrequency * Mathf.PI * 2f);
        visual.localScale = Vector3.one * scale;

        TryCollect();
    }

    private void TryCollect()
    {
        if (player == null || caster == null || !caster.enabled || (playerHealth != null && playerHealth.IsDead))
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > pickupRadius * pickupRadius)
            return;

        if (!caster.ResetCooldown<DashAbility>() && onlyWhenOnCooldown)
            return;

        // L'effet part des pieds du joueur et le suit (il « absorbe » l'énergie).
        if (collectVfx != null)
            Instantiate(collectVfx, new Vector3(player.position.x, transform.position.y, player.position.z), Quaternion.identity, player);
        Destroy(gameObject);
    }
}
