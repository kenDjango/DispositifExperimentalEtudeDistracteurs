using UnityEngine;

// Flotte lentement de haut en bas en tournant sur lui-même (spécimens dans les cuves).
public class Bobbing : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.12f;
    [SerializeField] private float period = 3.5f;
    [SerializeField] private float turnSpeed = 8f;

    private Vector3 startPosition;
    private float phase;

    // Multiplicateur du mouvement (0 : immobile), réglé par le système de distraction.
    public float Level { get; set; } = 1f;

    private void Awake()
    {
        startPosition = transform.localPosition;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * Mathf.PI * 2f / period + phase) * amplitude * Level;
        transform.localPosition = startPosition + Vector3.up * offset;
        transform.Rotate(0f, turnSpeed * Level * Time.deltaTime, 0f, Space.World);
    }
}
