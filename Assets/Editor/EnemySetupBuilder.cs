using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Génère les assets des ennemis (compétences, VFX de téléportation, prefab capsule)
// et installe l'EnemyManager et les barres de vie dans la scène ouverte.
public static class EnemySetupBuilder
{
    private const string Root = "Assets/Enemies";
    private const string ModelDir = Root + "/Models";
    private const string AnimationDir = Root + "/Animation";
    // Robots possibles (FBX dans Root) ; le premier sert de référence pour le graphe d'animation.
    private static readonly string[] ModelNames = { "Stan", "Mike", "George", "Leela" };
    private static readonly HashSet<string> LoopingClips = new HashSet<string> { "Idle", "Walk", "Run", "Walk_Tall", "Run_Tall", "Walk_Holding", "Run_Holding", "Dance" };
    // Hauteur des robots une fois mis à l'échelle (proche de celle du joueur) et rayon de leur capsule.
    private const float ModelHeight = 1.9f;
    private const float BodyRadius = 0.45f;
    // Doit correspondre au « Materialize Delay » de l'EnemyManager : moment du flash d'arrivée.
    private const float MaterializeDelay = 0.5f;

    // Cyan saturé : les intensités HDR l'éclaircissent déjà, un ton pâle finirait blanc.
    private static readonly Color TeleportCyan = new Color(0.1f, 0.7f, 1f);
    private static readonly Color TeleportLight = new Color(0.65f, 0.95f, 1f);
    // Hauteur d'où tombe le faisceau de téléportation.
    private const float BeamHeight = 12f;

    [MenuItem("Tools/Enemies/Build Enemy Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Root);
        BuildAbilities();
        BuildTeleportVfx();
        BuildEnemyPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Assets ennemis générés dans " + Root);
    }

    [MenuItem("Tools/Enemies/Setup Scene")]
    public static void SetupScene()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Enemy.prefab") == null)
            BuildAssets();

        GameObject player = GameObject.Find("Capsule");

        // Vie du joueur.
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth == null)
            playerHealth = Undo.AddComponent<Health>(player);
        var healthSo = new SerializedObject(playerHealth);
        healthSo.FindProperty("maxHealth").floatValue = 500f;
        healthSo.FindProperty("team").enumValueIndex = (int)Team.Player;
        healthSo.ApplyModifiedProperties();

        HealthBar playerBar = player.GetComponent<HealthBar>();
        if (playerBar == null)
            playerBar = Undo.AddComponent<HealthBar>(player);
        var barSo = new SerializedObject(playerBar);
        barSo.FindProperty("health").objectReferenceValue = playerHealth;
        barSo.FindProperty("fillColor").colorValue = new Color(0.2f, 0.85f, 0.3f);
        barSo.FindProperty("heightOffset").floatValue = 1.4f;
        barSo.ApplyModifiedProperties();

        // Gestionnaire d'ennemis.
        EnemyManager manager = Object.FindAnyObjectByType<EnemyManager>();
        if (manager == null)
        {
            var go = new GameObject("EnemyManager");
            Undo.RegisterCreatedObjectUndo(go, "EnemyManager");
            manager = go.AddComponent<EnemyManager>();
        }

