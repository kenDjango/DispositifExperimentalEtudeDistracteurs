using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Araignée mécanique décorative, générée à partir de Assets/Spider.fbx :
// - nettoyage : le plan « Box » (parent du squelette) perd son maillage, la fourrure est retirée ;
// - UV refaits par projection cubique (ceux du FBX ne sont pas dépliés) et textures procédurales de plaques
//   de métal (couleur, métal/brillance, relief, lueurs) ;
// - graphe d'animation : locomotion dans toutes les directions (marche/course) et gestes à l'arrêt ;
// - prefab orienté vers +Z, à l'échelle des robots, animé d'après son déplacement (SpiderAnimator).
public static class SpiderSetupBuilder
{
    private const string Fbx = "Assets/Spider.fbx";
    private const string Root = "Assets/Decor/Spider";
    private const string TexDir = Root + "/Textures";
    private const string MatDir = Root + "/Materials";
    private const string MeshDir = Root + "/Meshes";

    // Envergure des pattes (m) une fois mise à l'échelle : petite bestiole aux pieds des robots, mais lisible.
    private const float LegSpan = 0.75f;
    // Côté d'une répétition de la texture, en unités du maillage d'origine (le corps en fait environ 0,5).
    // Grandes plaques : à la taille de l'araignée, un motif fin se lit comme du bruit.
    private const float TileSize = 0.25f;
    private const int TexSize = 512;
    // Plaques par répétition de texture (dans chaque direction).
    private const int PanelGrid = 4;

    private static readonly Color Glow = new Color(0.2f, 0.85f, 1f);
    private static readonly Color EyeRed = new Color(1f, 0.12f, 0.05f);
    // Sablier de veuve noire sur le dos : la signature qui la fait reconnaître comme une araignée.
    private static readonly Color MarkRed = new Color(1f, 0.08f, 0.03f) * 3f;

    private static readonly string[] LoopingClips =
    {
        "warte_pose", "walk_ani_vor", "walk_ani_back", "walk_left", "walk_right",
        "run_ani_vor", "run_ani_back", "run_left", "run_right",
    };

    [MenuItem("Tools/Decor/Build Mechanical Spider")]
    public static void Build()
    {
        foreach (string dir in new[] { TexDir, MatDir, MeshDir })
            Directory.CreateDirectory(dir);

        ConfigureImport();
        Material metal = BuildMetalMaterial();
        Material eyes = LitMaterial("M_SpiderEyes", new Color(0.05f, 0f, 0f), 0f, 0.9f, EyeRed * 6f);
        Material fangs = LitMaterial("M_SpiderFangs", new Color(0.22f, 0.22f, 0.24f), 1f, 0.65f, Color.black);
        Material mark = BuildMarkMaterial();

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            BuildPrefab(preview, metal, eyes, fangs, mark);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
        AssetDatabase.SaveAssets();
        // Sans réimport, le prefab en mémoire garde une référence périmée au maillage régénéré (affiché vide).
        AssetDatabase.ImportAsset($"{Root}/MechSpider.prefab", ImportAssetOptions.ForceUpdate);
        Debug.Log("Araignée mécanique générée : " + Root + "/MechSpider.prefab");
        InstallSwarm();
    }

    // ---------- Scène ----------

