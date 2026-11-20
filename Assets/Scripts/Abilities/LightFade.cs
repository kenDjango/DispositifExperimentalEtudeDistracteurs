using UnityEngine;

// Fait décroître l'intensité d'une lumière jusqu'à 0 (flash d'explosion).
[RequireComponent(typeof(Light))]
public class LightFade : MonoBehaviour
{
    [SerializeField] private float duration = 0.35f;

    private Light lightSource;
    private float startIntensity;
    private float elapsed;

    // Au Start et non à l'Awake : l'intensité peut être réglée juste après l'Instantiate (effet atténué).
    private void Start()
    {
        lightSource = GetComponent<Light>();
        startIntensity = lightSource.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        lightSource.intensity = Mathf.Lerp(startIntensity, 0f, elapsed / duration);
    }
}
