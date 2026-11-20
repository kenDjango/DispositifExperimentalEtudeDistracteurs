using System.Collections.Generic;
using UnityEngine;

// Rayon laser : ligne de visée pendant la charge, puis tir qui touche toutes les cibles sur la ligne.
public class LaserBeam : MonoBehaviour
{
    [SerializeField] private LineRenderer telegraph;
    [SerializeField] private LineRenderer beamGlow;
    [SerializeField] private LineRenderer beamCore;
    // Large voile lumineux autour du rayon.
    [SerializeField] private LineRenderer beamHaze;
    // Filaments électriques qui défilent le long du rayon.
    [SerializeField] private LineRenderer beamNoise;
    [SerializeField] private ParticleSystem beamParticles;
    [SerializeField] private ParticleSystem hitSparks;
    // Énergie aspirée vers la source pendant la charge.
    [SerializeField] private ParticleSystem charge;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem endFlash;
    [SerializeField] private Light flash;
    [SerializeField] private float beamDuration = 0.35f;
    [SerializeField] private float particlesPerUnit = 6f;
    [SerializeField] private float flashIntensity = 6f;
    // Répétitions de la texture des filaments par unité de longueur, et vitesse de défilement.
    [SerializeField] private float noiseTiling = 0.4f;
    [SerializeField] private float noiseScrollSpeed = 3f;

    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private MaterialPropertyBlock noiseBlock;

    private GameObject owner;
    private Team ownerTeam;
    private Vector3 origin;
    private Vector3 direction;
    private float length;
    private float width;
    private float damage;
    private float chargeTime;
    private float timer;
    private bool fired;
    private AttackRecord record;

    public void Fire(GameObject owner, Vector3 origin, Vector3 direction, float length, float width, float damage, float chargeTime, AttackRecord record = null)
    {
        this.owner = owner;
        this.record = record;
        // Mémorisé : le lanceur peut mourir pendant la charge.
        ownerTeam = Health.TeamOf(owner);
        this.origin = origin;
        this.direction = direction;
        // Le décor arrête le rayon (les obstacles servent de couverture).
        if (Physics.Raycast(origin, direction, out RaycastHit wall, length, EnvironmentCollision.Mask, QueryTriggerInteraction.Ignore))
            length = wall.distance;
        this.length = length;
        this.width = width;
        this.damage = damage;
        this.chargeTime = chargeTime;

        Vector3 end = origin + direction * length;
        foreach (LineRenderer line in Lines())
        {
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.enabled = false;
        }

        telegraph.enabled = true;
        telegraph.widthMultiplier = 0f;
        flash.enabled = false;
        if (endFlash != null)
            endFlash.transform.position = end;
        if (charge != null)
            charge.Play();

        if (chargeTime <= 0f)
            Discharge();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!fired)
        {
            // La ligne de visée s'épaissit pendant la charge.
            float k = chargeTime > 0f ? timer / chargeTime : 1f;
            telegraph.widthMultiplier = Mathf.Lerp(0.05f, width * 0.35f, k);
            if (timer >= chargeTime)
                Discharge();
            return;
        }

        float t = timer / beamDuration;
        if (t >= 1f)
        {
            foreach (LineRenderer line in Lines())
                line.enabled = false;
            flash.enabled = false;
            enabled = false;
            // Laisse les particules finir avant de détruire l'objet.
            Destroy(gameObject, 1f);
            return;
        }

        // Rayon qui s'amincit en vibrant.
        float fade = 1f - t * t;
        float flicker = 1f + Random.Range(-0.12f, 0.12f);
        beamGlow.widthMultiplier = width * 1.6f * fade * flicker;
        beamCore.widthMultiplier = width * 0.45f * fade;
        if (beamHaze != null)
            beamHaze.widthMultiplier = width * 3.2f * fade;
        if (beamNoise != null)
        {
            beamNoise.widthMultiplier = width * 1.2f * fade * flicker;
            noiseBlock ??= new MaterialPropertyBlock();
            beamNoise.GetPropertyBlock(noiseBlock);
            noiseBlock.SetVector(BaseMapStId, new Vector4(length * noiseTiling, 1f, -timer * noiseScrollSpeed, 0f));
            beamNoise.SetPropertyBlock(noiseBlock);
        }
        flash.intensity = flashIntensity * fade;
    }

    private IEnumerable<LineRenderer> Lines()
    {
        foreach (LineRenderer line in new[] { telegraph, beamGlow, beamCore, beamHaze, beamNoise })
            if (line != null)
                yield return line;
    }

    private void Discharge()
    {
        fired = true;
        timer = 0f;

        foreach (LineRenderer line in Lines())
            line.enabled = line != telegraph;
        flash.enabled = true;
        if (charge != null)
            charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (endFlash != null)
            endFlash.Play();
        flash.transform.position = origin + direction * length * 0.5f;

        ApplyDamage();
        EmitBeamParticles();
    }

    private void ApplyDamage()
    {
        var alreadyHit = new HashSet<Health>();
        var targets = new List<(Health health, Vector3 point)>();
        RaycastHit[] hits = Physics.SphereCastAll(origin - direction * width, width * 0.5f, direction, length + width, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform))
                continue;

            Health health = hit.collider.GetComponentInParent<Health>();
            if (health == null || health.IsAllyOf(ownerTeam) || !alreadyHit.Add(health))
                continue;

            // distance == 0 : la cible chevauchait déjà le départ du rayon, hit.point n'est pas renseigné.
            targets.Add((health, hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(origin)));
            record?.RegisterHit(health);
        }

        // Issue du tir enregistrée avant les dégâts (un coup fatal peut terminer la partie).
        record?.Resolve();
        foreach ((Health health, Vector3 point) in targets)
        {
            health.TakeDamage(damage);
            EmitHitSparks(point);
        }
    }

    private void EmitBeamParticles()
    {
        int count = Mathf.CeilToInt(length * particlesPerUnit);
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            emit.position = origin + direction * Random.Range(0f, length) + Random.insideUnitSphere * width * 0.3f;
            emit.velocity = Random.insideUnitSphere * 1.5f + Vector3.up * 0.5f;
            beamParticles.Emit(emit, 1);
        }
    }

    private void EmitHitSparks(Vector3 point)
    {
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < 18; i++)
        {
            emit.position = point;
            emit.velocity = (Random.onUnitSphere + direction * 0.8f).normalized * Random.Range(3f, 7f);
            hitSparks.Emit(emit, 1);
        }
    }
}
