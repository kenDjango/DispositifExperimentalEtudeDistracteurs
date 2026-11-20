using UnityEngine;

// Zone rectangulaire (plan XZ) qui limite un déplacement. Visible en vert dans la Scene view.
public class MovementBounds : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(50f, 50f);
    [SerializeField] private Color gizmoColor = Color.green;

    public Vector2 Size => size;

    // Point tiré au hasard dans la zone (à au moins margin des bords).
    public Vector3 RandomPoint(float margin = 0f)
    {
        float halfX = Mathf.Max(0f, size.x * 0.5f - margin);
        float halfZ = Mathf.Max(0f, size.y * 0.5f - margin);
        return transform.position + new Vector3(Random.Range(-halfX, halfX), 0f, Random.Range(-halfZ, halfZ));
    }

    public Vector3 Clamp(Vector3 position, float margin = 0f)
    {
        Vector3 c = transform.position;
        float halfX = Mathf.Max(0f, size.x * 0.5f - margin);
        float halfZ = Mathf.Max(0f, size.y * 0.5f - margin);
        position.x = Mathf.Clamp(position.x, c.x - halfX, c.x + halfX);
        position.z = Mathf.Clamp(position.z, c.z - halfZ, c.z + halfZ);
        return position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0f, size.y));
    }
}
