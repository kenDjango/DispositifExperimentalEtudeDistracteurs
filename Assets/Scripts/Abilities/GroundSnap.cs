using UnityEngine;

// Pose l'objet sur le sol à son apparition (onde de choc, trace de brûlure).
public class GroundSnap : MonoBehaviour
{
    [SerializeField] private float groundHeight = 0f;
    // Petit décalage pour éviter que l'effet ne se confonde avec le sol.
    [SerializeField] private float offset = 0.02f;

    private void Awake()
    {
        Vector3 position = transform.position;
        position.y = groundHeight + offset;
        transform.position = position;
    }
}
