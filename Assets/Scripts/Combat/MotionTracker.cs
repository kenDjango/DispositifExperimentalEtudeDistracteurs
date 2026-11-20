using UnityEngine;

// Mesure le déplacement réel d'une entité, quel que soit ce qui la déplace : vitesse au sol lissée et vitesse de
// virage de sa trajectoire. Les ennemis s'en servent pour anticiper ses mouvements.
public class MotionTracker : MonoBehaviour
{
    // Temps de lissage (s). Le virage se mesure sur plus longtemps : au clic, la trajectoire tourne par à-coups
    // et c'est la rotation moyenne qui décrit un joueur qui tourne en rond.
    [SerializeField] private float velocitySmoothing = 0.1f;
    [SerializeField] private float turnSmoothing = 0.5f;
    // Au-delà de cette vitesse, le déplacement est ignoré (dash : saut ponctuel, pas une trajectoire).
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float maxTurnRate = 360f;
    // En dessous de cette vitesse, l'entité est considérée immobile.
    [SerializeField] private float minSpeed = 0.2f;

    private Vector3 lastPosition;
    private Vector3 lastDirection;

    public Vector3 Velocity { get; private set; }
    // Degrés par seconde, positif = virage à droite (vu du dessus).
    public float TurnRate { get; private set; }

    private void OnEnable()
    {
        lastPosition = transform.position;
        lastDirection = Vector3.zero;
        Velocity = Vector3.zero;
        TurnRate = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        Vector3 raw = delta / dt;
        float speed = raw.magnitude;
        if (speed > maxSpeed)
        {
            lastDirection = Vector3.zero;
            return;
        }

        Velocity = Vector3.Lerp(Velocity, raw, 1f - Mathf.Exp(-dt / velocitySmoothing));

        // Virage instantané (pas de limite ici : un à-coup de 90° doit peser dans la moyenne).
        float turn = 0f;
        if (speed >= minSpeed)
        {
            Vector3 direction = raw / speed;
            if (lastDirection != Vector3.zero)
                turn = Vector3.SignedAngle(lastDirection, direction, Vector3.up) / dt;
            lastDirection = direction;
        }
        else
        {
            lastDirection = Vector3.zero;
        }
        TurnRate = Mathf.Clamp(Mathf.Lerp(TurnRate, turn, 1f - Mathf.Exp(-dt / turnSmoothing)), -maxTurnRate, maxTurnRate);
    }
}
