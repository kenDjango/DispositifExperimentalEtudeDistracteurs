using System.Collections.Generic;
using UnityEngine;

// Essaim d'araignées mécaniques décoratives : chacune suit un robot ennemi en vie (au plus maxPerRobot par robot)
// en tournant lentement autour de lui. Quand son robot meurt, elle en rejoint un autre ; s'il n'y en a aucun de
// libre, elle erre dans l'arène jusqu'à ce qu'un robot apparaisse.
// Le nombre d'araignées présentes est réglable en cours de partie (système de distraction) : une araignée
// n'apparaît et ne disparaît que hors du champ de la caméra ; celle qui doit partir y marche d'abord.
public class SpiderSwarm : MonoBehaviour
{
    [SerializeField] private SpiderAnimator spiderPrefab;
    // Araignées créées (maximum du palier High) : 4 robots × 3 par robot.
    [SerializeField] private int count = 12;
    [SerializeField] private EnemyManager enemies;
    // Zone d'apparition et d'errance.
    [SerializeField] private MovementBounds area;

    [Header("Suivi")]
    [SerializeField] private int maxPerRobot = 3;
    // Distance au robot (aléatoire entre x et y, propre à chaque araignée).
    [SerializeField] private Vector2 followDistance = new Vector2(0.9f, 1.5f);
    // Rotation des araignées autour de leur robot (degrés par seconde).
    [SerializeField] private float orbitSpeed = 25f;
    [SerializeField] private float followSpeed = 3.5f;

    [Header("Errance")]
    [SerializeField] private float roamSpeed = 0.9f;
    // Pause (aléatoire entre x et y) à chaque point atteint.
    [SerializeField] private Vector2 roamPause = new Vector2(1f, 4f);

