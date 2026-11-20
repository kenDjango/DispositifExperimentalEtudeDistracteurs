using UnityEngine;

// Pilote l'Animator du personnage : marche selon la vitesse réelle, attaque magique quand une compétence est lancée.
public class CharacterAnimator : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    // Même attaque sur le calque haut du corps : les bras continuent l'attaque si les jambes passent à la marche.
    private static readonly int AttackUpperId = Animator.StringToHash("AttackUpper");
    private static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");
    private static readonly int EmptyStateId = Animator.StringToHash("Empty");

    [SerializeField] private Animator animator;
    [SerializeField] private AbilityCaster caster;
    // Index des slots qui jouent l'attaque magique (0 = A, 1 = Z).
    [SerializeField] private int[] attackSlots = { 0, 1 };
    // Multiplicateur de vitesse de l'attaque magique (1 = vitesse d'origine). Modifiable en Play mode.
    [SerializeField, Range(0.1f, 5f)] private float attackSpeed = 2f;
    // Lissage de la vitesse envoyée à l'Animator.
    [SerializeField] private float speedDamping = 0.08f;

    // Vitesse de fondu du calque haut du corps (poids par seconde).
    [SerializeField] private float upperBodyBlendSpeed = 6f;

    private Vector3 lastPosition;
    private int upperBodyLayer = -1;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (caster == null)
            caster = GetComponent<AbilityCaster>();
        lastPosition = transform.position;
        upperBodyLayer = animator.GetLayerIndex("UpperBody");
    }

    private void OnEnable()
    {
        if (caster != null)
            caster.AbilityCast += OnAbilityCast;
    }

    private void OnDisable()
    {
        if (caster != null)
            caster.AbilityCast -= OnAbilityCast;
    }

    private void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);
        animator.SetFloat(AttackSpeedId, attackSpeed);
        UpdateUpperBodyWeight();
    }

    // Le calque haut du corps n'agit que pendant l'attaque ; le reste du temps les bras suivent le calque de base.
    private void UpdateUpperBodyWeight()
    {
        if (upperBodyLayer < 0)
            return;

        bool attacking = animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash != EmptyStateId
                         || animator.IsInTransition(upperBodyLayer);
        float weight = Mathf.MoveTowards(animator.GetLayerWeight(upperBodyLayer), attacking ? 1f : 0f, upperBodyBlendSpeed * Time.deltaTime);
        animator.SetLayerWeight(upperBodyLayer, weight);
    }

    private void OnAbilityCast(int index)
    {
        if (System.Array.IndexOf(attackSlots, index) < 0)
            return;

        // L'attaque complète ne part qu'à l'arrêt : pas de trigger resté en attente qui la relancerait plus tard.
        if (animator.GetFloat(SpeedId) < 0.1f)
            animator.SetTrigger(AttackId);
        animator.SetTrigger(AttackUpperId);
    }
}
