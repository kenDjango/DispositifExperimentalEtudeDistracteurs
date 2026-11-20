using UnityEngine;

// Image rémanente : copie du personnage qui s'estompe puis disparaît.
public class DashGhost : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private Color color;
    private Vector3 startScale;
    private float lifetime;
    private float elapsed;
    // Mesh figé créé pour cette image, détruit avec elle.
    private Mesh ownedMesh;

    private void OnDestroy()
    {
        if (ownedMesh != null)
            Destroy(ownedMesh);
    }

    public void Init(Color color, float lifetime, Mesh ownedMesh = null)
    {
        this.ownedMesh = ownedMesh;
        this.color = color;
        this.lifetime = lifetime;
        meshRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        startScale = transform.localScale;
        Apply(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float k = elapsed / lifetime;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(k);
    }

    private void Apply(float k)
    {
        Color c = color;
        c.a *= 1f - k;
        block.SetColor(BaseColorId, c);
        meshRenderer.SetPropertyBlock(block);
        transform.localScale = startScale * Mathf.Lerp(1f, 0.85f, k);
    }
}
