using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Génère la recharge de dash laissée par les ennemis laser vaincus (effet d'énergie au sol aux couleurs du dash,
// effet de ramassage) et la branche sur l'EnemyManager de la scène ouverte.
public static class DashRechargeSetupBuilder
{
    private const string Root = "Assets/Pickups/DashRecharge";
    // Boule d'énergie : rayon et hauteur au-dessus du sol.
    private const float OrbRadius = 0.26f;
    private const float OrbHeight = 0.9f;

    private static readonly Color White = DashSetupBuilder.BoltWhite;
    private static readonly Color Yellow = DashSetupBuilder.SpeedYellow;
    private static readonly Color Orange = DashSetupBuilder.SpeedOrange;

    [MenuItem("Tools/Pickups/Build Dash Recharge")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);
        VfxLibrary.EnsureTextures();
        SigilLibrary.EnsureTextures();

        GameObject collect = BuildCollectVfx();
        DashRecharge pickup = BuildPickup(collect);
        AssetDatabase.SaveAssets();

        EnemyManager manager = Object.FindAnyObjectByType<EnemyManager>();
        if (manager == null)
            return;
        var so = new SerializedObject(manager);
        so.FindProperty("dashRechargePrefab").objectReferenceValue = pickup;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
    }

    // ---------- Recharge au sol ----------

    // Cellule d'énergie : boule incandescente qui flotte à mi-hauteur, parcourue d'arcs électriques, qui aspire
    // l'énergie autour d'elle, avec un halo qui vibre et une flaque de lumière au sol.
    private static DashRecharge BuildPickup(GameObject collectVfx)
    {
        Material groundMat = QuadMaterial("M_RechargeGround", "T_Glow", Orange, 0.9f);
        Material coreMat = EmissiveMaterial("M_RechargeCore", new Color(1f, 0.62f, 0.15f) * 3f);
        Material heartMat = VfxLibrary.Material($"{Root}/M_RechargeHeart.mat", VfxLibrary.Tex("T_Glow"), 2.5f);
        Material haloMat = VfxLibrary.Material($"{Root}/M_RechargeHalo.mat", VfxLibrary.Tex("T_Glow"), 2.2f);
        Material sparkMat = VfxLibrary.Material($"{Root}/M_RechargeSpark.mat", VfxLibrary.Tex("T_Spark"), 6f);
        Material arcMat = VfxLibrary.Material($"{Root}/M_RechargeArc.mat", VfxLibrary.Tex("T_BoltBand"), 4f);

        var root = new GameObject("DashRecharge");
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        // Lumière de la boule projetée au sol.
        GroundQuad("GroundGlow", visual, groundMat, 0.02f, 2.6f, 0f);

        var orb = new GameObject("Orb").transform;
        orb.SetParent(visual, false);
        orb.localPosition = new Vector3(0f, OrbHeight, 0f);
        var bobbing = new SerializedObject(orb.gameObject.AddComponent<Bobbing>());
        bobbing.FindProperty("amplitude").floatValue = 0.12f;
        bobbing.FindProperty("period").floatValue = 1.8f;
        bobbing.FindProperty("turnSpeed").floatValue = 0f;
        bobbing.ApplyModifiedPropertiesWithoutUndo();

        // Noyau solide en HDR (le bloom le fait rayonner) et coeur blanc brûlant par-dessus.
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(orb, false);
        core.transform.localScale = Vector3.one * OrbRadius * 2f;
        var coreRenderer = core.GetComponent<MeshRenderer>();
        coreRenderer.sharedMaterial = coreMat;
        coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
        coreRenderer.receiveShadows = false;

        var fadeInOut = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) };
        var white = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };

        ParticleSystem heart = Looping("Heart", orb, heartMat, 12f, 0.35f, 0f, White, 14);
        var main = heart.main;
        main.startSize = new ParticleSystem.MinMaxCurve(OrbRadius * 2.4f, OrbRadius * 3f);
        VfxLibrary.ColorOverLifetime(heart, white, fadeInOut);

        // Halo orange qui vibre autour de la boule.
        ParticleSystem halo = Looping("Halo", orb, haloMat, 6f, 0.7f, 0f, new Color(Orange.r, Orange.g, Orange.b, 0.7f), 12);
        main = halo.main;
        main.startSize = new ParticleSystem.MinMaxCurve(OrbRadius * 6.5f, OrbRadius * 8f);
        VfxLibrary.ColorOverLifetime(halo, white, fadeInOut);

        // Énergie aspirée vers la boule en tourbillonnant.
        ParticleSystem inflow = Looping("Inflow", orb, sparkMat, 30f, 0.45f, 0f, Yellow, 13);
        main = inflow.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(White, Yellow);
        var shape = inflow.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = OrbRadius * 4f;
        shape.radiusThickness = 0f;
        var velocity = inflow.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-1.7f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.5f);
        VfxLibrary.Stretch(inflow, 0.1f, 1f);
        VfxLibrary.ColorOverLifetime(inflow, white, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });

        // Arcs électriques qui crépitent à la surface, sur deux plans inclinés qui tournent.
        var shell = new GameObject("Shell").transform;
        shell.SetParent(orb, false);
        LevelBuilder.SetSpin(shell.gameObject.AddComponent<Spinner>(), new Vector3(40f, 110f, 25f));
        foreach ((string name, Vector3 tilt) in new[] { ("ArcsA", new Vector3(70f, 0f, 0f)), ("ArcsB", new Vector3(-50f, 0f, 60f)) })
        {
            var arcsGo = new GameObject(name);
            arcsGo.transform.SetParent(shell, false);
            arcsGo.transform.localRotation = Quaternion.Euler(tilt);
            var arcs = new SerializedObject(arcsGo.AddComponent<AlchemyArcs>());
            arcs.FindProperty("material").objectReferenceValue = arcMat;
            arcs.FindProperty("color").colorValue = Color.Lerp(Yellow, Color.white, 0.5f);
            SerializedProperty radii = arcs.FindProperty("radii");
            radii.arraySize = 1;
            radii.GetArrayElementAtIndex(0).floatValue = OrbRadius * 1.15f;
            arcs.FindProperty("arcCount").intValue = 3;
            arcs.FindProperty("width").floatValue = 0.035f;
            arcs.FindProperty("arcAngle").vector2Value = new Vector2(40f, 110f);
            arcs.FindProperty("jitter").floatValue = 0.15f;
            arcs.FindProperty("activeChance").floatValue = 0.6f;
            arcs.FindProperty("jumpChance").floatValue = 0f;
            arcs.ApplyModifiedPropertiesWithoutUndo();
        }

        Light light = VfxLibrary.PointLight("Light", orb, Color.Lerp(Yellow, Orange, 0.4f), 4.5f, 2f);
        light.gameObject.AddComponent<LightFlicker>();
        LevelBuilder.SetFlicker(light, 0.25f, 12f);

        DashRecharge pickup = root.AddComponent<DashRecharge>();
        var so = new SerializedObject(pickup);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("collectVfx").objectReferenceValue = collectVfx;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/DashRecharge.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<DashRecharge>();
    }

    // ---------- Ramassage ----------

    // Flash, onde de choc au sol, gerbe d'étincelles et énergie qui remonte le long du joueur.
    private static GameObject BuildCollectVfx()
    {
        Material flashMat = VfxLibrary.Material($"{Root}/M_CollectFlash.mat", VfxLibrary.Tex("T_Glow"), 5f);
        Material ringMat = VfxLibrary.Material($"{Root}/M_CollectRing.mat", VfxLibrary.Tex("T_Ring"), 4f);
        Material sparkMat = VfxLibrary.Material($"{Root}/M_CollectSpark.mat", VfxLibrary.Tex("T_Spark"), 6f);

        var root = new GameObject("DashRechargeCollect");
        var white = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        var fadeOut = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };

        ParticleSystem flash = FireballSetupBuilder.Burst("Flash", root.transform, flashMat, 1, 0.3f, 0f, 2.8f, 15);
        flash.transform.localPosition = new Vector3(0f, 1f, 0f);
        var main = flash.main;
        main.startColor = White;
        VfxLibrary.SizeOverLifetime(flash, AnimationCurve.Linear(0f, 0.5f, 1f, 1.3f));
        VfxLibrary.ColorOverLifetime(flash, white, fadeOut);

        ParticleSystem shockwave = FireballSetupBuilder.Burst("Shockwave", root.transform, ringMat, 1, 0.45f, 0f, 4.5f, 11);
        shockwave.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        shockwave.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        main = shockwave.main;
        main.startColor = Yellow;
        VfxLibrary.SizeOverLifetime(shockwave, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0.3f, 0f)));
        VfxLibrary.ColorOverLifetime(shockwave, white, fadeOut);

        ParticleSystem sparks = FireballSetupBuilder.Burst("Sparks", root.transform, sparkMat, 40, 0f, 0f, 0f, 14);
        sparks.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(White, Yellow);
        main.gravityModifier = 0.5f;
        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        VfxLibrary.Drag(sparks, 2f);
        VfxLibrary.Stretch(sparks, 0.05f, 1.2f);
        FireballSetupBuilder.SetColorOverLifetime(sparks, White, Orange, 1f, 0f);

        // Traits d'énergie qui remontent autour du joueur.
        ParticleSystem rise = FireballSetupBuilder.Burst("Rise", root.transform, sparkMat, 30, 0f, 0f, 0f, 14);
        main = rise.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(White, Yellow);
        shape = rise.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.7f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var velocity = rise.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(4f, 7f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        VfxLibrary.Stretch(rise, 0.08f, 1f);
        FireballSetupBuilder.SetColorOverLifetime(rise, White, Yellow, 1f, 0f);

        Light light = VfxLibrary.PointLight("Light", root.transform, Yellow, 6f, 6f);
        light.transform.localPosition = new Vector3(0f, 1f, 0f);
        var fade = new SerializedObject(light.gameObject.AddComponent<LightFade>());
        fade.FindProperty("duration").floatValue = 0.5f;
        fade.ApplyModifiedPropertiesWithoutUndo();

        var autoDestroy = new SerializedObject(root.AddComponent<AutoDestroy>());
        autoDestroy.FindProperty("delay").floatValue = 1.5f;
        autoDestroy.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/DashRechargeCollect.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------- Outils ----------

    // Matériau de quad teinté (un quad n'a pas de couleurs de sommet : la teinte HDR est dans le matériau).
    private static Material QuadMaterial(string name, string texture, Color color, float intensity)
    {
        Material mat = VfxLibrary.Material($"{Root}/{name}.mat", VfxLibrary.Tex(texture), 1f);
        mat.SetColor("_BaseColor", new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Matériau opaque non éclairé en HDR : le bloom fait rayonner l'objet.
    private static Material EmissiveMaterial(string name, Color hdr)
    {
        string path = $"{Root}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", hdr);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Quad posé à plat au sol, qui tourne sur lui-même (degrés par seconde).
    private static void GroundQuad(string name, Transform parent, Material material, float height, float size, float spin)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(new Vector3(0f, height, 0f), Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (spin != 0f)
            LevelBuilder.SetSpin(go.AddComponent<Spinner>(), new Vector3(0f, 0f, spin));
    }

    // Système en boucle sans vitesse initiale ni forme (durée de vie / taille à 0 : réglées ensuite).
    private static ParticleSystem Looping(string name, Transform parent, Material material, float rate, float lifetime, float size, Color color, int sortingOrder)
    {
        ParticleSystem ps = FireballSetupBuilder.CreateParticles(name, parent, material);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.prewarm = false;
        main.startSpeed = 0f;
        if (lifetime > 0f)
            main.startLifetime = lifetime;
        if (size > 0f)
            main.startSize = size;
        main.startColor = color;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.enabled = false;
        VfxLibrary.Order(ps, sortingOrder);
        return ps;
    }
}