    [Header("Déplacement")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float turnSpeed = 540f;
    // Encombrement de l'araignée pour les collisions avec le décor.
    [SerializeField] private float radius = 0.2f;
    // Sans progrès vers son but pendant ce temps, l'araignée change de but.
    [SerializeField] private float stuckTime = 1.5f;

    [Header("Arrivées et départs")]
    // Vitesse d'une araignée qui quitte le champ de la caméra.
    [SerializeField] private float leaveSpeed = 3.5f;
    // Marge autour de l'écran (fraction de sa taille) : en deçà, l'araignée est considérée comme visible.
    [SerializeField] private float offscreenMargin = 0.08f;

    // Écarts (degrés) essayés autour de la direction voulue quand le décor barre la route.
    private static readonly float[] DetourAngles = { 0f, 35f, 70f, 105f, 140f, 175f };

    private class Spider
    {
        public Transform Transform;
        public EnemyController Target;
        public float Distance;
        public float AngleOffset;
        public Vector3 Velocity;
        public Vector3 RoamPoint;
        public float PauseTimer;
        // Côté du contournement (+1 : gauche, -1 : droite), gardé tant qu'il marche pour longer l'obstacle.
        public float DetourSide;
        public float StuckTimer;
        // Absente : objet désactivé. Sur le départ : marche hors du champ de la caméra avant de disparaître.
        public bool Present = true;
        public bool Leaving;
        public Vector3 ExitPoint;
        public float ExitTimer;
    }

    private readonly List<Spider> spiders = new List<Spider>();
    private readonly Dictionary<EnemyController, List<Spider>> followers = new Dictionary<EnemyController, List<Spider>>();
    private float orbitAngle;
    // Nombre d'araignées voulu ; appliqué sans transition au prochain Update si placeNow.
    private int wanted = -1;
    private bool placeNow;

    public int Capacity => count;

    // Nombre d'araignées présentes voulu. instant : appliqué tout de suite (début de partie), sinon les araignées
    // arrivent et partent hors du champ de la caméra.
    public void SetPresentCount(int value, bool instant)
    {
        wanted = Mathf.Clamp(value, 0, count);
        placeNow |= instant;
    }

    private void Start()
    {
        if (wanted < 0)
            wanted = count;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = FreePoint();
            SpiderAnimator spider = Instantiate(spiderPrefab, point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            spider.name = $"MechSpider_{i}";
            spiders.Add(new Spider
            {
                Transform = spider.transform,
                Distance = Random.Range(followDistance.x, followDistance.y),
                AngleOffset = Random.Range(-20f, 20f),
                RoamPoint = point,
                PauseTimer = Random.Range(roamPause.x, roamPause.y),
                DetourSide = Random.value < 0.5f ? 1f : -1f,
            });
        }
        placeNow = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;
        orbitAngle += orbitSpeed * dt;

        UpdatePopulation();
        AssignTargets();
        foreach (Spider spider in spiders)
        {
            if (!spider.Present)
                continue;
            if (spider.Leaving)
                Leave(spider, dt);
            else if (spider.Target != null)
                Follow(spider, dt);
            else
                Roam(spider, dt);
        }
    }

    // ---------- Arrivées et départs ----------

    private void UpdatePopulation()
    {
        if (placeNow)
        {
            placeNow = false;
            for (int i = 0; i < spiders.Count; i++)
                SetPresent(spiders[i], i < wanted);
            return;
        }

        int staying = 0;
        foreach (Spider spider in spiders)
            if (spider.Present && !spider.Leaving)
                staying++;

        if (staying > wanted)
        {
            // De préférence une araignée déjà hors champ (elle disparaît aussitôt), sinon la première venue.
            Spider leaving = null;
            foreach (Spider spider in spiders)
                if (spider.Present && !spider.Leaving && (leaving == null || !Visible(spider.Transform.position)))
                    leaving = spider;
            leaving.Leaving = true;
            leaving.Target = null;
            leaving.ExitTimer = 0f;
        }
        else if (staying < wanted)
        {
            // Une araignée sur le départ fait demi-tour ; sinon, une nouvelle arrive hors champ.
            Spider back = spiders.Find(s => s.Present && s.Leaving);
            if (back != null)
            {
                back.Leaving = false;
                return;
            }
            Spider absent = spiders.Find(s => !s.Present);
            if (absent != null && OffscreenPoint(absent.Transform.position, out Vector3 point))
            {
                absent.Transform.position = point;
                absent.RoamPoint = point;
                absent.Velocity = Vector3.zero;
                SetPresent(absent, true);
            }
        }
    }

    private static void SetPresent(Spider spider, bool present)
    {
        spider.Present = present;
        spider.Leaving = false;
        spider.Target = null;
        spider.Transform.gameObject.SetActive(present);
    }

    // Marche vers un point hors champ (recalculé chaque seconde : la caméra suit le joueur) et disparaît dès qu'elle
    // n'est plus visible. Sans point hors champ, elle attend au bord de l'arène.
    private void Leave(Spider spider, float dt)
    {
        if (!Visible(spider.Transform.position))
        {
            SetPresent(spider, false);
            return;
        }
        spider.ExitTimer -= dt;
        if (spider.ExitTimer <= 0f)
        {
            spider.ExitTimer = 1f;
            if (!OffscreenPoint(spider.Transform.position, out spider.ExitPoint))
                spider.ExitPoint = NearestEdge(spider.Transform.position);
        }
        if (!Move(spider, spider.ExitPoint, leaveSpeed, null, dt))
            spider.ExitTimer = 0f;
    }

    private bool Visible(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        Vector3 v = cam.WorldToViewportPoint(position + Vector3.up * 0.2f);
        return v.z > 0f && v.x > -offscreenMargin && v.x < 1f + offscreenMargin && v.y > -offscreenMargin && v.y < 1f + offscreenMargin;
    }

    // Point libre hors champ, le plus proche de from parmi quelques tirages.
    private bool OffscreenPoint(Vector3 from, out Vector3 point)
    {
        point = from;
        float best = float.MaxValue;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector3 candidate = FreePoint();
            float d = Flat(candidate - from).sqrMagnitude;
            if (d < best && !Visible(candidate))
            {
                best = d;
                point = candidate;
            }
        }
        return best < float.MaxValue;
    }

