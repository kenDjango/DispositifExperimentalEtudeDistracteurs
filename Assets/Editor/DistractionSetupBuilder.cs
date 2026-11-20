using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Installe le système de distraction dans la scène ouverte : objet « DistractionDirector » (tous les réglages) et
// branchement des sources de l'arène sci-fi (gyrophares, brèche, flux du portail, cuves, ambiance), des araignées,
// de l'effet de téléportation et des mesures. Les réglages déjà saisis dans l'inspecteur sont conservés.
// Relancé automatiquement par Tools > Level > Build Sci-Fi Arena (la reconstruction de l'arène casse les liens).
public static class DistractionSetupBuilder
{
    [MenuItem("Tools/Distraction/Setup Distraction Director")]
    public static void Setup()
    {
        if (Install())
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }

    // Renvoie faux si la scène n'a pas d'arène sci-fi ou pas de boucle de jeu.
    internal static bool Install()
    {
        GameObject level = GameObject.Find("Level");
        GameManager game = Object.FindAnyObjectByType<GameManager>();
        PerformanceMetrics metrics = Object.FindAnyObjectByType<PerformanceMetrics>();
        if (level == null || level.transform.Find("Portal/PortalRing") == null || game == null || metrics == null)
        {
            Debug.LogWarning("Distraction : il faut l'arène sci-fi (Tools > Level > Build Sci-Fi Arena) et la boucle de jeu (Tools > Game Loop > Setup Scene).");
            return false;
        }
        Transform root = level.transform;

        DistractionDirector director = Object.FindAnyObjectByType<DistractionDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            var go = new GameObject("DistractionDirector");
            Undo.RegisterCreatedObjectUndo(go, "DistractionDirector");
            director = go.AddComponent<DistractionDirector>();
        }
        Undo.RecordObject(director, "Distraction sources");

        // Gyrophares : spots + halo, rotor qui tourne.
        DistractorGroup[] beacons = Children(root, "AlarmBeacon").Select((b, i) => new DistractorGroup
        {
            name = $"Gyrophare {i + 1}",
            lights = b.GetComponentsInChildren<Light>(),
            spinners = b.GetComponentsInChildren<Spinner>(),
        }).ToArray();

        // Brèches (mur est, mur ouest) : gerbes d'étincelles et lumière qui grésille. La première est la dernière
        // retirée quand le palier baisse.
        DistractorGroup[] breaches = new[] { "", "_2" }.Select((suffix, i) => new DistractorGroup
        {
            name = $"Brèche {i + 1}",
            particles = Components<ParticleSystem>(root, "Decor/BreachSparks" + suffix),
            lights = Components<Light>(root, "Decor/BreachLight" + suffix),
        }).ToArray();

        // Flux du portail : colonnes de caractères (leur traînée suit), caractères qui s'échappent, particules aspirées.
        // L'anneau et le halo du portail ne bougent pas.
        var portalStream = new DistractorGroup
        {
            name = "Flux du portail",
            particles = Components<ParticleSystem>(root, "Portal/DataRain")
                .Concat(Components<ParticleSystem>(root, "Portal/FlyingGlyphs"))
                .Concat(Components<ParticleSystem>(root, "Portal/PortalMotes")).ToArray(),
        };

        // Cuves : bulles, lumière, spécimen qui flotte.
        DistractorGroup[] tanks = root.Cast<Transform>().Where(t => t.name.StartsWith("SpecimenTank_")).Select(t => new DistractorGroup
        {
            name = "Cuve " + t.name.Substring("SpecimenTank_".Length),
            particles = Components<ParticleSystem>(t, "Bubbles"),
            lights = Components<Light>(t, "TankLight"),
            bobbers = t.GetComponentsInChildren<Bobbing>(),
        }).ToArray();

        // Ambiance : poussière et lumière du portail, toujours présentes.
        var ambient = new DistractorGroup
        {
            name = "Ambiance",
            particles = Components<ParticleSystem>(root, "Atmosphere/AmbientDust"),
            lights = Components<Light>(root, "Portal/PortalLight"),
        };

        director.SetSources(Object.FindAnyObjectByType<SpiderSwarm>(FindObjectsInactive.Include), beacons, breaches, portalStream, tanks, ambient,
            Object.FindAnyObjectByType<EnemyManager>(), game, metrics);
        EditorUtility.SetDirty(director);

        MetricsReporter reporter = Object.FindAnyObjectByType<MetricsReporter>();
        if (reporter != null)
        {
            var so = new SerializedObject(reporter);
            so.FindProperty("distraction").objectReferenceValue = director;
            so.ApplyModifiedProperties();
        }

        // HUD de débogage (palier et décision sous le score de performance).
        GameHUD hud = Object.FindAnyObjectByType<GameHUD>(FindObjectsInactive.Include);
        if (hud != null)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("distraction").objectReferenceValue = director;
            so.ApplyModifiedProperties();
        }

        Debug.Log($"Distraction : {beacons.Length} gyrophares, {tanks.Length} cuves, {breaches.Length} brèches, flux du portail et ambiance branchés sur {director.name}.");
        return true;
    }

    private static Transform[] Children(Transform root, string name) => root.Cast<Transform>().Where(t => t.name == name).ToArray();

    private static T[] Components<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        if (t == null)
        {
            Debug.LogWarning($"Distraction : objet introuvable {root.name}/{path}.");
            return new T[0];
        }
        T component = t.GetComponent<T>();
        return component != null ? new[] { component } : new T[0];
    }
}
