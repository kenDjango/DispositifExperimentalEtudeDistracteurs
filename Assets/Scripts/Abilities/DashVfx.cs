using UnityEngine;

// Effet de super-vitesse : traînées, images rémanentes du personnage et éclairs qui crépitent autour.
public class DashVfx : MonoBehaviour
{
    [Header("Images rémanentes")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField, ColorUsage(true, true)] private Color ghostColor = new Color(1f, 0.4f, 0.05f, 0.45f);
    // Distance parcourue entre deux images rémanentes.
    [SerializeField] private float ghostSpacing = 0.7f;
    [SerializeField] private float ghostLifetime = 0.3f;

    [Header("Éclairs")]
    [SerializeField] private LineRenderer[] bolts;
    // Halo large de chaque éclair (même tracé que l'éclair de même index).
    [SerializeField] private LineRenderer[] boltGlows;
    [SerializeField] private float boltRadius = 0.9f;
    [SerializeField] private int boltSegments = 7;
    [SerializeField] private float boltJitter = 0.18f;
    [SerializeField] private float boltRefresh = 0.035f;

    // Temps laissé aux traînées pour s'estomper après la fin du dash.
    [SerializeField] private float lingerTime = 0.5f;

    private Transform target;
    private Mesh ghostMesh;
    // Personnage animé : la pose courante est figée dans chaque image rémanente.
    private SkinnedMeshRenderer skinnedMesh;
    private Vector3 lastGhostPosition;
    private float boltTimer;
    private bool active;

    public void Begin(Transform target)
    {
        this.target = target;
        skinnedMesh = target.GetComponentInChildren<SkinnedMeshRenderer>();
        MeshFilter filter = target.GetComponent<MeshFilter>();
        ghostMesh = filter != null ? filter.sharedMesh : null;
        lastGhostPosition = target.position;
        active = true;
        RefreshBolts();
    }

    public void End()
    {
        active = false;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>())
            trail.emitting = false;
        foreach (LineRenderer bolt in bolts)
            bolt.enabled = false;
        foreach (LineRenderer glow in boltGlows)
            glow.enabled = false;

        Destroy(gameObject, lingerTime);
    }

    private void LateUpdate()
    {
        if (!active || target == null)
            return;

        transform.SetPositionAndRotation(target.position, target.rotation);

        // Images posées à intervalle régulier sur le trajet, derrière le personnage.
        Vector3 travelled = target.position - lastGhostPosition;
        int count = Mathf.FloorToInt(travelled.magnitude / ghostSpacing);
        for (int i = 1; i <= count; i++)
            SpawnGhost(lastGhostPosition + travelled.normalized * ghostSpacing * i);
        if (count > 0)
            lastGhostPosition += travelled.normalized * ghostSpacing * count;

        boltTimer -= Time.deltaTime;
        if (boltTimer <= 0f)
        {
            boltTimer = boltRefresh;
            RefreshBolts();
        }
    }

    private void SpawnGhost(Vector3 position)
    {
        if (ghostMaterial == null)
            return;

        Mesh mesh = ghostMesh;
        Transform source = target;
        if (skinnedMesh != null)
        {
            mesh = new Mesh();
            skinnedMesh.BakeMesh(mesh);
            source = skinnedMesh.transform;
        }
        if (mesh == null)
            return;

        var go = new GameObject("DashGhost");
        go.transform.SetPositionAndRotation(position + (source.position - target.position), source.rotation);
        go.transform.localScale = source.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = ghostMaterial;
        renderer.sharedMaterials = materials;
        renderer.sortingOrder = 9;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<DashGhost>().Init(ghostColor, ghostLifetime, skinnedMesh != null ? mesh : null);
    }

    // Redessine chaque éclair : soit un arc autour du corps, soit une décharge qui part vers l'arrière.
    private void RefreshBolts()
    {
        for (int i = 0; i < bolts.Length; i++)
        {
            LineRenderer bolt = bolts[i];
            bolt.enabled = true;

            Vector3 from = transform.position + Random.insideUnitSphere * boltRadius * 0.5f;
            Vector3 to = i % 2 == 0
                ? transform.position - transform.forward * Random.Range(1f, 2.2f) + Random.insideUnitSphere * 0.4f
                : transform.position + Random.onUnitSphere * boltRadius;

            bolt.positionCount = boltSegments;
            for (int s = 0; s < boltSegments; s++)
            {
                float k = s / (float)(boltSegments - 1);
                Vector3 p = Vector3.Lerp(from, to, k);
                if (s > 0 && s < boltSegments - 1)
                    p += Random.insideUnitSphere * boltJitter;
                bolt.SetPosition(s, p);
            }

            if (i < boltGlows.Length)
            {
                LineRenderer glow = boltGlows[i];
                glow.enabled = true;
                glow.positionCount = boltSegments;
                for (int s = 0; s < boltSegments; s++)
                    glow.SetPosition(s, bolt.GetPosition(s));
            }
        }
    }
}