    private Vector3 NearestEdge(Vector3 position)
    {
        if (area == null)
            return position;
        Vector3 c = area.transform.position;
        float halfX = area.Size.x * 0.5f - radius, halfZ = area.Size.y * 0.5f - radius;
        float dx = halfX - Mathf.Abs(position.x - c.x), dz = halfZ - Mathf.Abs(position.z - c.z);
        if (dx < dz)
            position.x = c.x + Mathf.Sign(position.x - c.x) * halfX;
        else
            position.z = c.z + Mathf.Sign(position.z - c.z) * halfZ;
        return position;
    }

    // ---------- Répartition ----------

    private void AssignTargets()
    {
        followers.Clear();
        IReadOnlyList<EnemyController> alive = enemies != null ? enemies.Alive : null;
        if (alive != null)
            foreach (EnemyController enemy in alive)
                if (enemy != null && !enemy.Health.IsDead)
                    followers[enemy] = new List<Spider>();

        // Robot mort (ou limite dépassée) : l'araignée est libérée. Absente ou sur le départ : elle ne suit personne.
        foreach (Spider spider in spiders)
        {
            if (!spider.Present || spider.Leaving || spider.Target == null || !followers.TryGetValue(spider.Target, out List<Spider> group) || group.Count >= maxPerRobot)
            {
                spider.Target = null;
                continue;
            }
            group.Add(spider);
        }

        // Araignées libres : le robot le moins suivi, puis le plus proche.
        foreach (Spider spider in spiders)
        {
            if (!spider.Present || spider.Leaving || spider.Target != null)
                continue;
            EnemyController best = null;
            int bestCount = maxPerRobot;
            float bestDistance = float.MaxValue;
            foreach (KeyValuePair<EnemyController, List<Spider>> pair in followers)
            {
                int n = pair.Value.Count;
                float d = (pair.Key.transform.position - spider.Transform.position).sqrMagnitude;
                if (n < bestCount || (n == bestCount && n < maxPerRobot && d < bestDistance))
                {
                    best = pair.Key;
                    bestCount = n;
                    bestDistance = d;
                }
            }
            if (best == null)
                continue;
            spider.Target = best;
            followers[best].Add(spider);
        }
    }

    // ---------- Déplacement ----------

    // Place autour du robot : les araignées d'un même robot se répartissent sur le cercle, qui tourne lentement.
    private void Follow(Spider spider, float dt)
    {
        List<Spider> group = followers[spider.Target];
        int slot = group.IndexOf(spider);
        float angle = orbitAngle + slot * 360f / group.Count + spider.AngleOffset;
        Vector3 robot = spider.Target.transform.position;
        robot.y = spider.Transform.position.y;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spider.Distance;
        // Place prise par le décor (robot contre un mur) : on se rapproche du robot jusqu'à trouver de la place.
        Vector3 goal = robot + offset;
        for (int i = 1; i <= 4 && !Free(goal); i++)
            goal = robot + offset * (1f - i * 0.2f);
        if (!Move(spider, goal, followSpeed, robot, dt))
        {
            // Bloquée : autre place autour du robot, contournement par l'autre côté.
            spider.AngleOffset = Random.Range(90f, 270f);
            spider.DetourSide = -spider.DetourSide;
        }
    }

    private void Roam(Spider spider, float dt)
    {
        Vector3 position = spider.Transform.position;
        if (Flat(spider.RoamPoint - position).magnitude < 0.15f)
        {
            spider.PauseTimer -= dt;
            if (spider.PauseTimer <= 0f)
            {
                spider.RoamPoint = FreePoint();
                spider.PauseTimer = Random.Range(roamPause.x, roamPause.y);
            }
        }
        if (!Move(spider, spider.RoamPoint, roamSpeed, null, dt))
            spider.RoamPoint = FreePoint();
    }

