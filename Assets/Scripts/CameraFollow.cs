using UnityEngine;

// Caméra façon LoL : angle fixe, suit le personnage, ne sort pas de sa zone.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    // Zone dans laquelle reste le point visé par la caméra (au sol).
    [SerializeField] private MovementBounds bounds;
    [SerializeField] private float pitch = 56f;
    [SerializeField] private float yaw = 0f;
    [SerializeField] private float distance = 25f;
    [SerializeField] private float fieldOfView = 40f;
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 focus;
    private Vector3 velocity;

    private void Start()
    {
        if (target != null)
            focus = GetDesiredFocus();
        Apply();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        focus = Vector3.SmoothDamp(focus, GetDesiredFocus(), ref velocity, smoothTime);
        Apply();
    }

    private Vector3 GetDesiredFocus()
    {
        Vector3 desired = target.position;
        return bounds != null ? bounds.Clamp(desired) : desired;
    }

    private void Apply()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        GetComponent<Camera>().fieldOfView = fieldOfView;
    }

    // Aperçu dans l'éditeur quand on modifie les réglages.
    private void OnValidate()
    {
        if (target == null || Application.isPlaying)
            return;

        focus = GetDesiredFocus();
        Apply();
    }
}
