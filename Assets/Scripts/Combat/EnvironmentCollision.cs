using UnityEngine;

// Collisions avec le décor pour les personnages déplacés sans physique (clic-déplacement, dash, ennemis).
// Le décor est identifié par le layer « Environment ».
public static class EnvironmentCollision
{
    public const string LayerName = "Environment";

    private static readonly Collider[] Buffer = new Collider[16];
    private static int mask = -1;

    public static int Mask
    {
        get
        {
            if (mask < 0)
                mask = LayerMask.GetMask(LayerName);
            return mask;
        }
    }

    // Repousse la capsule hors des obstacles du décor (dans le plan horizontal) et renvoie la position corrigée.
    public static Vector3 Resolve(CapsuleCollider capsule, Vector3 position)
    {
        if (capsule == null || Mask == 0)
            return position;

        Transform t = capsule.transform;
        Quaternion rotation = t.rotation;
        Vector3 scale = t.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float half = Mathf.Max(0f, capsule.height * 0.5f * Mathf.Abs(scale.y) - radius);
        if (radius <= 0.001f)
            return position;

        for (int iteration = 0; iteration < 3; iteration++)
        {
            Vector3 center = position + rotation * Vector3.Scale(capsule.center, scale);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.down * half, center + Vector3.up * half, radius, Buffer, Mask, QueryTriggerInteraction.Ignore);

            bool pushed = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = Buffer[i];
                if (!Physics.ComputePenetration(capsule, position, rotation, other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                    continue;

                Vector3 push = direction * distance;
                push.y = 0f;
                position += push;
                pushed = true;
            }
            if (!pushed)
                break;
        }
        return position;
    }

    // Vrai si une capsule verticale (pieds au sol) chevauche le décor à cet endroit.
    public static bool IsBlocked(Vector3 groundPoint, float radius, float height)
    {
        return Mask != 0 && Physics.CheckCapsule(groundPoint + Vector3.up * radius, groundPoint + Vector3.up * Mathf.Max(radius, height - radius), radius, Mask, QueryTriggerInteraction.Ignore);
    }
}
