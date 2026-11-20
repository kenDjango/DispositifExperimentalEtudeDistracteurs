using UnityEngine;

// Anime l'araignée mécanique d'après son déplacement réel, quel que soit ce qui la déplace : marche ou course
// dans la direction suivie (avant, arrière, de côté), accélérée au-delà de la vitesse de course, et petits gestes
// quand elle reste immobile.
public class SpiderAnimator : MonoBehaviour
{
    private static readonly int ForwardId = Animator.StringToHash("Forward");
    private static readonly int StrafeId = Animator.StringToHash("Strafe");
    private static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
    private static readonly int FidgetId = Animator.StringToHash("Fidget");
    private static readonly int FidgetIndexId = Animator.StringToHash("FidgetIndex");
    // Gestes disponibles dans le graphe (0 : se dresse, 1 : sautille).
    private const int FidgetCount = 2;

    [SerializeField] private Animator animator;
    // Vitesse (m/s) de la course vers l'avant à vitesse d'animation normale (mesurée par l'outil de génération).
    [SerializeField] private float runSpeed = 1f;
    // Au-delà de la vitesse de course, l'animation est accélérée jusqu'à ce facteur.
    [SerializeField] private float maxAnimSpeed = 3f;
    [SerializeField] private float damping = 0.1f;

    [Header("Immobile")]
    // Temps (aléatoire entre x et y) entre deux gestes quand l'araignée ne bouge pas (0 : jamais).
    [SerializeField] private Vector2 fidgetInterval = new Vector2(6f, 14f);
    [SerializeField] private float stillSpeed = 0.05f;

    private Vector3 lastPosition;
    private float fidgetTimer;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        lastPosition = transform.position;
        ResetFidget();
    }

    // Réactivée ailleurs (système de distraction) : pas de vitesse calculée depuis son ancienne position.
    private void OnEnable()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || animator == null)
            return;

        Vector3 local = transform.InverseTransformDirection((transform.position - lastPosition) / dt);
        lastPosition = transform.position;
        local.y = 0f;
        float speed = local.magnitude;

        // Plus vite que la course : mêmes pas, joués plus vite.
        float animSpeed = runSpeed > 0f ? Mathf.Clamp(speed / runSpeed, 1f, maxAnimSpeed) : 1f;
        animator.SetFloat(ForwardId, local.z / animSpeed, damping, dt);
        animator.SetFloat(StrafeId, local.x / animSpeed, damping, dt);
        animator.SetFloat(AnimSpeedId, animSpeed);

        if (fidgetInterval.y <= 0f || speed > stillSpeed)
        {
            ResetFidget();
            return;
        }
        fidgetTimer -= dt;
        if (fidgetTimer <= 0f)
        {
            animator.SetInteger(FidgetIndexId, Random.Range(0, FidgetCount));
            animator.SetTrigger(FidgetId);
            ResetFidget();
        }
    }

    private void ResetFidget()
    {
        fidgetTimer = Random.Range(fidgetInterval.x, fidgetInterval.y);
    }
}
