using UnityEngine;

// Fait vaciller une lumière comme une flamme.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    // Variation relative de l'intensité (0.3 = ±30 %).
    [SerializeField] private float amount = 0.3f;
    [SerializeField] private float speed = 12f;

    private Light lightSource;
    private float baseIntensity;
    private float seed;

    // Réglages extérieurs (système de distraction) : multiplicateurs de l'intensité et de l'amplitude du vacillement.
    public float Level { get; set; } = 1f;
    public float AmountScale { get; set; } = 1f;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
        // Amplitude renforcée (au-delà de ±100 %) : l'intensité ne descend pas sous zéro.
        lightSource.intensity = baseIntensity * Level * Mathf.Max(0f, 1f + noise * amount * AmountScale);
    }
}
