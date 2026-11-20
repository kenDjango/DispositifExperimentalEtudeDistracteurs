using UnityEngine;

// Arcs électriques qui crépitent le long des anneaux d'un cercle de transmutation posé à plat, centré sur l'objet
// (la réaction alchimique de Fullmetal Alchemist). Chaque arc est redessiné au hasard à intervalles très courts.
public class AlchemyArcs : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private Color color = new Color(0.6f, 0.85f, 1f);
    // Rayons des anneaux (en mètres) que les arcs peuvent suivre.
    [SerializeField] private float[] radii = { 1f };
    [SerializeField] private int arcCount = 5;
    [SerializeField] private float width = 0.05f;
    // Étendue angulaire d'un arc (min, max), en degrés.
    [SerializeField] private Vector2 arcAngle = new Vector2(15f, 50f);
    // Écart radial des points de l'arc, relatif au rayon.
    [SerializeField] private float jitter = 0.04f;
    [SerializeField] private Vector2 refreshInterval = new Vector2(0.04f, 0.14f);
    // Probabilité qu'un arc soit visible à chaque rafraîchissement (donne le crépitement).
    [SerializeField, Range(0f, 1f)] private float activeChance = 0.55f;
    // Probabilité qu'un arc saute d'un anneau à l'autre au lieu de suivre un anneau.
    [SerializeField, Range(0f, 1f)] private float jumpChance = 0.25f;

    private const int Points = 12;

    private LineRenderer[] lines;
    private float[] timers;

    private void Awake()
    {
        lines = new LineRenderer[arcCount];
        timers = new float[arcCount];
        for (int i = 0; i < arcCount; i++)
        {
            var go = new GameObject("Arc");
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = Points;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            lines[i] = line;
            timers[i] = Random.Range(0f, refreshInterval.y);
        }
    }

    private void Update()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            timers[i] -= Time.deltaTime;
            if (timers[i] > 0f)
                continue;
            timers[i] = Random.Range(refreshInterval.x, refreshInterval.y);
            Redraw(lines[i]);
        }
    }

    private void Redraw(LineRenderer line)
    {
        line.enabled = radii.Length > 0 && Random.value < activeChance;
        if (!line.enabled)
            return;

        float r0 = radii[Random.Range(0, radii.Length)];
        float r1 = Random.value < jumpChance ? radii[Random.Range(0, radii.Length)] : r0;
        float start = Random.Range(0f, 360f);
        float span = Random.Range(arcAngle.x, arcAngle.y) * (r0 == r1 ? 1f : 0.3f);
        for (int k = 0; k < Points; k++)
        {
            float t = k / (float)(Points - 1);
            float angle = (start + span * t) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(r0, r1, t);
            bool end = k == 0 || k == Points - 1;
            radius += end ? 0f : Random.Range(-jitter, jitter) * radius;
            line.SetPosition(k, new Vector3(Mathf.Cos(angle) * radius, Random.Range(0.02f, 0.08f), Mathf.Sin(angle) * radius));
        }

        Color c = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, Random.Range(0.4f, 1f));
        line.widthMultiplier = width * Random.Range(0.6f, 1.3f);
    }
}
