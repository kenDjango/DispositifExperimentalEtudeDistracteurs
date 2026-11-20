using System.Collections;
using UnityEngine;

// Exécute le déplacement d'un dash : interrompt le clic-déplacement et reste dans la zone du joueur.
public class DashMotor : MonoBehaviour
{
    public bool IsDashing { get; private set; }

    private ClickToMove mover;
    private CapsuleCollider capsule;

    private void Awake()
    {
        mover = GetComponent<ClickToMove>();
        capsule = GetComponent<CapsuleCollider>();
    }

    public void Dash(Vector3 destination, float duration, DashVfx vfxPrefab)
    {
        if (IsDashing)
            return;

        if (mover != null)
        {
            destination = mover.ClampToGround(destination);
            mover.Stop();
        }

        StartCoroutine(DashRoutine(destination, duration, vfxPrefab));
    }

    private IEnumerator DashRoutine(Vector3 destination, float duration, DashVfx vfxPrefab)
    {
        IsDashing = true;
        if (mover != null)
            mover.enabled = false;

        Vector3 start = transform.position;
        Vector3 toDestination = destination - start;
        if (toDestination.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toDestination);

        DashVfx vfx = vfxPrefab != null ? Instantiate(vfxPrefab, start, transform.rotation) : null;
        if (vfx != null)
            vfx.Begin(transform);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Ease-out : départ explosif, légère décélération à l'arrivée.
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);
            transform.position = EnvironmentCollision.Resolve(capsule, Vector3.LerpUnclamped(start, destination, k));
            yield return null;
        }
        transform.position = EnvironmentCollision.Resolve(capsule, destination);

        if (vfx != null)
            vfx.End();
        if (mover != null)
            mover.enabled = true;
        IsDashing = false;
    }
}
