using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Génère les assets du laser (matériaux, VFX, compétence) et l'assigne à la touche Z.
public static class LaserSetupBuilder
{
    private const string Root = "Assets/Abilities/Laser";
    private const string ArtRoot = FireballSetupBuilder.ArtRoot;
    private const int LaserSlot = 1;

    private static readonly Color LaserWhite = new Color(0.9f, 0.97f, 1f);
    private static readonly Color LaserCyan = new Color(0.25f, 0.75f, 1f);
    private static readonly Color LaserBlue = new Color(0.1f, 0.3f, 1f);

    [MenuItem("Tools/Abilities/Build Laser Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Root);

        FireballSetupBuilder.SaveTexture($"{ArtRoot}/T_IconLaser.png", MakeLaserIcon(128), true);
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/T_IconLaser.png");

        LaserBeam beam = BuildVfxPrefab();

        string abilityPath = $"{Root}/Laser.asset";
        LaserAbility ability = AssetDatabase.LoadAssetAtPath<LaserAbility>(abilityPath);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<LaserAbility>();
            AssetDatabase.CreateAsset(ability, abilityPath);
        }

        var so = new SerializedObject(ability);
        so.FindProperty("displayName").stringValue = "Laser";
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("cooldown").floatValue = 8f;
        so.FindProperty("beamPrefab").objectReferenceValue = beam;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        Debug.Log("Laser assets générés dans " + Root);
    }

    [MenuItem("Tools/Abilities/Setup Laser In Scene")]
    public static void SetupScene()
    {
        LaserAbility ability = AssetDatabase.LoadAssetAtPath<LaserAbility>($"{Root}/Laser.asset");
        if (ability == null)
        {
            BuildAssets();
            ability = AssetDatabase.LoadAssetAtPath<LaserAbility>($"{Root}/Laser.asset");
        }

        GameObject player = GameObject.Find("Capsule");
        var so = new SerializedObject(player.GetComponent<AbilityCaster>());
        so.FindProperty("slots").GetArrayElementAtIndex(LaserSlot).FindPropertyRelative("ability").objectReferenceValue = ability;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
    }

