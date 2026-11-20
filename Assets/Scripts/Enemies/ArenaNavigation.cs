using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Carte de navigation de l'arène : sol de la zone de jeu moins les obstacles du décor (layer Environment), élargis
// du rayon des robots. Les robots y calculent leur chemin pour contourner caisses, barils et murets au lieu de rester
// bloqués contre. La carte est calculée dans l'éditeur et enregistrée (Tools > Game Loop > Bake Arena Navigation) :
// dans le jeu exporté, les maillages du décor ne sont pas lisibles et ne pourraient pas servir à la construire.
public static class ArenaNavigation
{
    private static NavMeshDataInstance instance;

    public static bool Ready => instance.valid;

    // Active une carte (remplace la précédente : la scène est rechargée à chaque partie).
    public static void Use(NavMeshData data)
    {
        Clear();
        if (data != null)
            instance = NavMesh.AddNavMeshData(data);
    }

    // Calcule la carte de la zone de jeu (dans l'éditeur, ou en secours au lancement si aucune carte n'a été
    // enregistrée : les obstacles à maillage illisible manquent alors dans le jeu exporté).
    public static NavMeshData Create(MovementBounds area, float agentRadius, float agentHeight)
    {
        if (area == null)
            return null;

        Vector3 center = area.transform.position;
        Vector2 size = area.Size;
        var bounds = new Bounds(center, new Vector3(size.x + 2f, agentHeight * 3f, size.y + 2f));

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, EnvironmentCollision.Mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        // Sol : la zone de jeu elle-même (le sol réel peut ne pas avoir de collider exploitable).
        sources.Add(new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            size = new Vector3(size.x, 0.1f, size.y),
            transform = Matrix4x4.Translate(center + Vector3.down * 0.05f),
            area = 0,
        });

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = agentRadius;
        settings.agentHeight = agentHeight;
        // Les obstacles ne s'enjambent pas.
        settings.agentClimb = 0.2f;
        return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
    }

    public static void Clear()
    {
        if (instance.valid)
            instance.Remove();
        instance = default;
    }

    // Prochain point de passage pour aller de from à to en contournant le décor (faux : pas de chemin connu, aller
    // tout droit). Les positions sont ramenées au sol.
    public static bool NextWaypoint(Vector3 from, Vector3 to, NavMeshPath path, out Vector3 waypoint)
    {
        waypoint = to;
        if (!Ready
            || !NavMesh.SamplePosition(Ground(from), out NavMeshHit start, 2f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(Ground(to), out NavMeshHit end, 3f, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            || path.status == NavMeshPathStatus.PathInvalid)
            return false;

        // Premier coin qui n'est pas déjà atteint.
        Vector3[] corners = path.corners;
        Vector3 origin = Ground(from);
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 corner = Ground(corners[i]);
            if ((corner - origin).sqrMagnitude > 0.3f * 0.3f)
            {
                waypoint = corner;
                return true;
            }
        }
        return false;
    }

    private static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);
}