    // Avance vers le but en ralentissant à l'arrivée ; contourne le décor en le longeant et, à l'arrêt, se tourne
    // vers lookAt. Renvoie faux quand l'araignée n'arrive plus à progresser (il faut lui donner un autre but).
    private bool Move(Spider spider, Vector3 goal, float maxSpeed, Vector3? lookAt, float dt)
    {
        Transform t = spider.Transform;
        Vector3 position = t.position;
        Vector3 toGoal = Flat(goal - position);
        float distance = toGoal.magnitude;
        Vector3 desired = distance > 0.05f ? toGoal / distance * Mathf.Min(maxSpeed, distance * 3f) : Vector3.zero;

        // Déjà dans un obstacle (poussée par le bord de l'arène...) : elle en sort sans tenir compte des collisions.
        bool inside = !Free(position);
        if (desired != Vector3.zero && !inside && !TryDetour(spider, position, ref desired, dt))
            desired = Vector3.zero;
        spider.Velocity = Vector3.MoveTowards(spider.Velocity, desired, acceleration * dt);

        Vector3 next = position + spider.Velocity * dt;
        if (area != null)
            next = area.Clamp(next, radius);
        if (!inside && !Free(next))
        {
            // La vitesse lissée bute encore sur l'obstacle : on prend directement la direction de contournement.
            spider.Velocity = desired;
            next = position + desired * dt;
            if (area != null)
                next = area.Clamp(next, radius);
            if (!Free(next))
            {
                next = position;
                spider.Velocity = Vector3.zero;
            }
        }
        t.position = next;

        // Pas de progrès alors que le but est encore loin.
        bool stalled = distance > 0.3f && Flat(next - position).magnitude < maxSpeed * 0.15f * dt;
        spider.StuckTimer = stalled ? spider.StuckTimer + dt : 0f;
        bool progressing = spider.StuckTimer < stuckTime;
        if (!progressing)
            spider.StuckTimer = 0f;

        Vector3 facing = spider.Velocity.sqrMagnitude > 0.04f ? spider.Velocity
                       : lookAt.HasValue ? Flat(lookAt.Value - next)
                       : Vector3.zero;
        if (facing.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(facing), turnSpeed * dt);
        return progressing;
    }

    // Première direction libre en s'écartant de plus en plus de la direction voulue, du côté du contournement en
    // cours d'abord (l'araignée longe ainsi l'obstacle au lieu d'osciller), puis de l'autre.
    private bool TryDetour(Spider spider, Vector3 position, ref Vector3 desired, float dt)
    {
        float speed = desired.magnitude;
        // Le pas lui-même doit être libre, et un peu plus loin aussi pour anticiper l'obstacle (un point libre au
        // loin ne suffit pas : au coin d'un obstacle, le chemin pour y aller peut être barré).
        float step = speed * dt;
        float probe = Mathf.Max(step, radius);
        foreach (float angle in DetourAngles)
            for (int s = 0; s < (angle == 0f ? 1 : 2); s++)
            {
                float side = s == 0 ? spider.DetourSide : -spider.DetourSide;
                Vector3 direction = Quaternion.Euler(0f, angle * side, 0f) * (desired / speed);
                if (!Free(position + direction * step) || !Free(position + direction * probe))
                    continue;
                if (angle > 0f)
                    spider.DetourSide = side;
                desired = direction * speed;
                return true;
            }
        return false;
    }

    private bool Free(Vector3 point) => !EnvironmentCollision.IsBlocked(point, radius, radius * 2f);

    // Point libre de l'arène (hors du décor).
    private Vector3 FreePoint()
    {
        Vector3 origin = area != null ? area.transform.position : transform.position;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 candidate = area != null ? area.RandomPoint(1f) : origin + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
            if (Free(candidate))
                return candidate;
        }
        return origin;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