    // Regénère uniquement le VFX (matériaux, prefab) sans toucher aux réglages de la compétence.
    [MenuItem("Tools/Abilities/Rebuild Laser VFX")]
    public static LaserBeam BuildVfxPrefab()
    {
        VfxLibrary.EnsureTextures();
        Texture2D band = VfxLibrary.Tex("T_Band");

        // Intensités HDR : le coeur sature en blanc et le bloom crée le rayonnement.
        Material telegraphMat = VfxLibrary.Material($"{Root}/M_LaserBeam.mat", band, 1.5f);
        Material coreMat = VfxLibrary.Material($"{Root}/M_LaserCore.mat", band, 8f);
        Material glowMat = VfxLibrary.Material($"{Root}/M_LaserGlow.mat", band, 2f);
        Material hazeMat = VfxLibrary.Material($"{Root}/M_LaserHaze.mat", band, 1.2f);
        Material noiseMat = VfxLibrary.Material($"{Root}/M_LaserNoise.mat", VfxLibrary.Tex("T_BeamNoise"), 4f);
        Material sparkMat = VfxLibrary.Material($"{Root}/M_LaserSpark.mat", VfxLibrary.Tex("T_Spark"), 6f);
        Material flareMat = VfxLibrary.Material($"{Root}/M_LaserFlare.mat", VfxLibrary.Tex("T_Flare"), 6f);
        Material moteMat = VfxLibrary.Material($"{Root}/M_LaserMote.mat", VfxLibrary.Tex("T_Glow"), 4f);

        var root = new GameObject("LaserBeam");

        LineRenderer telegraph = CreateLine("Telegraph", root.transform, telegraphMat, 10,
            new[] { new GradientColorKey(LaserCyan, 0f), new GradientColorKey(LaserBlue, 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.3f, 1f) });
        // Voile sans embouts arrondis, qui s'efface aux extrémités (sinon ses bords se devinent).
        LineRenderer haze = CreateLine("BeamHaze", root.transform, hazeMat, 9,
            new[] { new GradientColorKey(LaserCyan, 0f), new GradientColorKey(LaserBlue, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.3f, 0.06f), new GradientAlphaKey(0.15f, 0.85f), new GradientAlphaKey(0f, 1f) });
        haze.numCapVertices = 0;
        LineRenderer glow = CreateLine("BeamGlow", root.transform, glowMat, 10,
            new[] { new GradientColorKey(LaserCyan, 0f), new GradientColorKey(LaserBlue, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 1f) });
        LineRenderer noise = CreateLine("BeamNoise", root.transform, noiseMat, 11,
            new[] { new GradientColorKey(LaserWhite, 0f), new GradientColorKey(LaserCyan, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 1f) });
        LineRenderer core = CreateLine("BeamCore", root.transform, coreMat, 12,
            new[] { new GradientColorKey(LaserWhite, 0f), new GradientColorKey(LaserCyan, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 1f) });

        // Particules lumineuses le long du rayon (émises par script au moment du tir).
        ParticleSystem beamParticles = FireballSetupBuilder.CreateParticles("BeamParticles", root.transform, moteMat);
        var main = beamParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(LaserWhite, LaserCyan);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var emission = beamParticles.emission;
        emission.enabled = false;
        VfxLibrary.Turbulence(beamParticles, 1.2f, 1.5f, 1f);
        FireballSetupBuilder.SetColorOverLifetime(beamParticles, LaserWhite, LaserBlue, 1f, 0f);
        FireballSetupBuilder.SetSizeOverLifetime(beamParticles, 1f, 0.2f);
        VfxLibrary.Order(beamParticles, 13);

        // Gerbe d'étincelles sur chaque cible touchée.
        ParticleSystem hitSparks = FireballSetupBuilder.CreateParticles("HitSparks", root.transform, sparkMat);
        main = hitSparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(LaserWhite, LaserCyan);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;
        main.maxParticles = 200;
        emission = hitSparks.emission;
        emission.enabled = false;
        VfxLibrary.Drag(hitSparks, 1f);
        VfxLibrary.Stretch(hitSparks, 0.04f, 1.5f);
        FireballSetupBuilder.SetColorOverLifetime(hitSparks, LaserWhite, LaserCyan, 1f, 0f);
        VfxLibrary.Order(hitSparks, 14);

        // Charge : énergie aspirée vers la source avant le tir.
        ParticleSystem charge = FireballSetupBuilder.CreateParticles("Charge", root.transform, sparkMat);
        main = charge.main;
        main.playOnAwake = false;
        main.startLifetime = 0.22f;
        main.startSpeed = -5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(LaserWhite, LaserCyan);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        emission = charge.emission;
        emission.rateOverTime = 120f;
        var shape = charge.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.2f;
        shape.radiusThickness = 0f;
        VfxLibrary.Stretch(charge, 0.05f, 1f);
        FireballSetupBuilder.SetColorOverLifetime(charge, LaserCyan, LaserWhite, 0f, 1f);
        VfxLibrary.Order(charge, 14);

        ParticleSystem muzzle = FlashBurst("MuzzleFlash", root.transform, flareMat, 2.2f);
        ParticleSystem endFlash = FlashBurst("EndFlash", root.transform, flareMat, 1.4f);

        Light light = VfxLibrary.PointLight("Flash", root.transform, LaserCyan, 10f, 6f);
        light.enabled = false;

        LaserBeam beam = root.AddComponent<LaserBeam>();
        var so = new SerializedObject(beam);
        so.FindProperty("telegraph").objectReferenceValue = telegraph;
        so.FindProperty("beamGlow").objectReferenceValue = glow;
        so.FindProperty("beamCore").objectReferenceValue = core;
        so.FindProperty("beamHaze").objectReferenceValue = haze;
        so.FindProperty("beamNoise").objectReferenceValue = noise;
        so.FindProperty("beamParticles").objectReferenceValue = beamParticles;
        so.FindProperty("hitSparks").objectReferenceValue = hitSparks;
        so.FindProperty("charge").objectReferenceValue = charge;
        so.FindProperty("muzzleFlash").objectReferenceValue = muzzle;
        so.FindProperty("endFlash").objectReferenceValue = endFlash;
        so.FindProperty("flash").objectReferenceValue = light;
        so.FindProperty("flashIntensity").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/LaserBeam.prefab");
        Object.DestroyImmediate(root);
        LaserBeam result = prefab.GetComponent<LaserBeam>();

        LaserAbility ability = AssetDatabase.LoadAssetAtPath<LaserAbility>($"{Root}/Laser.asset");
        if (ability != null)
        {
            var abilitySo = new SerializedObject(ability);
            abilitySo.FindProperty("beamPrefab").objectReferenceValue = result;
            abilitySo.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    // Éclat bref (flare + halo), déclenché par script.
    private static ParticleSystem FlashBurst(string name, Transform parent, Material flareMat, float size)
    {
        ParticleSystem flash = FireballSetupBuilder.Burst(name, parent, flareMat, 2, 0.18f, 0f, size, 15);
        var main = flash.main;
        main.playOnAwake = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(LaserWhite, LaserCyan);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        VfxLibrary.SizeOverLifetime(flash, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
        FireballSetupBuilder.SetColorOverLifetime(flash, Color.white, Color.white, 1f, 0f);
        return flash;
    }

    private static LineRenderer CreateLine(string name, Transform parent, Material material, int sortingOrder,
        GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        line.colorGradient = gradient;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }

    private static Texture2D MakeLaserIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 a = new Vector2(0.12f, 0.12f);
        Vector2 b = new Vector2(0.95f, 0.88f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            Color c = Color.Lerp(new Color(0.02f, 0.04f, 0.15f), new Color(0.05f, 0.12f, 0.3f), p.y);

            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);

            float glow = Mathf.Clamp01(1f - d / 0.16f);
            c = Color.Lerp(c, LaserBlue, glow);
            c = Color.Lerp(c, LaserCyan, Mathf.Clamp01(1f - d / 0.08f));
            c = Color.Lerp(c, LaserWhite, Mathf.Clamp01(1f - d / 0.03f));

            // Halo à la source du tir.
            float source = Mathf.Clamp01(1f - Vector2.Distance(p, a) / 0.2f);
            c = Color.Lerp(c, LaserWhite, source * source);

            c.a = 1f;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
}
