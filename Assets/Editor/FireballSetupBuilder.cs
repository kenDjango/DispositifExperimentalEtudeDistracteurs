using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Génère les assets de la boule de feu (textures, matériaux, VFX, prefabs, compétence)
// et installe le système de compétences dans la scène ouverte.
public static class FireballSetupBuilder
{
    private const string Root = "Assets/Abilities/Fireball";
    internal const string ArtRoot = "Assets/Art/Generated";

    private static readonly Color FireCore = new Color(1f, 0.85f, 0.45f);
    private static readonly Color FireMid = new Color(1f, 0.45f, 0.08f);
    private static readonly Color FireDark = new Color(0.6f, 0.08f, 0.02f);

    [MenuItem("Tools/Abilities/Build Fireball Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ArtRoot);

        // T_SoftGlow est encore utilisée par d'autres effets.
        SaveTexture($"{ArtRoot}/T_SoftGlow.png", MakeSoftGlow(64), false);
        SaveTexture($"{ArtRoot}/T_UIWhite.png", MakeSolid(8, Color.white), true);
        SaveTexture($"{ArtRoot}/T_IconFireball.png", MakeFireballIcon(128), true);
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/T_IconFireball.png");

        Projectile projectile = BuildVfxPrefabs();

        string abilityPath = $"{Root}/Fireball.asset";
        FireballAbility ability = AssetDatabase.LoadAssetAtPath<FireballAbility>(abilityPath);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<FireballAbility>();
            AssetDatabase.CreateAsset(ability, abilityPath);
        }

        var so = new SerializedObject(ability);
        so.FindProperty("displayName").stringValue = "Boule de feu";
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("cooldown").floatValue = 4f;
        so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
        so.FindProperty("damage").floatValue = 80f;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        Debug.Log("Fireball assets générés dans " + Root);
    }

    [MenuItem("Tools/Abilities/Setup Scene")]
    public static void SetupScene()
    {
        FireballAbility ability = AssetDatabase.LoadAssetAtPath<FireballAbility>($"{Root}/Fireball.asset");
        if (ability == null)
        {
            BuildAssets();
            ability = AssetDatabase.LoadAssetAtPath<FireballAbility>($"{Root}/Fireball.asset");
        }

        GameObject player = GameObject.Find("Capsule");
        Camera gameplayCamera = GameObject.Find("Camera").GetComponent<Camera>();

        AbilityCaster caster = player.GetComponent<AbilityCaster>();
        if (caster == null)
            caster = Undo.AddComponent<AbilityCaster>(player);

        var so = new SerializedObject(caster);
        so.FindProperty("viewCamera").objectReferenceValue = gameplayCamera;
        SerializedProperty slots = so.FindProperty("slots");
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("ability").objectReferenceValue = ability;
        so.ApplyModifiedProperties();

        BuildAbilityBar(caster);
        BuildTrainingDummy();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
    }

    // ---------- VFX ----------

