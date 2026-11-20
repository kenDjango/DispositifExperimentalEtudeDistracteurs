using UnityEngine;
using UnityEngine.InputSystem;

// Déplace le personnage vers le point cliqué (clic droit) sur le sol, sans sortir de la zone de jeu.
public class ClickToMove : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    // Zone de déplacement autorisée du personnage.
    [SerializeField] private MovementBounds bounds;
    // Marge pour que le personnage reste entièrement sur le sol (rayon de la capsule).
    [SerializeField] private float edgeMargin = 0.5f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stopDistance = 0.05f;
    // Hauteur du sol utilisée si le rayon ne touche aucun collider.
    [SerializeField] private float groundHeight = 0f;

    // Temps passé bloqué contre le décor avant d'abandonner la destination.
    [SerializeField] private float giveUpWhenBlocked = 0.25f;

    private Vector3 target;
    private bool hasTarget;
    private CapsuleCollider capsule;
    private float blockedTime;

    // Ordre de déplacement donné (clic droit sur le sol), avec sa destination.
    public event System.Action<Vector3> MoveOrdered;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
        capsule = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && TryGetClickPoint(mouse.position.ReadValue(), out Vector3 point))
        {
            target = ClampToGround(new Vector3(point.x, transform.position.y, point.z));
            hasTarget = true;
            blockedTime = 0f;
            MoveOrdered?.Invoke(target);
        }

        if (!hasTarget)
            return;

        Vector3 toTarget = target - transform.position;
        if (toTarget.magnitude <= stopDistance)
        {
            hasTarget = false;
            return;
        }

        float step = moveSpeed * Time.deltaTime;
        Vector3 before = transform.position;
        transform.position = EnvironmentCollision.Resolve(capsule, Vector3.MoveTowards(before, target, step));

        // Coincé contre un obstacle : on s'arrête plutôt que de pousser indéfiniment.
        blockedTime = Vector3.Distance(before, transform.position) < step * 0.2f ? blockedTime + Time.deltaTime : 0f;
        if (blockedTime >= giveUpWhenBlocked)
        {
            hasTarget = false;
            return;
        }

        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    // Annule le déplacement en cours (ex. interrompu par un dash).
    public void Stop()
    {
        hasTarget = false;
    }

    public Vector3 ClampToGround(Vector3 position)
    {
        return bounds != null ? bounds.Clamp(position, edgeMargin) : position;
    }

    private bool TryGetClickPoint(Vector2 screenPos, out Vector3 point)
    {
        Ray ray = viewCamera.ScreenPointToRay(screenPos);

        // Le décor est ignoré : on vise le sol derrière (sinon un clic sur une caisse vise son sommet).
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~EnvironmentCollision.Mask, QueryTriggerInteraction.Ignore) && hit.transform != transform)
        {
            point = hit.point;
            return true;
        }

        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        if (ground.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = default;
        return false;
    }
}
