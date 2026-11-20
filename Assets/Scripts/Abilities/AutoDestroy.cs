using UnityEngine;

// Détruit l'objet après un délai (effets d'impact).
public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float delay = 1.5f;

    private void Start()
    {
        Destroy(gameObject, delay);
    }
}