    // Dégradé de température d'une flamme : blanc-jaune brûlant -> orange -> rouge sombre -> fumée.
    private static readonly GradientColorKey[] HeatColors =
    {
        new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f),
        new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0.2f),
        new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.45f),
        new GradientColorKey(new Color(0.6f, 0.12f, 0.04f), 0.75f),
        new GradientColorKey(new Color(0.15f, 0.07f, 0.05f), 1f),
    };

    private class FireMaterials
    {
        public Material Fire, Smoke, Glow, Spark, Flare, Shockwave, Scorch;
    }

    // Regénère uniquement les VFX (textures, matériaux, prefabs) sans toucher aux réglages de la compétence.
    [MenuItem("Tools/Abilities/Rebuild Fireball VFX")]
    public static Projectile BuildVfxPrefabs()
    {
        VfxLibrary.EnsureTextures();

        // Alpha-blend plutôt qu'additif : l'additif disparaît sur un sol clair. L'éclat vient de l'intensité HDR + bloom.
        var m = new FireMaterials
        {
            Fire = VfxLibrary.Material($"{Root}/M_Fire.mat", VfxLibrary.Tex("T_FireSheet"), 2.5f, true),
            Smoke = VfxLibrary.Material($"{Root}/M_Smoke.mat", VfxLibrary.Tex("T_SmokeSheet"), 1f, true),
            Glow = VfxLibrary.Material($"{Root}/M_FireGlow.mat", VfxLibrary.Tex("T_Glow"), 4f),
            Spark = VfxLibrary.Material($"{Root}/M_FireSpark.mat", VfxLibrary.Tex("T_Spark"), 6f),
            Flare = VfxLibrary.Material($"{Root}/M_FireFlare.mat", VfxLibrary.Tex("T_Flare"), 6f),
            Shockwave = VfxLibrary.Material($"{Root}/M_Shockwave.mat", VfxLibrary.Tex("T_Ring"), 2f),
            Scorch = VfxLibrary.Material($"{Root}/M_Scorch.mat", VfxLibrary.Tex("T_Scorch"), 1f),
        };

        GameObject impact = BuildImpactPrefab(m);
        Projectile projectile = BuildProjectilePrefab(m, impact);

        FireballAbility ability = AssetDatabase.LoadAssetAtPath<FireballAbility>($"{Root}/Fireball.asset");
        if (ability != null)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        return projectile;
    }

    private static Projectile BuildProjectilePrefab(FireMaterials m, GameObject impact)
    {
        var root = new GameObject("Fireball");

        // Halo de chaleur autour de la boule.
        ParticleSystem heatGlow = CreateParticles("HeatGlow", root.transform, m.Glow);
        var main = heatGlow.main;
        main.startLifetime = 0.15f;
        main.startSpeed = 0f;
        main.startSize = 2.4f;
        main.startColor = new Color(1f, 0.45f, 0.1f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = heatGlow.emission;
        emission.rateOverTime = 30f;
        var shape = heatGlow.shape;
        shape.enabled = false;
        VfxLibrary.Order(heatGlow, 10);

        // Coeur incandescent, presque blanc.
        ParticleSystem core = CreateParticles("Core", root.transform, m.Glow);
        main = core.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.75f);
        main.startColor = new Color(1f, 0.9f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        emission = core.emission;
        emission.rateOverTime = 60f;
        shape = core.shape;
        shape.enabled = false;
        VfxLibrary.Order(core, 13);

        // Flammes turbulentes laissées dans le sillage.
        ParticleSystem flames = CreateParticles("Flames", root.transform, m.Fire);
        main = flames.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.5f;
        main.maxParticles = 300;
        emission = flames.emission;
        emission.rateOverTime = 50f;
        emission.rateOverDistance = 8f;
        shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        VfxLibrary.SizeOverLifetime(flames, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.4f, 1.1f), new Keyframe(1f, 1.5f)));
        VfxLibrary.Spin(flames, 120f);
        VfxLibrary.Turbulence(flames, 0.8f, 1.3f, 0.8f);
        VfxLibrary.ColorOverLifetime(flames, HeatColors,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0.7f, 0.75f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Flipbook(flames);
        VfxLibrary.Order(flames, 12);

        // Fumée sombre qui s'élève derrière la boule.
        ParticleSystem smoke = CreateParticles("Smoke", root.transform, m.Smoke);
        main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.22f, 0.19f, 0.17f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;
        emission = smoke.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 2.5f;
        shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        VfxLibrary.SizeOverLifetime(smoke, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.6f));
        VfxLibrary.Spin(smoke, 40f);
        VfxLibrary.Turbulence(smoke, 0.4f, 0.8f, 0.3f);
        VfxLibrary.ColorOverLifetime(smoke, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.15f), new GradientAlphaKey(0.25f, 0.6f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Flipbook(smoke);
        VfxLibrary.Order(smoke, 9);

        // Braises qui tourbillonnent dans le sillage.
        ParticleSystem embers = CreateParticles("Embers", root.transform, m.Spark);
        main = embers.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f;
        emission = embers.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 7f;
        shape = embers.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        VfxLibrary.Turbulence(embers, 1.5f, 2f, 1f);
        VfxLibrary.Stretch(embers, 0.04f, 1.5f);
        VfxLibrary.ColorOverLifetime(embers, EmberColors,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Order(embers, 14);

        Light light = VfxLibrary.PointLight("Light", root.transform, new Color(1f, 0.5f, 0.15f), 6f, 5f);
        light.gameObject.AddComponent<LightFlicker>();

        Projectile projectile = root.AddComponent<Projectile>();
        var so = new SerializedObject(projectile);
        so.FindProperty("impactPrefab").objectReferenceValue = impact;
        so.FindProperty("lingerTime").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Fireball.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Projectile>();
    }

    private static readonly GradientColorKey[] EmberColors =
    {
        new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
        new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.5f),
        new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1f),
    };

    private static GameObject BuildImpactPrefab(FireMaterials m)
    {
        var root = new GameObject("FireballImpact");
        var white = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };

        // Éclair aveuglant au moment de l'impact.
        ParticleSystem flare = Burst("FlashFlare", root.transform, m.Flare, 1, 0.14f, 0f, 3.2f, 15);
        var main = flare.main;
        main.startColor = new Color(1f, 0.85f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        VfxLibrary.SizeOverLifetime(flare, AnimationCurve.Linear(0f, 0.5f, 1f, 1.3f));
        VfxLibrary.ColorOverLifetime(flare, white, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        ParticleSystem flashGlow = Burst("FlashGlow", root.transform, m.Glow, 1, 0.3f, 0f, 5.5f, 11);
        main = flashGlow.main;
        main.startColor = new Color(1f, 0.5f, 0.15f, 0.8f);
        VfxLibrary.ColorOverLifetime(flashGlow, white, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        // Boule de feu qui gonfle, ralentit et monte.
        ParticleSystem fireball = Burst("Fireball", root.transform, m.Fire, 26, 0f, 0f, 0f, 12);
        main = fireball.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.6f;
        fireball.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26), new ParticleSystem.Burst(0.04f, 12) });
        var shape = fireball.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        VfxLibrary.Drag(fireball, 3f);
        VfxLibrary.SizeOverLifetime(fireball, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 1.8f)));
        VfxLibrary.Spin(fireball, 90f);
        VfxLibrary.Turbulence(fireball, 0.9f, 1f, 0.7f);
        VfxLibrary.ColorOverLifetime(fireball, HeatColors,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.6f, 0.75f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Flipbook(fireball);

        // Fumée épaisse qui prend le relais du feu.
        ParticleSystem smoke = Burst("Smoke", root.transform, m.Smoke, 16, 0f, 0f, 0f, 9);
        main = smoke.main;
        main.startDelay = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.2f, 0.18f, 0.17f);
        main.gravityModifier = -0.25f;
        shape = smoke.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        VfxLibrary.Drag(smoke, 2f);
        VfxLibrary.SizeOverLifetime(smoke, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
        VfxLibrary.Spin(smoke, 30f);
        VfxLibrary.Turbulence(smoke, 0.35f, 0.6f, 0.25f);
        VfxLibrary.ColorOverLifetime(smoke, white,
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Flipbook(smoke);

        // Gerbe d'étincelles qui retombent.
        ParticleSystem sparks = Burst("Sparks", root.transform, m.Spark, 45, 0f, 0f, 0f, 14);
        main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.gravityModifier = 1.4f;
        shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        VfxLibrary.Drag(sparks, 1.2f);
        VfxLibrary.Stretch(sparks, 0.035f, 1.2f);
        VfxLibrary.ColorOverLifetime(sparks, EmberColors,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });

        // Braises qui flottent ensuite dans la fumée.
        ParticleSystem embers = Burst("Embers", root.transform, m.Spark, 25, 0f, 0f, 0f, 14);
        main = embers.main;
        main.startDelay = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.gravityModifier = -0.15f;
        shape = embers.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;
        VfxLibrary.Turbulence(embers, 1.2f, 1.5f, 0.8f);
        VfxLibrary.ColorOverLifetime(embers, EmberColors,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });

        // Onde de choc qui balaie le sol.
        ParticleSystem shockwave = Burst("Shockwave", root.transform, m.Shockwave, 1, 0.4f, 0f, 6f, 11);
        shockwave.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        main = shockwave.main;
        main.startColor = new Color(1f, 0.65f, 0.35f);
        VfxLibrary.SizeOverLifetime(shockwave, new AnimationCurve(new Keyframe(0f, 0.05f, 0f, 4f), new Keyframe(1f, 1f, 0f, 0f)));
        VfxLibrary.ColorOverLifetime(shockwave, white, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        shockwave.gameObject.AddComponent<GroundSnap>();

        // Trace de brûlure qui reste quelques secondes.
        ParticleSystem scorch = Burst("Scorch", root.transform, m.Scorch, 1, 4.5f, 0f, 0f, 8);
        scorch.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        main = scorch.main;
        main.startDelay = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(2.6f, 3.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        VfxLibrary.ColorOverLifetime(scorch, white,
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.03f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        scorch.gameObject.AddComponent<GroundSnap>();

        Light light = VfxLibrary.PointLight("Flash", root.transform, new Color(1f, 0.55f, 0.2f), 9f, 14f);
        var fade = new SerializedObject(light.gameObject.AddComponent<LightFade>());
        fade.FindProperty("duration").floatValue = 0.45f;
        fade.ApplyModifiedPropertiesWithoutUndo();

        var autoDestroy = root.AddComponent<AutoDestroy>();
        var so = new SerializedObject(autoDestroy);
        so.FindProperty("delay").floatValue = 4.8f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/FireballImpact.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Système à une seule salve, sans boucle. Une durée de vie / taille à 0 laisse la valeur à régler ensuite.
    internal static ParticleSystem Burst(string name, Transform parent, Material material, int count, float lifetime, float speed, float size, int sortingOrder)
    {
        ParticleSystem ps = CreateParticles(name, parent, material);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.startSpeed = speed;
        if (lifetime > 0f)
            main.startLifetime = lifetime;
        if (size > 0f)
            main.startSize = size;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        var shape = ps.shape;
        shape.enabled = false;
        VfxLibrary.Order(ps, sortingOrder);
        return ps;
    }

    internal static ParticleSystem CreateParticles(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        // Le sol est un sprite transparent : sans ça il est dessiné par-dessus les particules.
        renderer.sortingOrder = 10;
        var main = ps.main;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return ps;
    }

    internal static void SetColorOverLifetime(ParticleSystem ps, Color from, Color to, float alphaFrom, float alphaTo)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(alphaFrom, 0f), new GradientAlphaKey(alphaFrom, 0.3f), new GradientAlphaKey(alphaTo, 1f) });
        col.color = gradient;
    }

    internal static void SetSizeOverLifetime(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    internal static Material SaveParticleMaterial(string path, Texture2D texture, bool additive)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // Après les sprites transparents (le sol est un sprite qui sinon recouvre les particules).
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Textures ----------

    internal static Texture2D SaveTexture(string path, Texture2D texture, bool asSprite)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = !asSprite;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D MakeSoftGlow(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f) / (size * 0.5f);
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        return tex;
    }

    internal static Texture2D MakeSolid(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeFireballIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.58f, size * 0.42f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            // Fond sombre dégradé.
            Color bg = Color.Lerp(new Color(0.12f, 0.03f, 0.02f), new Color(0.3f, 0.06f, 0.02f), (float)y / size);

            // Boule de feu étirée vers le haut-gauche (direction de la traînée).
            Vector2 rel = p - center;
            Vector2 trailDir = new Vector2(-0.7f, 0.7f);
            float along = Mathf.Max(0f, Vector2.Dot(rel, trailDir));
            float stretch = 1f + along / (size * 0.18f);
            float r = new Vector2(rel.x, rel.y).magnitude / (size * 0.28f * Mathf.Sqrt(stretch));
            float flicker = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.35f;
            float heat = Mathf.Clamp01(1.15f - r - flicker * Mathf.Clamp01(r));

            Color fire = heat > 0.75f ? Color.Lerp(FireCore, Color.white, (heat - 0.75f) * 4f)
                       : heat > 0.35f ? Color.Lerp(FireMid, FireCore, (heat - 0.35f) / 0.4f)
                       : Color.Lerp(FireDark, FireMid, heat / 0.35f);
            Color c = Color.Lerp(bg, fire, Mathf.SmoothStep(0f, 1f, heat * 2.2f));
            c.a = 1f;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // ---------- UI ----------

    private static void BuildAbilityBar(AbilityCaster caster)
    {
        GameObject existing = GameObject.Find("HUD");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/T_UIWhite.png");
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "HUD");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Fond de la barre.
        var bar = CreateUI("AbilityBar", canvasGo.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.9f));
        var barRect = bar.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 24f);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var views = new AbilitySlotUI[caster.Slots.Length];
        for (int i = 0; i < views.Length; i++)
            views[i] = BuildSlot(i, bar.transform, white, font);

        var barUI = canvasGo.AddComponent<AbilityBarUI>();
        var so = new SerializedObject(barUI);
        so.FindProperty("caster").objectReferenceValue = caster;
        SerializedProperty viewsProp = so.FindProperty("slotViews");
        viewsProp.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++)
            viewsProp.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        so.ApplyModifiedProperties();
    }

    private static AbilitySlotUI BuildSlot(int index, Transform parent, Sprite white, Font font)
    {
        const float size = 84f;
        const float border = 3f;

        // Cadre doré façon LoL.
        Image frame = CreateUI($"Slot{index + 1}", parent, white, new Color(0.78f, 0.67f, 0.43f));
        frame.rectTransform.sizeDelta = new Vector2(size, size);

        Image icon = CreateUI("Icon", frame.transform, white, Color.white);
        Stretch(icon.rectTransform, border);

        Image overlay = CreateUI("CooldownOverlay", frame.transform, white, new Color(0f, 0f, 0f, 0.7f));
        Stretch(overlay.rectTransform, border);
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;

        Image flash = CreateUI("ReadyFlash", frame.transform, white, new Color(1f, 0.9f, 0.6f, 0f));
        Stretch(flash.rectTransform, border);

        Text cooldown = CreateText("CooldownText", frame.transform, font, 32, TextAnchor.MiddleCenter);
        Stretch(cooldown.rectTransform, 0f);

        // Petite étiquette de touche en bas.
        Image keyBg = CreateUI("KeyBadge", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.95f));
        RectTransform keyRect = keyBg.rectTransform;
        keyRect.anchorMin = keyRect.anchorMax = new Vector2(0.5f, 0f);
        keyRect.pivot = new Vector2(0.5f, 0.5f);
        keyRect.sizeDelta = new Vector2(26f, 22f);
        keyRect.anchoredPosition = Vector2.zero;
        Text key = CreateText("KeyText", keyBg.transform, font, 16, TextAnchor.MiddleCenter);
        Stretch(key.rectTransform, 0f);

        var view = frame.gameObject.AddComponent<AbilitySlotUI>();
        var so = new SerializedObject(view);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("cooldownOverlay").objectReferenceValue = overlay;
        so.FindProperty("cooldownText").objectReferenceValue = cooldown;
        so.FindProperty("keyText").objectReferenceValue = key;
        so.FindProperty("readyFlash").objectReferenceValue = flash;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    internal static Image CreateUI(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    internal static Text CreateText(string name, Transform parent, Font font, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }

    internal static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // ---------- Cible d'entraînement ----------

    private static void BuildTrainingDummy()
    {
        if (GameObject.Find("TrainingDummy") != null)
            return;

        GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(dummy, "TrainingDummy");
        dummy.name = "TrainingDummy";
        dummy.transform.position = new Vector3(0f, 1f, 8f);
        dummy.transform.localScale = new Vector3(1f, 2f, 1f);

        var health = dummy.AddComponent<Health>();
        var so = new SerializedObject(health);
        so.FindProperty("maxHealth").floatValue = 500f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
