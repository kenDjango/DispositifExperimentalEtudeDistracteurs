using UnityEngine;

// Fait tourner l'objet en continu (gyrophares, portail, hologrammes).
public class Spinner : MonoBehaviour
{
    [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);

    // Multiplicateur de la rotation (0 : arrêt), réglé par le système de distraction.
    public float Level { get; set; } = 1f;

    private void Update()
    {
        transform.Rotate(degreesPerSecond * (Level * Time.deltaTime), Space.Self);
    }
}