        var so = new SerializedObject(manager);
        so.FindProperty("spawnArea").objectReferenceValue = Object.FindAnyObjectByType<MovementBounds>();
        so.FindProperty("teleportVfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/TeleportVfx.prefab");
        so.FindProperty("materializeDelay").floatValue = MaterializeDelay;
        so.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyController>($"{Root}/Enemy.prefab");
        so.FindProperty("fireballAbility").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Ability>($"{Root}/EnemyFireball.asset");
        so.FindProperty("laserAbility").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Ability>($"{Root}/EnemyLaser.asset");
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
    }

    // Copies des compétences du joueur, réglées pour les ennemis (moins de dégâts, plus lisibles à esquiver).
    private static void BuildAbilities()
    {
        var fireball = LoadOrCreate<FireballAbility>($"{Root}/EnemyFireball.asset");
        var so = new SerializedObject(fireball);
        so.FindProperty("displayName").stringValue = "Boule de feu (ennemi)";
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/Abilities/Fireball/Fireball.prefab");
        so.FindProperty("damage").floatValue = 40f;
        so.FindProperty("speed").floatValue = 13f;
        so.FindProperty("range").floatValue = 16f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var laser = LoadOrCreate<LaserAbility>($"{Root}/EnemyLaser.asset");
        so = new SerializedObject(laser);
        so.FindProperty("displayName").stringValue = "Laser (ennemi)";
        so.FindProperty("beamPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LaserBeam>("Assets/Abilities/Laser/LaserBeam.prefab");
        so.FindProperty("damage").floatValue = 60f;
        so.FindProperty("length").floatValue = 13f;
        // Ligne de visée plus longue que celle du joueur : laisse le temps d'esquiver.
        so.FindProperty("chargeTime").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // Ennemi : capsule de collision (gameplay) + un des modèles de robot, tiré au hasard à l'apparition.
    private static void BuildEnemyPrefab()
    {
        GameObject[] models = BuildModels();

        var root = new GameObject("Enemy");
        // Pivot au centre de la capsule (l'EnemyManager pose l'ennemi à une demi-hauteur du sol).
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = ModelHeight;
        capsule.radius = BodyRadius;
        capsule.center = Vector3.zero;

        Health health = root.AddComponent<Health>();
        var so = new SerializedObject(health);
        so.FindProperty("destroyOnDeath").boolValue = true;
        so.FindProperty("team").enumValueIndex = (int)Team.Enemy;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnemyController controller = root.AddComponent<EnemyController>();
        so = new SerializedObject(controller);
        SerializedProperty modelsProperty = so.FindProperty("models");
        modelsProperty.arraySize = models.Length;
        for (int i = 0; i < models.Length; i++)
            modelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        HealthBar bar = root.AddComponent<HealthBar>();
        so = new SerializedObject(bar);
        so.FindProperty("health").objectReferenceValue = health;
        so.FindProperty("fillColor").colorValue = new Color(0.9f, 0.2f, 0.15f);
        so.FindProperty("heightOffset").floatValue = ModelHeight * 0.5f + 0.35f;
        so.FindProperty("size").vector2Value = new Vector2(80f, 9f);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Enemy.prefab");
        Object.DestroyImmediate(root);
    }

    // ---------- Modèles et animations ----------

    // Prépare les robots (import, matériaux, animations) et renvoie un prefab par robot : modèle mis à l'échelle,
    // pieds à l'origine, Animator branché sur sa version du graphe d'animation.
    [MenuItem("Tools/Enemies/Rebuild Enemy Models")]
    public static GameObject[] BuildModels()
    {
        Directory.CreateDirectory(ModelDir);
        foreach (string name in ModelNames)
            ConfigureImport(name);

        AnimatorController controller = BuildAnimatorController(ModelNames[0]);
        var prefabs = new GameObject[ModelNames.Length];
        for (int i = 0; i < ModelNames.Length; i++)
        {
            string name = ModelNames[i];
            string fbx = $"{Root}/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model_" + name;
            instance.transform.localScale = Vector3.one * (ModelHeight / MeasureHeight(model, fbx));

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = BuildOverride(controller, name);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = ShadowCastingMode.On;
            }

            prefabs[i] = PrefabUtility.SaveAsPrefabAsset(instance, $"{ModelDir}/Model_{name}.prefab");
            Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
        return prefabs;
    }

    // Rig générique avec avatar, clips renommés sans le préfixe « RobotArmature| », repos et marche en boucle,
    // matériaux sortis du FBX (la carrosserie et les yeux reçoivent la couleur de l'attaque en émissif).
    private static void ConfigureImport(string name)
    {
        string path = $"{Root}/{name}.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            int bar = clip.name.LastIndexOf('|');
            if (bar >= 0)
                clip.name = clip.name.Substring(bar + 1);
            clip.loopTime = LoopingClips.Contains(clip.name);
        }
        importer.clipAnimations = clips;

        string materialDir = $"{ModelDir}/Materials/{name}";
        Directory.CreateDirectory(materialDir);
        foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
        {
            string dest = $"{materialDir}/{embedded.name}.mat";
            Material external = AssetDatabase.LoadAssetAtPath<Material>(dest);
            if (external == null)
            {
                external = new Material(embedded);
                AssetDatabase.CreateAsset(external, dest);
            }
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), external);
        }
        importer.SaveAndReimport();

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { materialDir }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m.name != "Main" && m.name != "Eye")
                continue;
            // Émission activée mais noire : sa couleur est fixée par ennemi (EnemyController).
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }
    }

    // Hauteur du modèle dans sa pose de repos (le maillage seul ne le dit pas : l'armature est mise à l'échelle).
    private static float MeasureHeight(GameObject model, string fbx)
    {
        var instance = (GameObject)Object.Instantiate(model);
        try
        {
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            if (idle != null)
                idle.SampleAnimation(instance, 0f);
            float min = float.MaxValue, max = float.MinValue;
            foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                Matrix4x4 m = smr.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices)
                {
                    float y = m.MultiplyPoint3x4(v).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
                Object.DestroyImmediate(baked);
            }
            return max > min ? max - min : ModelHeight;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // Graphe commun : Repos <-> Marche selon la vitesse, et deux attaques déclenchées par trigger qui reviennent au repos.
    // Boule de feu = coup de poing (le projectile part à l'impact) ; laser = pose de tir, bras tendu pendant la charge.
    private static AnimatorController BuildAnimatorController(string referenceModel)
    {
        Directory.CreateDirectory(AnimationDir);
        string path = $"{AnimationDir}/Enemy.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("WalkSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Fireball", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Laser", AnimatorControllerParameterType.Trigger);

        string fbx = $"{Root}/{referenceModel}.fbx";
        AnimationClip Clip(string clipName) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => c.name == clipName);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle", new Vector3(300f, 0f));
        idle.motion = Clip("Idle");
        AnimatorState walk = sm.AddState("Walk", new Vector3(300f, 120f));
        walk.motion = Clip("Walk");
        walk.speedParameterActive = true;
        walk.speedParameter = "WalkSpeed";
        AnimatorState fireball = sm.AddState("Fireball", new Vector3(600f, -80f));
        fireball.motion = Clip("Punch");
        AnimatorState laser = sm.AddState("Laser", new Vector3(600f, 80f));
        laser.motion = Clip("Shoot");
        // Pose de tir tenue pendant la charge du laser.
        laser.speed = 0.8f;
        sm.defaultState = idle;

        AnimatorStateTransition t = idle.AddTransition(walk);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
        t = walk.AddTransition(idle);
        t.hasExitTime = false;
        t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

        foreach ((AnimatorState state, string trigger) in new[] { (fireball, "Fireball"), (laser, "Laser") })
        {
            t = sm.AddAnyStateTransition(state);
            t.hasExitTime = false;
            t.duration = 0.08f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t = state.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.2f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // Version du graphe pour un robot : mêmes états, ses propres clips (chaque robot a son squelette).
    private static AnimatorOverrideController BuildOverride(AnimatorController controller, string name)
    {
        string path = $"{AnimationDir}/Enemy_{name}.overrideController";
        var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(overrideController, path);
        }
        overrideController.runtimeAnimatorController = controller;

        AnimationClip[] own = AssetDatabase.LoadAllAssetsAtPath($"{Root}/{name}.fbx").OfType<AnimationClip>().ToArray();
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (AnimationClip clip in controller.animationClips.Distinct())
        {
            AnimationClip match = own.FirstOrDefault(c => c.name == clip.name);
            if (match == null)
                Debug.LogWarning($"{name} : pas de clip « {clip.name} », celui de {ModelNames[0]} est utilisé.");
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, match));
        }
        overrideController.ApplyOverrides(overrides);
        EditorUtility.SetDirty(overrideController);
        return overrideController;
    }

    // ---------- VFX de téléportation ----------

    // Téléportation futuriste : un faisceau de particules tombe du ciel, des anneaux de scan remontent le long du
    // rayon, puis flash, onde de choc et pixels de numérisation au moment où l'ennemi se matérialise.
    [MenuItem("Tools/Enemies/Rebuild Teleport VFX")]
    public static void BuildTeleportVfx()
    {
        Directory.CreateDirectory(Root);
        SigilLibrary.EnsureTextures();
        Material streakMat = VfxLibrary.Material($"{Root}/M_TeleportStreak.mat", VfxLibrary.Tex("T_Spark"), 6f);
        Material beamCoreMat = VfxLibrary.Material($"{Root}/M_TeleportBeamCore.mat", VfxLibrary.Tex("T_BeamCore"), 5f);
        Material beamGlowMat = VfxLibrary.Material($"{Root}/M_TeleportBeamGlow.mat", VfxLibrary.Tex("T_BeamCore"), 1.5f);
        Material sigilMat = VfxLibrary.Material($"{Root}/M_TeleportSigil.mat", VfxLibrary.Tex("T_HoloSigil"), 3f);
        Material ringMat = VfxLibrary.Material($"{Root}/M_TeleportScanRing.mat", VfxLibrary.Tex("T_HoloRing"), 3f);
        Material glowMat = VfxLibrary.Material($"{Root}/M_TeleportGlow.mat", VfxLibrary.Tex("T_Glow"), 1.6f);
        Material pixelMat = VfxLibrary.Material($"{Root}/M_TeleportPixel.mat", VfxLibrary.Tex("T_Pixel"), 5f);
        Material flareMat = VfxLibrary.Material($"{Root}/M_TeleportFlare.mat", VfxLibrary.Tex("T_Flare"), 6f);

        var root = new GameObject("TeleportVfx");
        var white = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        var fadeInOut = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) };
        var fadeOut = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };

        // Réticule holographique au sol, qui s'ouvre en tournant puis se referme.
        ParticleSystem sigil = GroundDecal("Sigil", root.transform, sigilMat, 3f, 1.2f, TeleportLight, 11);
        VfxLibrary.SizeOverLifetime(sigil, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0.6f)));
        VfxLibrary.ColorOverLifetime(sigil, white, fadeInOut);
        SpinConstant(sigil, 90f);
        ParticleSystem groundGlow = GroundDecal("GroundGlow", root.transform, glowMat, 3.5f, 1.2f, new Color(TeleportCyan.r, TeleportCyan.g, TeleportCyan.b, 0.45f), 10);
        VfxLibrary.ColorOverLifetime(groundGlow, white, fadeInOut);

        // Faisceau : traînées de particules qui tombent du ciel à grande vitesse, concentrées sur une colonne étroite.
        ParticleSystem streaks = FireballSetupBuilder.CreateParticles("BeamStreaks", root.transform, streakMat);
        streaks.transform.SetLocalPositionAndRotation(new Vector3(0f, BeamHeight, 0f), Quaternion.Euler(90f, 0f, 0f));
        var main = streaks.main;
        main.loop = false;
        main.duration = MaterializeDelay + 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(36f, 40f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, TeleportLight);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = streaks.emission;
        emission.rateOverTime = 220f;
        var shape = streaks.shape;
        // Cône d'ouverture nulle : émission droit vers le bas (un cercle émettrait à l'horizontale).
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = 0.3f;
        shape.radiusThickness = 1f;
        VfxLibrary.Stretch(streaks, 0.045f, 1f);
        VfxLibrary.ColorOverLifetime(streaks, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(TeleportCyan, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 1f) });
        VfxLibrary.Order(streaks, 13);

        // Colonne de lumière : coeur fin et halo large, qui s'élargissent au moment de l'arrivée puis s'éteignent.
        var beamWidth = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.1f, 1f), new Keyframe(0.45f, 0.8f), new Keyframe(0.52f, 2.2f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f));
        foreach ((string name, Material material, float width, Color color) in new[]
        {
            ("BeamGlow", beamGlowMat, 1.4f, new Color(TeleportCyan.r, TeleportCyan.g, TeleportCyan.b, 0.55f)),
            ("BeamCore", beamCoreMat, 0.3f, TeleportLight),
        })
        {
            ParticleSystem beam = Pillar(name, root.transform, material, width, color);
            // Le billboard vertical s'étend vers le haut depuis la particule : la base est posée au sol.
            beam.transform.localPosition = Vector3.zero;
            var beamMain = beam.main;
            beamMain.startDelay = 0.05f;
            beamMain.startLifetime = 0.95f;
            beamMain.startSizeY = BeamHeight;
            var size = beam.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, beamWidth);
            size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            VfxLibrary.ColorOverLifetime(beam, white, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        }

        // Anneaux de scan qui remontent le long du faisceau.
        ParticleSystem scan = FireballSetupBuilder.CreateParticles("ScanRings", root.transform, ringMat);
        scan.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        scan.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        main = scan.main;
        main.loop = false;
        main.duration = MaterializeDelay + 0.1f;
        main.startLifetime = 0.7f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 1.7f);
        main.startColor = TeleportLight;
        emission = scan.emission;
        emission.rateOverTime = 12f;
        shape = scan.shape;
        shape.enabled = false;
        var velocity = scan.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        VfxLibrary.SizeOverLifetime(scan, AnimationCurve.Linear(0f, 1f, 1f, 0.55f));
        VfxLibrary.ColorOverLifetime(scan, white, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Order(scan, 12);

        // Pixels de numérisation qui montent autour de la silhouette juste avant l'arrivée.
        ParticleSystem digitize = FireballSetupBuilder.CreateParticles("Digitize", root.transform, pixelMat);
        digitize.transform.localPosition = new Vector3(0f, 1f, 0f);
        main = digitize.main;
        main.loop = false;
        main.startDelay = MaterializeDelay - 0.2f;
        main.duration = 0.45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, TeleportCyan);
        emission = digitize.emission;
        emission.rateOverTime = 70f;
        shape = digitize.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.9f, 2f, 0.9f);
        velocity = digitize.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        VfxLibrary.ColorOverLifetime(digitize, white, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Order(digitize, 13);

        // Arrivée : flash, onde de choc au sol et gerbe de pixels.
        ParticleSystem flash = FireballSetupBuilder.Burst("ArrivalFlash", root.transform, flareMat, 1, 0.22f, 0f, 4.5f, 15);
        flash.transform.localPosition = new Vector3(0f, 1f, 0f);
        main = flash.main;
        main.startDelay = MaterializeDelay;
        main.startColor = TeleportLight;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        VfxLibrary.SizeOverLifetime(flash, AnimationCurve.Linear(0f, 0.5f, 1f, 1.3f));
        VfxLibrary.ColorOverLifetime(flash, white, fadeOut);

        ParticleSystem shockwave = GroundDecal("Shockwave", root.transform, ringMat, 6f, 0.45f, TeleportLight, 11);
        main = shockwave.main;
        main.startDelay = MaterializeDelay;
        VfxLibrary.SizeOverLifetime(shockwave, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0.3f, 0f)));
        VfxLibrary.ColorOverLifetime(shockwave, white, fadeOut);

        ParticleSystem pixels = FireballSetupBuilder.Burst("ArrivalPixels", root.transform, pixelMat, 45, 0f, 0f, 0f, 14);
        pixels.transform.localPosition = new Vector3(0f, 1f, 0f);
        main = pixels.main;
        main.startDelay = MaterializeDelay;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, TeleportCyan);
        main.gravityModifier = -0.3f;
        shape = pixels.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
        VfxLibrary.Drag(pixels, 3f);
        VfxLibrary.ColorOverLifetime(pixels, white, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

        Light light = VfxLibrary.PointLight("Light", root.transform, TeleportCyan, 7f, 8f);
        light.transform.localPosition = new Vector3(0f, 1f, 0f);
        var fade = new SerializedObject(light.gameObject.AddComponent<LightFade>());
        fade.FindProperty("duration").floatValue = 1.1f;
        fade.ApplyModifiedPropertiesWithoutUndo();

        var autoDestroy = new SerializedObject(root.AddComponent<AutoDestroy>());
        autoDestroy.FindProperty("delay").floatValue = 2f;
        autoDestroy.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/TeleportVfx.prefab");
        Object.DestroyImmediate(root);
    }

    // Particule unique posée à plat au sol.
    private static ParticleSystem GroundDecal(string name, Transform parent, Material material, float size, float lifetime, Color color, int sortingOrder)
    {
        ParticleSystem ps = FireballSetupBuilder.Burst(name, parent, material, 1, lifetime, 0f, size, sortingOrder);
        ps.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var main = ps.main;
        main.startColor = color;
        return ps;
    }

    // Halo étiré verticalement (toujours face à la caméra, mais droit).
    private static ParticleSystem Pillar(string name, Transform parent, Material material, float width, Color color)
    {
        ParticleSystem ps = FireballSetupBuilder.Burst(name, parent, material, 1, 0.75f, 0f, 0f, 12);
        ps.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        var main = ps.main;
        main.startDelay = 0.1f;
        main.startSize3D = true;
        main.startSizeX = width;
        main.startSizeY = 4.2f;
        main.startSizeZ = 1f;
        main.startColor = color;
        return ps;
    }

    private static void SpinConstant(ParticleSystem ps, float degreesPerSecond)
    {
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = degreesPerSecond * Mathf.Deg2Rad;
    }
}
