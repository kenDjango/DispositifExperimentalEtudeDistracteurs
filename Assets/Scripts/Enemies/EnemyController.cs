using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Réglages communs des ennemis, fournis par l'EnemyManager à l'apparition.
[Serializable]
public class EnemyStats
{
    public float maxHealth = 200f;
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 540f;
    // Distance à partir de laquelle l'ennemi peut attaquer.
    public float attackRange = 10f;
    // L'ennemi s'arrête d'avancer à cette fraction de la portée.
    [Range(0.2f, 1f)] public float preferredRangeRatio = 0.7f;
    // Temps (aléatoire entre x et y) entre deux attaques.
    public Vector2 attackInterval = new Vector2(2.5f, 4f);
    // Délai avant la première attaque après l'apparition.
    public float firstAttackDelay = 1.5f;
    // Immobilisation après une attaque (le temps de lancer le sort).
    public float attackPause = 0.6f;
}

// IA d'un ennemi : s'approche du joueur en contournant le décor (ArenaNavigation), se tourne vers lui et l'attaque
// avec son unique compétence quand il le voit.
// L'apparence est un modèle tiré au hasard, animé (repos, marche, boule de feu, laser) ; les ennemis s'écartent
// les uns des autres au lieu de se traverser.
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkSpeedId = Animator.StringToHash("WalkSpeed");
    private static readonly int FireballId = Animator.StringToHash("Fireball");
    private static readonly int LaserId = Animator.StringToHash("Laser");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Ennemis en jeu, pour la séparation.
    private static readonly List<EnemyController> Active = new List<EnemyController>();

    [Header("Apparence")]
    // Modèles possibles (un est tiré au hasard à l'apparition), pieds à l'origine, avec leur Animator.
    [SerializeField] private GameObject[] models;
    // Matériau du modèle qui prend la couleur de l'attaque, et celui des yeux qui brillent de cette couleur.
    [SerializeField] private string bodyMaterialName = "Main";
    [SerializeField] private string eyeMaterialName = "Eye";
    [SerializeField] private float spawnScaleDuration = 0.25f;
    // Lueur propre de l'ennemi (fraction de sa couleur), pour rester lisible dans un décor sombre.
    [SerializeField] private float emissionStrength = 0.35f;
    [SerializeField] private float eyeGlow = 4f;

    [Header("Animation")]
    // Vitesse de déplacement (m/s) pour laquelle l'animation de marche est jouée à sa vitesse d'origine.
    [SerializeField] private float walkAnimationSpeed = 1.8f;
    [SerializeField] private float speedDamping = 0.1f;
    // Délai entre le début du geste et le départ de l'attaque (impact du coup de poing, bras levé pour le laser).
    [SerializeField] private float fireballWindup = 0.3f;
    [SerializeField] private float laserWindup = 0.1f;

    [Header("Navigation")]
    // Intervalle entre deux recalculs du chemin et de la ligne de vue (secondes).
    [SerializeField] private float repathInterval = 0.3f;
    // Hauteur (au-dessus du sol) de la ligne de vue vers le joueur : celle des tirs.
    [SerializeField] private float sightHeight = 1f;

    [Header("Séparation")]
    // En dessous de cette distance entre deux ennemis, ils s'écartent l'un de l'autre.
    [SerializeField] private float separationRadius = 1.8f;
    [SerializeField] private float separationStrength = 2.5f;

    private Health health;
    private CapsuleCollider capsule;
    private Animator animator;
    private Renderer bodyRenderer;
    private Ability ability;
    private EnemyStats stats;
    private Transform target;
    private Health targetHealth;
    private MotionTracker targetMotion;
    private AimPredictionSettings aimPrediction;
    private MovementBounds bounds;
    private float nextAttackTime;
    private float pausedUntil;
    // Moment où l'attaque en préparation part (< 0 : aucune).
    private float castAt = -1f;
    private float spawnTimer;
    private Vector3 lastPosition;
    // Direction d'écartement quand deux ennemis sont exactement au même point (propre à chaque ennemi).
    private float fallbackAngle;
    // Créé dans Awake : construit dans l'initialiseur du champ, il n'aurait pas d'objet natif.
    private NavMeshPath path;
    private float nextRepath;
    // Prochain point du chemin vers le joueur (au sol), et si le joueur est visible (aucun obstacle entre eux).
    private Vector3 waypoint;
    private bool hasWaypoint;
    private bool targetVisible = true;

    public Health Health => health;
    public Ability Ability => ability;

    private void Awake()
    {
        health = GetComponent<Health>();
        capsule = GetComponent<CapsuleCollider>();
        SpawnModel();
        lastPosition = transform.position;
        fallbackAngle = UnityEngine.Random.Range(0f, 360f);
        path = new NavMeshPath();
        // Recalculs étalés entre les ennemis.
        nextRepath = UnityEngine.Random.Range(0f, repathInterval);
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    // Instancie un des modèles, les pieds posés au bas de la capsule.
    private void SpawnModel()
    {
        if (models == null || models.Length == 0)
            return;

        GameObject prefab = models[UnityEngine.Random.Range(0, models.Length)];
        float feet = capsule != null ? capsule.center.y - capsule.height * 0.5f : 0f;
        GameObject model = Instantiate(prefab, transform);
        model.transform.SetLocalPositionAndRotation(new Vector3(0f, feet, 0f), Quaternion.identity);
        animator = model.GetComponentInChildren<Animator>();
        bodyRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
    }

    public void Init(Ability ability, Color color, EnemyStats stats, Transform target, MovementBounds bounds, AimPredictionSettings aimPrediction)
    {
        this.ability = ability;
        this.stats = stats;
        this.target = target;
        this.bounds = bounds;
        this.aimPrediction = aimPrediction;
        targetHealth = target != null ? target.GetComponent<Health>() : null;
        targetMotion = target != null ? target.GetComponent<MotionTracker>() : null;

        health.Init(stats.maxHealth, Team.Enemy);
        nextAttackTime = Time.time + spawnScaleDuration + stats.firstAttackDelay;
        transform.localScale = Vector3.zero;
        Tint(color);
    }

    // Couleur de l'attaque sur la carrosserie principale et les yeux (le reste du modèle garde ses couleurs).
    private void Tint(Color color)
    {
        if (bodyRenderer == null)
            return;

        Material[] materials = bodyRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                continue;
            var block = new MaterialPropertyBlock();
            if (materials[i].name.StartsWith(bodyMaterialName))
            {
                block.SetColor(BaseColorId, color);
                block.SetColor(EmissionColorId, color * emissionStrength);
            }
            else if (materials[i].name.StartsWith(eyeMaterialName))
            {
                //block.SetColor(EmissionColorId, color * eyeGlow);
            }
            else
            {
                continue;
            }
            bodyRenderer.SetPropertyBlock(block, i);
        }
    }

    private void Update()
    {
        // Apparition : grossit rapidement avec un léger rebond.
        if (spawnTimer < spawnScaleDuration)
        {
            spawnTimer += Time.deltaTime;
            float k = Mathf.Clamp01(spawnTimer / spawnScaleDuration);
            float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
            transform.localScale = Vector3.one * (1f - Mathf.Pow(1f - k, 3f)) * overshoot;
            lastPosition = transform.position;
            return;
        }
        transform.localScale = Vector3.one;

        if (stats == null || target == null || targetHealth == null || targetHealth.IsDead || health.IsDead)
        {
            Animate();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (Time.time >= nextRepath)
            Repath();

        Vector3 moved = Move(toTarget, distance);

        // Face au joueur quand il est en vue (pour viser), sinon dans le sens de la marche (contournement).
        Vector3 facing = targetVisible || moved.sqrMagnitude < 0.0001f ? toTarget : moved;
        if (facing.sqrMagnitude > 0.000001f)
        {
            Quaternion look = Quaternion.LookRotation(facing.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, stats.rotationSpeed * Time.deltaTime);
        }

        // Pas de tir dans un obstacle : il faut voir le joueur.
        if (castAt >= 0f && Time.time >= castAt)
            Cast();
        else if (castAt < 0f && Time.time >= nextAttackTime && distance <= stats.attackRange && targetVisible && ability != null)
            StartAttack();

        Animate();
    }

    // Ligne de vue vers le joueur et prochain point du chemin qui contourne le décor.
    private void Repath()
    {
        nextRepath = Time.time + repathInterval;
        Vector3 eye = new Vector3(transform.position.x, sightHeight, transform.position.z);
        Vector3 aim = new Vector3(target.position.x, sightHeight, target.position.z);
        targetVisible = !Physics.Linecast(eye, aim, EnvironmentCollision.Mask, QueryTriggerInteraction.Ignore);
        hasWaypoint = ArenaNavigation.NextWaypoint(transform.position, target.position, path, out waypoint);
    }

    // Avance vers le joueur jusqu'à la distance de tir préférée, en suivant le chemin ; s'il est caché derrière un
    // obstacle, continue jusqu'à le voir. Renvoie le déplacement effectué.
    private Vector3 Move(Vector3 toTarget, float distance)
    {
        bool casting = Time.time < pausedUntil;
        Vector3 velocity = Vector3.zero;
        bool advance = distance > stats.attackRange * stats.preferredRangeRatio || !targetVisible;
        if (!casting && advance && distance > 0.001f)
        {
            Vector3 toWaypoint = hasWaypoint ? waypoint - transform.position : toTarget;
            toWaypoint.y = 0f;
            // Point de passage atteint d'ici le prochain recalcul : on vise déjà le suivant.
            if (hasWaypoint && toWaypoint.sqrMagnitude < 0.35f * 0.35f)
                Repath();
            Vector3 direction = toWaypoint.sqrMagnitude > 0.0001f ? toWaypoint.normalized : toTarget / distance;
            velocity = direction * stats.moveSpeed;
        }
        // Écartement progressif : pas pendant le geste d'attaque, pour ne pas glisser en lançant le sort.
        if (!casting)
            velocity = Vector3.ClampMagnitude(velocity + Separation() * separationStrength, stats.moveSpeed * 1.2f);

        Vector3 start = transform.position;
        Vector3 next = start + velocity * Time.deltaTime;
        next = ResolveOverlaps(next);
        next = EnvironmentCollision.Resolve(capsule, next);
        transform.position = bounds != null ? bounds.Clamp(next, 0.5f) : next;
        Vector3 moved = transform.position - start;
        moved.y = 0f;
        return moved;
    }

    // Direction d'écartement (pondérée par la proximité) par rapport aux ennemis trop proches.
    private Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (EnemyController other in Active)
        {
            if (other == this || other.health.IsDead)
                continue;
            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d >= separationRadius)
                continue;
            // Deux ennemis au même point : direction arbitraire mais stable.
            Vector3 dir = d > 0.001f ? away / d : Quaternion.Euler(0f, fallbackAngle, 0f) * Vector3.forward;
            push += dir * (1f - d / separationRadius);
        }
        return push;
    }

    // Contrainte dure : deux capsules ne se chevauchent jamais (chacun corrige sa moitié de l'interpénétration).
    private Vector3 ResolveOverlaps(Vector3 position)
    {
        float radius = Radius();
        foreach (EnemyController other in Active)
        {
            if (other == this || other.health.IsDead)
                continue;
            Vector3 away = position - other.transform.position;
            away.y = 0f;
            float minDistance = radius + other.Radius();
            float d = away.magnitude;
            if (d >= minDistance)
                continue;
            Vector3 dir = d > 0.001f ? away / d : Quaternion.Euler(0f, fallbackAngle, 0f) * Vector3.forward;
            position += dir * (minDistance - d) * 0.5f;
        }
        return position;
    }

    private float Radius()
    {
        return capsule != null ? capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.5f;
    }

    // Lance le geste ; l'attaque elle-même part au moment fort de l'animation.
    private void StartAttack()
    {
        bool laser = ability is LaserAbility;
        if (animator != null)
            animator.SetTrigger(laser ? LaserId : FireballId);
        float windup = laser ? laserWindup : fireballWindup;
        castAt = Time.time + windup;
        pausedUntil = castAt + stats.attackPause;
        nextAttackTime = castAt + UnityEngine.Random.Range(stats.attackInterval.x, stats.attackInterval.y);
    }

    private void Cast()
    {
        castAt = -1f;
        // Vise là où le joueur sera au moment de l'impact (si l'anticipation est activée).
        Vector3 aim = AimPredictor.Aim(transform.position, target, targetMotion, ability, aimPrediction, bounds);
        Vector3 toAim = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
        if (toAim.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toAim);

        ability.Cast(new AbilityContext
        {
            Caster = gameObject,
            Origin = transform.position,
            AimPoint = aim,
        });
    }

    // Vitesse réelle (déplacement + écartement) envoyée à l'Animator.
    private void Animate()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        if (animator == null || Time.deltaTime <= 0f)
            return;

        float speed = delta.magnitude / Time.deltaTime;
        animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);
        animator.SetFloat(WalkSpeedId, Mathf.Clamp(speed / walkAnimationSpeed, 0.6f, 2f));
    }
}
