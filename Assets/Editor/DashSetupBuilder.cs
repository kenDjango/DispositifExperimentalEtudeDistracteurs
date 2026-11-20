using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Génère les assets du dash (textures, matériaux, VFX de super-vitesse, compétence) et l'assigne à la touche E.
public static class DashSetupBuilder
{
    private const string Root = "Assets/Abilities/Dash";
    private const string ArtRoot = FireballSetupBuilder.ArtRoot;
    private const int DashSlot = 2;

    // Couleurs du dash (reprises par la recharge de dash).
    internal static readonly Color BoltWhite = new Color(1f, 0.98f, 0.85f);
    internal static readonly Color SpeedYellow = new Color(1f, 0.82f, 0.1f);
    internal static readonly Color SpeedOrange = new Color(1f, 0.45f, 0.05f);
    private static readonly Color SpeedRed = new Color(0.85f, 0.08f, 0.04f);

    [MenuItem("Tools/Abilities/Build Dash Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ArtRoot);

        FireballSetupBuilder.SaveTexture($"{ArtRoot}/T_IconDash.png", MakeDashIcon(128), true);
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/T_IconDash.png");

        DashVfx vfx = BuildVfxPrefab();

        string abilityPath = $"{Root}/Dash.asset";
        DashAbility ability = AssetDatabase.LoadAssetAtPath<DashAbility>(abilityPath);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<DashAbility>();
            AssetDatabase.CreateAsset(ability, abilityPath);
        }

        var so = new SerializedObject(ability);
        so.FindProperty("displayName").stringValue = "Dash";
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("cooldown").floatValue = 6f;
        so.FindProperty("maxDistance").floatValue = 6f;
        so.FindProperty("duration").floatValue = 0.15f;
        so.FindProperty("vfxPrefab").objectReferenceValue = vfx;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        Debug.Log("Dash assets générés dans " + Root);
    }

    [MenuItem("Tools/Abilities/Setup Dash In Scene")]
    public static void SetupScene()
    {
        DashAbility ability = AssetDatabase.LoadAssetAtPath<DashAbility>($"{Root}/Dash.asset");
        if (ability == null)
        {
            BuildAssets();
            ability = AssetDatabase.LoadAssetAtPath<DashAbility>($"{Root}/Dash.asset");
        }

        GameObject player = GameObject.Find("Capsule");
        AbilityCaster caster = player.GetComponent<AbilityCaster>();
        var so = new SerializedObject(caster);
        so.FindProperty("slots").GetArrayElementAtIndex(DashSlot).FindPropertyRelative("ability").objectReferenceValue = ability;
        so.ApplyModifiedProperties();

        if (player.GetComponent<DashMotor>() == null)
            Undo.AddComponent<DashMotor>(player);

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
    }

