using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Gère les vagues d'ennemis : première apparition, remplacement des ennemis tués, type d'attaque, effet de téléportation.
public class EnemyManager : MonoBehaviour
{
    [Header("Apparition")]
    // Délai entre le début de la partie et l'arrivée des premiers ennemis.
    [SerializeField] private float firstSpawnDelay = 3f;
    [SerializeField] private int enemyCount = 3;
    // Écart entre les apparitions de la première vague.
    [SerializeField] private float firstWaveStagger = 0.35f;
    // Délai avant qu'un ennemi tué soit remplacé.
    [SerializeField] private float respawnDelay = 5f;
    // Distance au joueur (min, max) du point d'apparition.
    [SerializeField] private Vector2 spawnDistance = new Vector2(6f, 11f);
    [SerializeField] private MovementBounds spawnArea;

    [Header("Téléportation")]
    [SerializeField] private GameObject teleportVfx;
    // Temps entre le début de l'effet et l'apparition de l'ennemi (synchronisé avec le flash de l'effet).
    [SerializeField] private float materializeDelay = 0.5f;

    [Header("Ennemis")]
    [SerializeField] private EnemyController enemyPrefab;
    [SerializeField] private EnemyStats stats = new EnemyStats();

    [Header("Types d'attaque")]
    // Probabilité qu'un nouvel ennemi attaque au laser (sinon boule de feu).
    [SerializeField, Range(0f, 1f)] private float laserChance = 0.3f;
    [SerializeField] private Ability fireballAbility;
    [SerializeField] private Ability laserAbility;
    [SerializeField] private Color fireballColor = new Color(0.85f, 0.25f, 0.12f);
    [SerializeField] private Color laserColor = new Color(0.2f, 0.45f, 0.95f);

    [Header("Butin")]
    // Recharge de dash laissée au sol par les ennemis laser vaincus.
    [SerializeField] private DashRecharge dashRechargePrefab;
    [SerializeField, Range(0f, 1f)] private float dashRechargeDropChance = 1f;

    [Header("Joueur")]
    [SerializeField] private Transform player;

    private readonly List<EnemyController> alive = new List<EnemyController>();
    private bool gameOver;

    public IReadOnlyList<EnemyController> Alive => alive;
    // Taille et luminosité de l'effet de téléportation (1 : effet normal), réglées par le système de distraction.
    // Le délai avant l'apparition du robot ne change pas.
    public float TeleportVfxScale { get; set; } = 1f;
    // Visée anticipée des ennemis (réglée par le GameManager ; null = visée directe).
    public AimPredictionSettings AimPrediction { get; set; }

    // Ennemi tué pendant la partie (pour le score).
    public event System.Action<EnemyController> EnemyKilled;

    [Header("Navigation")]
    // Carte de navigation de l'arène, calculée dans l'éditeur (Tools > Game Loop > Bake Arena Navigation).
    [SerializeField] private NavMeshData navigationData;
    // Marge ajoutée au rayon des robots autour des obstacles de la carte de navigation.
    [SerializeField] private float navigationMargin = 0.1f;

    private void Awake()
    {
        // Carte de navigation de l'arène, avant les premières apparitions.
        if (navigationData == null)
        {
            Debug.LogWarning("Aucune carte de navigation enregistrée : calcul au lancement (incomplet dans le jeu exporté). Lancez Tools > Game Loop > Bake Arena Navigation.");
            ArenaNavigation.Use(CreateNavigationData());
        }
        else
        {
            ArenaNavigation.Use(navigationData);
        }
    }

    // Carte de navigation pour ces robots (rayon et hauteur de leur capsule) dans la zone d'apparition.
    public NavMeshData CreateNavigationData()
    {
        var capsule = enemyPrefab != null ? enemyPrefab.GetComponent<CapsuleCollider>() : null;
        float scale = enemyPrefab != null ? enemyPrefab.transform.localScale.x : 1f;
        float radius = capsule != null ? capsule.radius * scale : 0.5f;
        float height = capsule != null ? capsule.height * scale : 2f;
        return ArenaNavigation.Create(spawnArea, radius + navigationMargin, height);
    }

    private void OnDestroy()
    {
        ArenaNavigation.Clear();
    }

