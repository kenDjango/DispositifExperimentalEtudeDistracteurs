using UnityEngine;

// Projectile en ligne droite : touche le premier collider sur son trajet (sauf le lanceur), puis explose.
public class Projectile : MonoBehaviour
{
    [SerializeField] private GameObject impactPrefab;
    // Temps laissé aux particules (traînée) pour disparaître après l'impact.
    [SerializeField] private float lingerTime = 0.6f;

    private GameObject owner;
    private Team ownerTeam;
    private Vector3 direction;
    private float speed;
    private float remainingRange;
    private float radius;
    private float damage;
    private bool finished;
    private AttackRecord record;

    public void Launch(GameObject owner, Vector3 direction, float speed, float range, float radius, float damage, AttackRecord record = null)
    {
        this.owner = owner;
        this.record = record;
        // Mémorisé : le lanceur peut mourir pendant le vol.
        ownerTeam = Health.TeamOf(owner);
        this.direction = direction;
        this.speed = speed;
        remainingRange = range;
        this.radius = radius;
        this.damage = damage;
    }

    private void Update()
    {
        if (finished)
            return;

        float step = Mathf.Min(speed * Time.deltaTime, remainingRange);
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, step, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform))
                continue;

            // Traverse les alliés du lanceur.
            Health health = hit.collider.GetComponentInParent<Health>();
            if (health != null && health.IsAllyOf(ownerTeam))
                continue;

            transform.position += direction * hit.distance;
            record?.RegisterHit(health);
            record?.Resolve();
            if (health != null)
                health.TakeDamage(damage);
            Explode();
            return;
        }

        transform.position += direction * step;
        remainingRange -= step;
        if (remainingRange <= 0f)
        {
            record?.Resolve();
            Explode();
        }
    }

    private void Explode()
    {
        finished = true;

        if (impactPrefab != null)
            Instantiate(impactPrefab, transform.position, Quaternion.identity);

        // Arrête l'émission mais laisse les particules déjà émises finir leur vie.
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        foreach (Light l in GetComponentsInChildren<Light>())
            l.enabled = false;
        foreach (Renderer r in GetComponentsInChildren<MeshRenderer>())
            r.enabled = false;

        Destroy(gameObject, lingerTime);
    }
}