    // Regénère uniquement le VFX (matériaux, prefab) sans toucher aux réglages de la compétence.
    [MenuItem("Tools/Abilities/Rebuild Dash VFX")]
    public static DashVfx BuildVfxPrefab()
    {
        VfxLibrary.EnsureTextures();
        Texture2D white = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/T_UIWhite.png");

        // Intensités HDR : le bloom fait rayonner les traînées et les éclairs.
        Material trailMat = VfxLibrary.Material($"{Root}/M_SpeedTrail.mat", VfxLibrary.Tex("T_TrailStreak"), 2.5f);
        Material trailCoreMat = VfxLibrary.Material($"{Root}/M_SpeedTrailCore.mat", VfxLibrary.Tex("T_Band"), 5f);
        Material sparkMat = VfxLibrary.Material($"{Root}/M_SpeedSpark.mat", VfxLibrary.Tex("T_Spark"), 6f);
        Material boltMat = VfxLibrary.Material($"{Root}/M_Bolt.mat", VfxLibrary.Tex("T_BoltBand"), 7f);
        Material boltGlowMat = VfxLibrary.Material($"{Root}/M_BoltGlow.mat", VfxLibrary.Tex("T_Band"), 2.5f);
        Material dustMat = VfxLibrary.Material($"{Root}/M_Dust.mat", VfxLibrary.Tex("T_SmokeSheet"), 1f, true);
        // Couleur des images rémanentes appliquée par DashGhost (en HDR, voir ghostColor plus bas).
        Material ghostMat = VfxLibrary.Material($"{Root}/M_DashGhost.mat", white, 1f);

        var root = new GameObject("DashVfx");

        // Large traînée rouge/orange striée qui donne la masse du mouvement.
        CreateTrail("TrailGlow", root.transform, trailMat, Vector3.zero, 1.1f, 0.28f,
            new[] { new GradientColorKey(SpeedOrange, 0f), new GradientColorKey(SpeedRed, 0.5f), new GradientColorKey(SpeedRed, 1f) },
            new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) });

        // Coeur jaune-blanc, plus fin et plus court.
        CreateTrail("TrailCore", root.transform, trailCoreMat, Vector3.zero, 0.45f, 0.18f,
            new[] { new GradientColorKey(BoltWhite, 0f), new GradientColorKey(SpeedYellow, 0.4f), new GradientColorKey(SpeedOrange, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });

        // Fines lignes de vitesse en haut et en bas du corps.
        Vector3[] streakOffsets = { new Vector3(0.3f, 0.65f, 0f), new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.25f, -0.35f, 0f), new Vector3(-0.2f, -0.7f, 0f) };
        for (int i = 0; i < streakOffsets.Length; i++)
        {
            CreateTrail($"Streak{i + 1}", root.transform, trailCoreMat, streakOffsets[i], 0.06f, 0.35f,
                new[] { new GradientColorKey(BoltWhite, 0f), new GradientColorKey(SpeedYellow, 0.3f), new GradientColorKey(SpeedOrange, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        }

        // Étincelles électriques laissées sur le trajet.
        ParticleSystem sparks = FireballSetupBuilder.CreateParticles("Sparks", root.transform, sparkMat);
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(BoltWhite, SpeedYellow);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.6f;
        main.maxParticles = 400;
        var emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 30f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        VfxLibrary.Drag(sparks, 1.5f);
        VfxLibrary.Turbulence(sparks, 1f, 2f, 1f);
        VfxLibrary.Stretch(sparks, 0.05f, 1.5f);
        FireballSetupBuilder.SetColorOverLifetime(sparks, BoltWhite, SpeedOrange, 1f, 0f);
        VfxLibrary.Order(sparks, 13);

        // Poussière soulevée au ras du sol (le VFX suit le centre du personnage, ~0.8 au-dessus des pieds).
        ParticleSystem dust = FireballSetupBuilder.CreateParticles("Dust", root.transform, dustMat);
        dust.transform.localPosition = new Vector3(0f, -0.75f, 0f);
        main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.95f, 0.9f, 0.82f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        emission = dust.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 6f;
        shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        VfxLibrary.Drag(dust, 2f);
        VfxLibrary.SizeOverLifetime(dust, AnimationCurve.EaseInOut(0f, 0.8f, 1f, 2f));
        VfxLibrary.Spin(dust, 40f);
        VfxLibrary.ColorOverLifetime(dust, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.15f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Flipbook(dust);
        VfxLibrary.Order(dust, 9);

        // Éclairs (redessinés en continu par DashVfx) : trait fin éblouissant + halo large.
        var bolts = new LineRenderer[4];
        var boltGlows = new LineRenderer[bolts.Length];
        for (int i = 0; i < bolts.Length; i++)
        {
            bolts[i] = CreateBolt($"Bolt{i + 1}", root.transform, boltMat, 0.05f, 1f, 12);
            boltGlows[i] = CreateBolt($"BoltGlow{i + 1}", root.transform, boltGlowMat, 0.35f, 0.45f, 11);
        }

        Light light = VfxLibrary.PointLight("Light", root.transform, SpeedYellow, 5f, 4f);
        light.gameObject.AddComponent<LightFlicker>();

        DashVfx vfx = root.AddComponent<DashVfx>();
        var so = new SerializedObject(vfx);
        so.FindProperty("ghostMaterial").objectReferenceValue = ghostMat;
        so.FindProperty("ghostColor").colorValue = new Color(2f, 0.8f, 0.1f, 0.45f);
        so.FindProperty("boltSegments").intValue = 12;
        so.FindProperty("boltJitter").floatValue = 0.14f;
        SerializedProperty boltsProp = so.FindProperty("bolts");
        SerializedProperty glowsProp = so.FindProperty("boltGlows");
        boltsProp.arraySize = bolts.Length;
        glowsProp.arraySize = boltGlows.Length;
        for (int i = 0; i < bolts.Length; i++)
        {
            boltsProp.GetArrayElementAtIndex(i).objectReferenceValue = bolts[i];
            glowsProp.GetArrayElementAtIndex(i).objectReferenceValue = boltGlows[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/DashVfx.prefab");
        Object.DestroyImmediate(root);
        DashVfx result = prefab.GetComponent<DashVfx>();

        DashAbility ability = AssetDatabase.LoadAssetAtPath<DashAbility>($"{Root}/Dash.asset");
        if (ability != null)
        {
            var abilitySo = new SerializedObject(ability);
            abilitySo.FindProperty("vfxPrefab").objectReferenceValue = result;
            abilitySo.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static void CreateTrail(string name, Transform parent, Material material, Vector3 offset, float width, float time,
        GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = time;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = width;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        trail.colorGradient = gradient;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingOrder = 10;
    }

    private static LineRenderer CreateBolt(string name, Transform parent, Material material, float width, float alpha, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
        line.alignment = LineAlignment.View;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(BoltWhite, 0f), new GradientColorKey(SpeedYellow, 1f) },
            new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.7f, 1f) });
        line.colorGradient = gradient;
        line.positionCount = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        return line;
    }

    private static Texture2D MakeDashIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Éclair en zigzag (coordonnées normalisées).
        Vector2[] bolt =
        {
            new Vector2(0.62f, 0.9f), new Vector2(0.4f, 0.55f), new Vector2(0.58f, 0.52f),
            new Vector2(0.36f, 0.1f),
        };
        float[] streakY = { 0.25f, 0.42f, 0.6f, 0.77f };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            Color c = Color.Lerp(new Color(0.25f, 0.03f, 0.03f), new Color(0.5f, 0.08f, 0.03f), p.y);

            // Lignes de vitesse vers la gauche.
            foreach (float sy in streakY)
            {
                float d = Mathf.Abs(p.y - sy);
                float len = Mathf.Clamp01((p.x - 0.05f) / 0.6f);
                float streak = Mathf.Clamp01(1f - d / 0.025f) * len * (p.x < 0.75f ? 1f : 0f);
                c = Color.Lerp(c, SpeedOrange, streak * 0.8f);
            }

            float boltDist = float.MaxValue;
            for (int i = 0; i < bolt.Length - 1; i++)
                boltDist = Mathf.Min(boltDist, DistanceToSegment(p, bolt[i], bolt[i + 1]));
            float glow = Mathf.Clamp01(1f - boltDist / 0.12f);
            c = Color.Lerp(c, SpeedYellow, glow * glow);
            float core = Mathf.Clamp01(1f - boltDist / 0.035f);
            c = Color.Lerp(c, BoltWhite, core);

            c.a = 1f;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