    private void Start()
    {
        StartCoroutine(FirstWave());
    }

    // Fin de partie (appelé par le GameManager) : plus aucune apparition.
    public void StopSpawning()
    {
        gameOver = true;
    }

    private IEnumerator FirstWave()
    {
        yield return new WaitForSeconds(firstSpawnDelay);
        for (int i = 0; i < enemyCount; i++)
        {
            StartCoroutine(SpawnRoutine(0f));
            yield return new WaitForSeconds(firstWaveStagger);
        }
    }

    private IEnumerator SpawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector3 point = PickSpawnPoint();
        if (!gameOver)
            PlayTeleportVfx(point);
        yield return new WaitForSeconds(materializeDelay);

        if (!gameOver)
            Spawn(point);
    }

    // Effet de téléportation au sol en point, à la taille réglée par le système de distraction (aussi joué sur le
    // joueur à sa réapparition).
    public void PlayTeleportVfx(Vector3 point)
    {
        if (teleportVfx == null)
            return;
        point.y = 0f;
        GameObject vfx = Instantiate(teleportVfx, point, Quaternion.identity);
        // Particules à l'échelle de la hiérarchie : réduire l'objet réduit tout l'effet.
        vfx.transform.localScale *= TeleportVfxScale;
        foreach (Light light in vfx.GetComponentsInChildren<Light>())
            light.intensity *= TeleportVfxScale;
    }

    private void Spawn(Vector3 groundPoint)
    {
        bool laser = Random.value < laserChance;
        Ability ability = laser ? laserAbility : fireballAbility;
        Color color = laser ? laserColor : fireballColor;

        // Le pivot de la capsule est en son centre : on la pose sur le sol.
        Vector3 position = groundPoint + Vector3.up * EnemyHalfHeight();
        Vector3 toPlayer = player != null ? Vector3.ProjectOnPlane(player.position - position, Vector3.up) : Vector3.zero;
        Quaternion facing = toPlayer.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

        EnemyController enemy = Instantiate(enemyPrefab, position, facing);
        enemy.name = laser ? "Enemy_Laser" : "Enemy_Fireball";
        enemy.Init(ability, color, stats, player, spawnArea, AimPrediction);
        enemy.Health.Died += () => OnEnemyDied(enemy);
        alive.Add(enemy);
    }

    private void OnEnemyDied(EnemyController enemy)
    {
        alive.Remove(enemy);
        if (gameOver)
            return;
        EnemyKilled?.Invoke(enemy);
        if (enemy.Ability is LaserAbility)
            DropDashRecharge(enemy.transform.position);
        StartCoroutine(SpawnRoutine(respawnDelay));
    }

    private void DropDashRecharge(Vector3 position)
    {
        if (dashRechargePrefab == null || Random.value >= dashRechargeDropChance)
            return;
        position.y = 0f;
        DashRecharge recharge = Instantiate(dashRechargePrefab, position, Quaternion.identity);
        recharge.Init(player);
    }

    private Vector3 PickSpawnPoint()
    {
        Vector3 center = player != null ? player.position : transform.position;
        Vector3 best = center;
        float bestScore = float.MinValue;

        // Plusieurs essais : on garde le point le plus éloigné des autres ennemis (évite les apparitions groupées).
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(spawnDistance.x, spawnDistance.y);
            Vector3 candidate = center + new Vector3(dir.x, 0f, dir.y) * distance;
            if (spawnArea != null)
                candidate = spawnArea.Clamp(candidate, 1f);
            candidate.y = 0f;

            float score = Vector3.Distance(candidate, new Vector3(center.x, 0f, center.z)) >= spawnDistance.x * 0.8f ? 0f : -100f;
            // Jamais dans un obstacle du décor.
            if (EnvironmentCollision.IsBlocked(candidate, 0.8f, 2f))
                score -= 1000f;
            foreach (EnemyController enemy in alive)
                if (enemy != null)
                    score += Mathf.Min(6f, Vector3.Distance(candidate, enemy.transform.position));

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private float EnemyHalfHeight()
    {
        var capsule = enemyPrefab.GetComponent<CapsuleCollider>();
        return capsule != null ? capsule.height * 0.5f * enemyPrefab.transform.localScale.y : 1f;
    }
}