    // Essaim d'araignées qui suivent les robots dans la scène ouverte (les araignées hors de l'essaim sont supprimées).
    private static void InstallSwarm()
    {
        EnemyManager manager = Object.FindAnyObjectByType<EnemyManager>();
        if (manager == null)
            return;

        foreach (SpiderAnimator loose in Object.FindObjectsByType<SpiderAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (loose.GetComponentInParent<SpiderSwarm>(true) == null)
                Object.DestroyImmediate(loose.gameObject);

        SpiderSwarm swarm = Object.FindAnyObjectByType<SpiderSwarm>(FindObjectsInactive.Include);
        if (swarm == null)
            swarm = new GameObject("Spiders").AddComponent<SpiderSwarm>();

        GameObject bounds = GameObject.Find("PlayerBounds");
        var so = new SerializedObject(swarm);
        so.FindProperty("spiderPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpiderAnimator>($"{Root}/MechSpider.prefab");
        so.FindProperty("enemies").objectReferenceValue = manager;
        so.FindProperty("area").objectReferenceValue = bounds != null ? bounds.GetComponent<MovementBounds>() : null;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(swarm.gameObject.scene);
        EditorSceneManager.SaveScene(swarm.gameObject.scene);
    }

    // ---------- Import ----------

    // Boucle des clips de locomotion et du repos (les noms restent ceux du FBX).
    private static void ConfigureImport()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
            clip.loopTime = LoopingClips.Contains(ShortName(clip.name));
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static string ShortName(string clipName) => clipName.Substring(clipName.LastIndexOf('|') + 1);

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__"))
            .ToDictionary(c => ShortName(c.name));
    }

    // ---------- Prefab ----------

    private static void BuildPrefab(Scene scene, Material metal, Material eyes, Material fangs, Material mark)
    {
        var root = new GameObject("MechSpider");
        SceneManager.MoveGameObjectToScene(root, scene);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), scene);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);
        // Le modèle avance vers -Z : demi-tour pour suivre la convention Unity (+Z).
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Nettoyage : le plan « Box » garde son nœud (parent du squelette) mais plus son maillage ; pas de fourrure.
        Transform box = model.transform.Find("Box");
        Object.DestroyImmediate(box.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(box.GetComponent<MeshFilter>());
        Object.DestroyImmediate(model.transform.Find("Spider_Fur").gameObject);

        foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            switch (smr.name)
            {
                case "Spider":
                    smr.sharedMesh = BoxProjectedCopy(smr.sharedMesh);
                    smr.sharedMaterial = metal;
                    break;
                case "Spider_Eyes":
                    smr.sharedMaterial = eyes;
                    break;
                default:
                    smr.sharedMaterial = fangs;
                    break;
            }
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.updateWhenOffscreen = false;
        }

        Dictionary<string, AnimationClip> clips = LoadClips();

        // Mise à l'échelle d'après l'envergure en pose de repos.
        clips["warte_pose"].SampleAnimation(model, 0f);
        AddBackMark(model, mark);
        model.transform.localScale = Vector3.one * (LegSpan / MeasureSpan(model));

        // Vitesse de déplacement de chaque clip à cette échelle (repère de l'araignée, +Z devant).
        Dictionary<string, Vector2> speeds = LoopingClips.Where(n => n != "warte_pose").ToDictionary(n => n, n => MeasureVelocity(root.transform, model, clips[n]));
        clips["warte_pose"].SampleAnimation(model, 0f);

        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = BuildController(clips, speeds);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var driver = new SerializedObject(root.AddComponent<SpiderAnimator>());
        driver.FindProperty("animator").objectReferenceValue = animator;
        driver.FindProperty("runSpeed").floatValue = speeds["run_ani_vor"].magnitude;
        // Petite araignée qui suit des robots bien plus rapides que sa course : pattes jouées jusqu'à 12× plus vite.
        driver.FindProperty("maxAnimSpeed").floatValue = 12f;
        driver.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/MechSpider.prefab");
        Object.DestroyImmediate(root);

        Debug.Log("Vitesses des clips (m/s) : " + string.Join(", ", speeds.Select(s => $"{s.Key} {s.Value.magnitude:0.00}")));
    }

    // Envergure (plus grande dimension au sol) du maillage déformé dans la pose courante.
    private static float MeasureSpan(GameObject model)
    {
        var bounds = new Bounds(model.transform.position, Vector3.zero);
        foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            foreach (Vector3 v in baked.vertices)
                bounds.Encapsulate(smr.transform.TransformPoint(v));
            Object.DestroyImmediate(baked);
        }
        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    // Sablier posé sur le dessus de l'abdomen (pose courante), attaché à son os pour suivre l'animation.
    private static void AddBackMark(GameObject model, Material mark)
    {
        Transform[] bones = model.GetComponentsInChildren<Transform>();
        Transform abdomen = bones.First(t => t.name == "Body2");
        Transform tail = bones.First(t => t.name == "Body2_end");
        Vector3 center = (abdomen.position + tail.position) * 0.5f;
        float length = Vector3.Distance(abdomen.position, tail.position);

        // Point le plus haut de la carapace au-dessus du milieu de l'abdomen.
        SkinnedMeshRenderer body = model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Spider");
        var baked = new Mesh();
        body.BakeMesh(baked, true);
        float top = center.y;
        foreach (Vector3 v in baked.vertices)
        {
            Vector3 p = body.transform.TransformPoint(v);
            if (new Vector2(p.x - center.x, p.z - center.z).magnitude < length * 0.12f)
                top = Mathf.Max(top, p.y);
        }
        Object.DestroyImmediate(baked);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BackMark";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        // À plat, la pointe du sablier dans l'axe de l'araignée.
        Vector3 forward = Vector3.ProjectOnPlane(abdomen.position - tail.position, Vector3.up).normalized;
        quad.transform.SetPositionAndRotation(new Vector3(center.x, top + length * 0.02f, center.z), Quaternion.LookRotation(Vector3.down, forward));
        quad.transform.localScale = new Vector3(length * 0.38f, length * 0.55f, 1f);
        quad.transform.SetParent(abdomen, true);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mark;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Les clips sont joués sur place : les pieds au sol reculent à la vitesse à laquelle le corps avancerait.
    // Vitesse du corps = -(vitesse moyenne des extrémités des pattes quand elles sont au plus bas).
    private static Vector2 MeasureVelocity(Transform root, GameObject model, AnimationClip clip)
    {
        Transform[] tips = model.GetComponentsInChildren<Transform>().Where(t => t.childCount == 0 && t.GetComponent<Renderer>() == null).ToArray();
        const int steps = 120;
        float dt = clip.length / steps;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform tip in tips)
        {
            var positions = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                clip.SampleAnimation(model, clip.length * i / steps);
                positions[i] = root.InverseTransformPoint(tip.position);
            }
            float min = positions.Min(p => p.y), max = positions.Max(p => p.y);
            // Extrémité qui ne se lève pas : pas une patte.
            if (max - min < LegSpan * 0.005f)
                continue;
            for (int i = 0; i < steps; i++)
            {
                if (positions[i].y > min + (max - min) * 0.1f)
                    continue;
                sum += (positions[i + 1] - positions[i]) / dt;
                count++;
            }
        }
        Vector3 foot = count > 0 ? sum / count : Vector3.zero;
        return new Vector2(-foot.x, -foot.z);
    }

    // ---------- Graphe d'animation ----------

    // Locomotion : mélange 2D (Strafe, Forward) en m/s entre le repos, la marche et la course dans les quatre
    // directions, placés à la vitesse réelle de chaque clip (pas de glissement des pattes).
    // Gestes à l'arrêt : déclencheur « Fidget », FidgetIndex choisit le geste (0 : se dresse, 1 : sautille).
    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, Dictionary<string, Vector2> speeds)
    {
        string path = $"{Root}/MechSpider.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Forward", AnimatorControllerParameterType.Float);
        controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter("Fidget", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("FidgetIndex", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "Strafe";
        tree.blendParameterY = "Forward";
        tree.AddChild(clips["warte_pose"], Vector2.zero);
        foreach (KeyValuePair<string, Vector2> speed in speeds)
            tree.AddChild(clips[speed.Key], speed.Value);
        locomotion.speedParameterActive = true;
        locomotion.speedParameter = "AnimSpeed";
        sm.defaultState = locomotion;

        string[] fidgets = { "Attack", "Jump" };
        for (int i = 0; i < fidgets.Length; i++)
        {
            AnimatorState state = sm.AddState("Fidget_" + fidgets[i], new Vector3(500f, i * 100f));
            state.motion = clips[fidgets[i]];
            AnimatorStateTransition enter = locomotion.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.15f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Fidget");
            enter.AddCondition(AnimatorConditionMode.Equals, i, "FidgetIndex");
            AnimatorStateTransition back = state.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.25f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---------- UV ----------

    // Copie du maillage avec des UV par projection cubique : chaque sommet est projeté selon l'axe dominant de sa
    // normale (pose de liaison), ce qui colle la texture au maillage pendant l'animation.
    private static Mesh BoxProjectedCopy(Mesh source)
    {
        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_BoxUV";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i] / TileSize;
            Vector3 n = normals[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            uv[i] = ax >= ay && ax >= az ? new Vector2(p.z, p.y)
                  : ay >= az ? new Vector2(p.x, p.z)
                  : new Vector2(p.x, p.y);
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();

        // Mise à jour sur place d'un maillage déjà généré : il garde son identifiant (et les références vers lui).
        string path = $"{MeshDir}/{mesh.name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = mesh.name;
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ---------- Matériaux ----------

    private static Material BuildMetalMaterial()
    {
        BuildMetalTextures(out Texture2D albedo, out Texture2D mask, out Texture2D normal, out Texture2D emission);
        Material mat = LitMaterial("M_SpiderMetal", Color.white, 1f, 1f, Color.white * 4f);
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTexture("_MetallicGlossMap", mask);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_EmissionMap", emission);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Sablier rouge incandescent (non éclairé, en HDR : le bloom le fait rayonner), découpé par la transparence.
    private static Material BuildMarkMaterial()
    {
        const int size = 128;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
            // Deux triangles pointe contre pointe, reliés par une taille fine.
            float halfWidth = 0.12f + 0.8f * Mathf.Abs(v);
            float inside = Mathf.Min(halfWidth - Mathf.Abs(u), 0.9f - Mathf.Abs(v));
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f + inside * size * 0.25f));
        }
        Texture2D tex = SaveTexture("T_SpiderMark", pixels, true, false, size, TextureWrapMode.Clamp);

        string path = $"{MatDir}/M_SpiderMark.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", MarkRed);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LitMaterial(string name, Color baseColor, float metallic, float smoothness, Color emission)
    {
        string path = $"{MatDir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            // Avec « None », la validation d'URP à l'import retire le mot-clé d'émission.
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Textures ----------

    private enum PanelKind { Plain, Vent, Hazard, Light }

    private struct Panel
    {
        public RectInt Rect;
        public PanelKind Kind;
        public float Tint;
        public float Height;
    }

    // Plaques de blindage en métal sombre (répétables) : joints creux, bords biseautés usés, rivets, grilles
    // d'aération dont certaines éclairées en cyan, quelques plaques à rayures de danger.
    private static void BuildMetalTextures(out Texture2D albedoTex, out Texture2D maskTex, out Texture2D normalTex, out Texture2D emissionTex)
    {
        const int size = TexSize;
        List<Panel> panels = LayoutPanels(size);
        var height = new float[size * size];
        var albedo = new Color[size * size];
        var mask = new Color[size * size];
        var emission = new Color[size * size];

        // Acier clair : l'araignée doit se détacher du sol sombre de l'arène.
        Color dark = new Color(0.46f, 0.48f, 0.52f);
        Color light = new Color(0.66f, 0.68f, 0.72f);
        Color bare = new Color(0.82f, 0.83f, 0.86f);
        Color seamColor = new Color(0.025f, 0.025f, 0.03f);
        Color hazardYellow = new Color(0.78f, 0.56f, 0.08f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            Panel panel = panels.First(p => p.Rect.Contains(new Vector2Int(x, y)));
            RectInt r = panel.Rect;
            float edge = Mathf.Min(Mathf.Min(x - r.xMin, r.xMax - 1 - x), Mathf.Min(y - r.yMin, r.yMax - 1 - y));

            float grime = Fbm(u, v, 8, 4, 3);
            float scratches = Mathf.Pow(Fbm(u * 1f, v * 1f, 32, 3, 9), 6f);

            // Relief : joint creux, biseau, plaque surélevée.
            float bevel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 7f, edge));
            float h = bevel * (0.7f + panel.Height);
            bool seam = edge < 2f;
            bool worn = !seam && edge < 7f && Fbm(u, v, 16, 3, 5) > 0.52f;

            Color c = Color.Lerp(dark, light, panel.Tint) * Mathf.Lerp(0.8f, 1.15f, grime);
            // Métal peint : un métal pur paraît noir dans une arène sombre (rien à refléter).
            float metallic = 0.4f;
            float smooth = Mathf.Lerp(0.35f, 0.6f, grime);

            if (panel.Kind == PanelKind.Hazard && edge > 9f)
            {
                bool stripe = Mathf.Repeat((x + y) / 14f, 2f) < 1f;
                c = (stripe ? hazardYellow : new Color(0.05f, 0.05f, 0.05f)) * Mathf.Lerp(0.75f, 1.05f, grime);
                metallic = 0.25f;
                smooth = 0.4f;
            }

            bool slit = false;
            if ((panel.Kind == PanelKind.Vent || panel.Kind == PanelKind.Light) && edge > 12f)
            {
                slit = Mathf.Repeat(y - r.yMin, 9f) < 4f;
                if (slit)
                {
                    h -= 0.6f;
                    c = new Color(0.02f, 0.02f, 0.025f);
                    smooth = 0.2f;
                    if (panel.Kind == PanelKind.Light)
                        emission[i] = Glow;
                }
            }

            // Rivets aux coins des plaques.
            foreach (Vector2 corner in new[] { new Vector2(r.xMin + 8, r.yMin + 8), new Vector2(r.xMax - 9, r.yMin + 8), new Vector2(r.xMin + 8, r.yMax - 9), new Vector2(r.xMax - 9, r.yMax - 9) })
            {
                float d = Vector2.Distance(new Vector2(x, y), corner) / 3.5f;
                if (d < 1f && !slit)
                {
                    h += 0.5f * Mathf.Sqrt(1f - d * d);
                    c = Color.Lerp(c, bare * 0.8f, 0.5f);
                }
            }

            if (worn || scratches > 0.35f)
            {
                c = Color.Lerp(c, bare, worn ? 0.8f : 0.4f);
                metallic = 1f;
                smooth = 0.75f;
            }
            if (seam)
            {
                c = seamColor;
                metallic = 0.5f;
                smooth = 0.15f;
            }

            height[i] = h;
            albedo[i] = new Color(c.r, c.g, c.b, 1f);
            mask[i] = new Color(metallic, 0f, 0f, smooth);
            emission[i].a = 1f;
        }

        // Normales depuis le relief (bords qui bouclent : texture répétable).
        var normal = new Color[size * size];
        const float strength = 4f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = height[y * size + (x + 1) % size] - height[y * size + (x - 1 + size) % size];
            float dy = height[((y + 1) % size) * size + x] - height[((y - 1 + size) % size) * size + x];
            Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
            normal[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        albedoTex = SaveTexture("T_SpiderMetal_Albedo", albedo, true, false);
        maskTex = SaveTexture("T_SpiderMetal_MetalSmooth", mask, false, false);
        normalTex = SaveTexture("T_SpiderMetal_Normal", normal, false, true);
        emissionTex = SaveTexture("T_SpiderMetal_Emission", emission, true, false);
    }

    // Grille de plaques, certaines coupées en deux (motif fixe d'une génération à l'autre).
    private static List<Panel> LayoutPanels(int size)
    {
        var random = new System.Random(17);
        int cell = size / PanelGrid;
        var panels = new List<Panel>();
        for (int gy = 0; gy < PanelGrid; gy++)
        for (int gx = 0; gx < PanelGrid; gx++)
        {
            var rect = new RectInt(gx * cell, gy * cell, cell, cell);
            int split = random.Next(3);
            var rects = split == 0 ? new[] { rect }
                : split == 1 ? new[] { new RectInt(rect.x, rect.y, cell, cell / 2), new RectInt(rect.x, rect.y + cell / 2, cell, cell / 2) }
                : new[] { new RectInt(rect.x, rect.y, cell / 2, cell), new RectInt(rect.x + cell / 2, rect.y, cell / 2, cell) };
            foreach (RectInt r in rects)
            {
                double roll = random.NextDouble();
                panels.Add(new Panel
                {
                    Rect = r,
                    Kind = roll < 0.2 ? PanelKind.Light : roll < 0.32 ? PanelKind.Vent : roll < 0.4 ? PanelKind.Hazard : PanelKind.Plain,
                    Tint = (float)random.NextDouble(),
                    Height = (float)random.NextDouble() * 0.3f,
                });
            }
        }
        return panels;
    }

    private static Texture2D SaveTexture(string name, Color[] pixels, bool sRGB, bool normalMap, int size = TexSize, TextureWrapMode wrap = TextureWrapMode.Repeat)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.SetPixels(pixels);
        tex.Apply();
        string path = $"{TexDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRGB;
        importer.wrapMode = wrap;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Bruit répétable ----------

    // Somme d'octaves de bruit de valeur périodique (période = cellules sur [0, 1]), environ 0 -> 1.
    private static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0f, amplitude = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(u * period, v * period, period, seed + o * 31) * amplitude;
            norm += amplitude;
            period *= 2;
            amplitude *= 0.5f;
        }
        return sum / norm;
    }

    private static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float Corner(int cx, int cy) => Hash(((cx % period) + period) % period, ((cy % period) + period) % period, seed);
        return Mathf.Lerp(Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx), Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx), fy);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }
}
