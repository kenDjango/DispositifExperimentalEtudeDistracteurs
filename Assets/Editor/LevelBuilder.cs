using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Construit l'arène sci-fi à partir du « Modular SciFi MegaKit » : matériaux améliorés, sol, murs, couverts,
// éléments de décor saillants (portail, cuves à spécimens, gyrophares...) et éclairage.
public static class LevelBuilder
{
    private const string Kit = "Assets/Modular SciFi MegaKit[Standard]/Modular SciFi MegaKit[Standard]/";
    private const string KitFbx = Kit + "FBX (Unity)";
    private const string Root = "Assets/Level";
    private const string MatDir = Root + "/Materials";
    private const string TexDir = Root + "/Textures";

    // Arène intérieure : x dans [-HalfX, HalfX], z dans [-HalfZ, HalfZ], dalles de 4 m.
    // Profondeur choisie pour que la caméra (vue plongeante) garde le portail du mur nord dans le champ.
    private const float Tile = 4f;
    private const int TilesX = 10;
    private const int TilesZ = 5;
    private const float HalfX = TilesX * Tile * 0.5f;
    private const float HalfZ = TilesZ * Tile * 0.5f;

    private static readonly Color SpecimenGreen = new Color(0.25f, 1f, 0.45f);
    private static readonly Color PortalGreen = new Color(0.2f, 1f, 0.6f);
    private static readonly Color AlarmRed = new Color(1f, 0.12f, 0.05f);
    // Halo du portail volontairement modéré : les caractères verts du flux de données doivent ressortir dessus.
    private const float PortalGlowIntensity = 0.6f;
    private const float PortalRingIntensity = 1.4f;

    private static int environmentLayer;
    private static Transform level;

    [MenuItem("Tools/Level/Build Sci-Fi Arena")]
    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(TexDir);
        environmentLayer = EnsureLayer(EnvironmentCollision.LayerName);
        VfxLibrary.EnsureTextures();

        ExtractKitMaterials();
        UpgradeKitMaterials();

        GameObject existing = GameObject.Find("Level");
        if (existing != null)
            Object.DestroyImmediate(existing);
        level = new GameObject("Level").transform;

        BuildFloor();
        BuildWalls();
        BuildCover();
        BuildPortal(new Vector3(0f, 0f, HalfZ));
        BuildSpecimenTank(new Vector3(-7.5f, 0f, HalfZ - 3f), "Alien_Cyclop", 0.75f);
        BuildSpecimenTank(new Vector3(7.5f, 0f, HalfZ - 3f), "Alien_Oculichrysalis", 1.05f);
        BuildBeacon(new Vector3(-HalfX + 0.6f, 4.2f, HalfZ - 0.6f));
        BuildBeacon(new Vector3(HalfX - 0.6f, 4.2f, HalfZ - 0.6f));
        BuildDecor();
        BuildAtmosphere();
        SetupLighting();
        SetupGameplay();
        // Les sources de distraction viennent d'être recréées : on rebranche le système.
        if (Object.FindAnyObjectByType<DistractionDirector>(FindObjectsInactive.Include) != null)
            DistractionSetupBuilder.Install();

        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        EditorSceneManager.SaveScene(level.gameObject.scene);
        Debug.Log("Arène sci-fi construite.");
    }

    // ---------- Matériaux du kit ----------

    // Sort les matériaux intégrés aux FBX dans Assets/Level/Materials (modifiables) et les rebranche sur tous les modèles.
    private static void ExtractKitMaterials()
    {
        string[] models = AssetDatabase.FindAssets("t:Model", new[] { KitFbx }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var importers = new List<ModelImporter>();

        foreach (string path in models)
        {
            Material[] embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
            if (embedded.Length == 0)
                continue;

            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            foreach (Material m in embedded)
            {
                string dest = $"{MatDir}/{m.name}.mat";
                Material external = AssetDatabase.LoadAssetAtPath<Material>(dest);
                if (external == null)
                {
                    external = new Material(m);
                    AssetDatabase.CreateAsset(external, dest);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), external);
            }
            importers.Add(importer);
        }

        if (importers.Count == 0)
            return;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (ModelImporter importer in importers)
                importer.SaveAndReimport();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static void UpgradeKitMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            Texture baseMap = m.GetTexture("_BaseMap");
            string baseName = baseMap != null ? baseMap.name : "";

            string set = baseName.StartsWith("T_Trim_01") ? "Trim_01"
                       : baseName.StartsWith("T_Trim_02") ? "Trim_02"
                       : baseName == "T_Trim_03_BaseColor" ? "Trim_03"
                       : baseName == "T_PaddedWall_BaseColor" ? "PaddedWall"
                       : null;
            if (set != null)
                ApplyPbr(m, set);

            switch (m.name)
            {
                case "MI_Trim_01":
                case "MI_Trim_01_Floor":
                case "MI_Trim_01_Stairs":
                    SetEmission(m, KitTexture("T_Trim_01_Emissive", false), Color.white * 3f);
                    break;
                case "MI_PaddedWall":
                    SetEmission(m, KitTexture("T_PaddingWall_Emissive", false), Color.white * 3f);
                    break;
                case "M_Light":
                    SetEmission(m, null, new Color(0.85f, 0.95f, 1f) * 6f);
                    break;
                case "M_LightFade_Red":
                    SetEmission(m, null, AlarmRed * 4f);
                    break;
                case "M_Decal_Screen":
                    SetEmission(m, KitTexture("T_Decals", false), new Color(0.35f, 0.85f, 1f) * 2f);
                    break;
                case "MI_Decal_Line":
                    SetEmission(m, null, new Color(1f, 0.5f, 0.05f) * 1.5f);
                    break;
                case "MI_Decal_Red":
                    SetEmission(m, KitTexture("T_Decals", false), AlarmRed * 1.5f);
                    break;
                case "MI_Trim_03_Cables_Blue":
                    SetEmission(m, null, new Color(0.1f, 0.45f, 1f) * 3f);
                    break;
                case "M_Glass":
                    MakeGlass(m, new Color(0.55f, 0.75f, 0.8f, 0.25f));
                    break;
            }
            EditorUtility.SetDirty(m);
        }

        // Ennemis : prêts à recevoir leur lueur (couleur fixée par EnemyController).
        Material enemy = AssetDatabase.LoadAssetAtPath<Material>("Assets/Enemies/M_Enemy.mat");
        if (enemy != null)
            SetEmission(enemy, null, Color.black);

        AssetDatabase.SaveAssets();
    }

    private static void ApplyPbr(Material m, string set)
    {
        Texture2D normal = KitTexture($"T_{set}_Normal", true);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }

        Texture2D mask = MaskFromOrm(set);
        if (mask != null)
        {
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 0.9f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", mask);
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
        }
    }

    // URP attend le métal en R, l'occlusion en G et le lissé en A ; le kit fournit AO / rugosité / métal (ORM).
    private static Texture2D MaskFromOrm(string set)
    {
        string path = $"{TexDir}/T_{set}_Mask.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        string orm = $"{Kit}Textures/T_{set}_ORM.png";
        if (!File.Exists(orm))
            return null;

        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        source.LoadImage(File.ReadAllBytes(orm));
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            pixels[i] = new Color32(p.b, p.r, 0, (byte)(255 - p.g));
        }
        source.SetPixels32(pixels);
        File.WriteAllBytes(path, source.EncodeToPNG());
        Object.DestroyImmediate(source);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D KitTexture(string name, bool normalMap)
    {
        string path = $"{Kit}Textures/{name}.png";
        if (normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static void SetEmission(Material m, Texture emissionMap, Color color)
    {
        m.SetTexture("_EmissionMap", emissionMap);
        m.SetColor("_EmissionColor", color);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    internal static void MakeGlass(Material m, Color tint)
    {
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Smoothness", 0.95f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Material LitMaterial(string name, Color color, float smoothness, float metallic, Color emission)
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0f)
            SetEmission(m, null, emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Sol ----------

    private static void BuildFloor()
    {
        Transform floor = Group("Floor");
        for (int ix = 0; ix < TilesX; ix++)
        for (int iz = 0; iz < TilesZ; iz++)
        {
            float x = -HalfX + Tile * (ix + 0.5f);
            float z = -HalfZ + Tile * (iz + 0.5f);
            bool border = ix == 0 || ix == TilesX - 1 || iz == 0 || iz == TilesZ - 1;
            bool lane = ix == TilesX / 2 - 1 || ix == TilesX / 2;
            string piece = border ? "Platform_DarkPlates" : lane ? "Platform_CenterPlate" : "Platform_Metal";
            Place("Platforms/" + piece, new Vector3(x, 0f, z), 0f, floor, ColliderKind.None);
        }

        // Plateforme circulaire au centre : marque le coeur de l'arène.
        Place("Platforms/Platform_Round1", new Vector3(0f, 0.005f, 0f), 0f, floor, ColliderKind.None);
    }

    // ---------- Murs ----------

    private static void BuildWalls()
    {
        Transform walls = Group("Walls");

        // Nord : grands murs + bandeau de câbles au-dessus ; ouverture au centre pour le portail.
        string[] northPattern = { "WallAstra_Straight", "WallAstra_Straight_Window", "WallAstra_Straight", "WallAstra_Straight_Divided" };
        for (int ix = 0; ix < TilesX; ix++)
        {
            if (ix == TilesX / 2 - 1 || ix == TilesX / 2)
                continue;
            float x = -HalfX + Tile * (ix + 0.5f);
            var position = new Vector3(x, 0f, HalfZ - 2f);
            Place("Walls/" + northPattern[ix % northPattern.Length], position, -90f, walls, ColliderKind.Mesh);
            Place("Walls/TopCables_Straight", position, -90f, walls, ColliderKind.None);
        }

        // Est / ouest : murs à hublots et bandeaux, un panneau endommagé à l'est.
        for (int iz = 0; iz < TilesZ; iz++)
        {
            float z = -HalfZ + Tile * (iz + 0.5f);
            string east = iz == 2 ? "WallBand_Straight_Broken" : iz % 2 == 0 ? "WallWindow_Straight" : "WallBand_Straight";
            string west = iz % 2 == 1 ? "WallWindow_Straight" : "WallBand_Straight";
            Place("Walls/" + east, new Vector3(HalfX - 2f, 0f, z), 0f, walls, ColliderKind.Mesh);
            Place("Walls/" + west, new Vector3(-HalfX + 2f, 0f, z), 180f, walls, ColliderKind.Mesh);
            Place("Walls/TopSimple_Straight", new Vector3(HalfX - 2f, 0f, z), 0f, walls, ColliderKind.None);
            Place("Walls/TopSimple_Straight", new Vector3(-HalfX + 2f, 0f, z), 180f, walls, ColliderKind.None);
        }

        // Sud : simple rebord + garde-corps bas, pour ne pas masquer l'action vue du dessus. Au-delà : le vide.
        for (int ix = 0; ix < TilesX; ix++)
        {
            float x = -HalfX + Tile * (ix + 0.5f);
            Place("Walls/BottomMetal_Straight", new Vector3(x, 0f, -HalfZ + 2f), 90f, walls, ColliderKind.None);
            Place("Props/Prop_Rail_4", new Vector3(x - 2f, 0f, -HalfZ + 0.15f), 90f, walls, ColliderKind.None);
        }
        // Limite physique invisible le long du garde-corps.
        AddBoxBlocker(walls, "SouthEdge", new Vector3(0f, 0.5f, -HalfZ - 0.1f), new Vector3(HalfX * 2f, 1f, 0.4f));

        // Piliers d'angle pour masquer les raccords.
        foreach (float sx in new[] { -1f, 1f })
        {
            Place("Columns/Column_Large_Straight", new Vector3(sx * (HalfX - 0.3f), 0f, HalfZ - 0.3f), sx > 0 ? 180f : 90f, walls, ColliderKind.Box, new Vector3(1f, 0.5f, 1f));
            Place("Columns/Column_Large_Straight", new Vector3(sx * (HalfX - 0.3f), 0f, -HalfZ + 0.3f), sx > 0 ? 270f : 0f, walls, ColliderKind.Box, new Vector3(1f, 0.25f, 1f));
        }
    }

    // ---------- Couverts (servent à esquiver les tirs) ----------

    private static void BuildCover()
    {
        Transform cover = Group("Cover");

        // Quatre îlots de caisses, symétriques, laissant le centre libre.
        foreach (Vector3 c in new[] { new Vector3(-11f, 0f, -4.5f), new Vector3(11f, 0f, -4.5f), new Vector3(-12.5f, 0f, 3.5f), new Vector3(12.5f, 0f, 3.5f) })
        {
            float side = Mathf.Sign(c.x);
            Place("Props/Prop_Crate4", c + new Vector3(0f, 0.56f, 0f), 8f * side, cover, ColliderKind.Box);
            Place("Props/Prop_Crate3", c + new Vector3(-1.15f * side, 0.5f, 0.35f), -14f * side, cover, ColliderKind.Box);
            Place("Props/Prop_Crate3", c + new Vector3(0.05f, 1.62f, 0.05f), 27f * side, cover, ColliderKind.Box);
            Place("Props/Prop_Barrel_Large", c + new Vector3(0.9f * side, 0f, -1f), 40f, cover, ColliderKind.Box);
            Place("Props/Prop_Barrel_Large", c + new Vector3(1.35f * side, 0f, -0.45f), 0f, cover, ColliderKind.Box);

            // Les couverts doivent se repérer d'un coup d'oeil : éclairage froid au-dessus.
            Light light = VfxLibrary.PointLight("CoverLight", cover, new Color(0.75f, 0.85f, 1f), 6f, 4f);
            light.transform.position = c + new Vector3(0f, 3f, -1.2f);
        }

        // Piliers techniques dans la moitié nord (hauts : placés là où ils ne cachent pas le joueur).
        foreach (float sx in new[] { -1f, 1f })
            Place("Columns/Column_Pipes", new Vector3(sx * 4.2f, 0f, 4f), 0f, cover, ColliderKind.Box);

        // Coffres bas sur les flancs.
        Place("Props/Prop_Chest", new Vector3(-16.5f, 0f, -0.5f), 90f, cover, ColliderKind.Box);
        Place("Props/Prop_Chest", new Vector3(16.5f, 0f, 0.5f), -90f, cover, ColliderKind.Box);
    }

    // ---------- Portail (point focal au nord) ----------

    private static void BuildPortal(Vector3 basePosition)
    {
        Transform portal = Group("Portal");
        portal.position = basePosition;

        Place("Platforms/Door_Frame_A", basePosition + new Vector3(0f, 0f, -0.5f), 0f, portal, ColliderKind.Mesh);
        // Comble les côtés entre le cadre et les murs.
        foreach (float sx in new[] { -1f, 1f })
        {
            Place("Columns/Column_Astra", basePosition + new Vector3(sx * 3.2f, 0f, -0.4f), 90f * sx, portal, ColliderKind.Box);
            Place("Walls/TopCables_Straight", basePosition + new Vector3(sx * 3.2f, 0f, -2f), -90f, portal, ColliderKind.None);
        }
        AddBoxBlocker(portal, "PortalBlocker", basePosition + new Vector3(0f, 1.5f, 0f), new Vector3(Tile * 2f, 3f, 0.8f));

        Vector3 center = basePosition + new Vector3(0f, 2.4f, -0.45f);

        // Champ d'énergie : halo + anneau qui tourne, traversé par un flux de données.
        Material glow = PortalMaterial("M_PortalGlow", "T_Glow", PortalGreen * PortalGlowIntensity);
        Material ring = PortalMaterial("M_PortalRing", "T_Ring", new Color(0.7f, 1f, 0.85f) * PortalRingIntensity);
        Quad("PortalGlow", portal, glow, center, 6f, Vector3.zero);
        Quad("PortalRing", portal, ring, center + Vector3.back * 0.04f, 3.1f, new Vector3(0f, 0f, -60f));
        BuildDataStream(portal, center);

        // Particules aspirées en spirale vers le centre du portail.
        ParticleSystem motes = FireballSetupBuilder.CreateParticles("PortalMotes", portal,
            VfxLibrary.Material($"{MatDir}/M_PortalMote.mat", VfxLibrary.Tex("T_Glow"), 4f));
        motes.transform.position = center + Vector3.back * 0.1f;
        var main = motes.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 0.85f), PortalGreen);
        var emission = motes.emission;
        emission.rateOverTime = 40f;
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.2f;
        shape.radiusThickness = 0f;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalZ = 1.6f;
        velocity.radial = -1.1f;
        FireballSetupBuilder.SetColorOverLifetime(motes, Color.white, Color.white, 1f, 0f);

        Light light = VfxLibrary.PointLight("PortalLight", portal, PortalGreen, 14f, 9f);
        light.transform.position = center + Vector3.back * 1.5f;
        light.gameObject.AddComponent<LightFlicker>();
        SetFlicker(light, 0.12f, 3f);

        // Signalétique au sol devant le portail.
        Place("Decals/Decal_XSign", basePosition + new Vector3(0f, 0.02f, -3f), 180f, portal, ColliderKind.None, Vector3.one * 1.6f);
    }

    // Flux d'information façon « Matrix » : colonnes de caractères verts qui défilent dans le portail (une tête
    // brillante descend et laisse derrière elle une traînée de caractères qui s'éteignent), et caractères qui
    // s'échappent du portail en flottant vers l'arène.
    internal static void BuildDataStream(Transform portal, Vector3 center)
    {
        SigilLibrary.EnsureTextures();
        Material headMat = VfxLibrary.Material($"{MatDir}/M_DataHead.mat", VfxLibrary.Tex("T_Glyphs"), 5f);
        Material trailMat = VfxLibrary.Material($"{MatDir}/M_DataTrail.mat", VfxLibrary.Tex("T_Glyphs"), 2.5f);
        const float glyphSize = 0.2f;
        const float halfWidth = 1.8f;
        Color matrixGreen = new Color(0.2f, 1f, 0.35f);

        // Têtes de colonne : chacune descend dans une colonne fixe (émission en pas réguliers le long d'une ligne).
        ParticleSystem rain = FireballSetupBuilder.CreateParticles("DataRain", portal, headMat);
        rain.transform.position = center + new Vector3(0f, 1.9f, -0.1f);
        var main = rain.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
        main.startSpeed = 0f;
        main.startSize = glyphSize;
        main.startColor = new Color(0.85f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;
        var emission = rain.emission;
        emission.rateOverTime = 14f;
        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
        shape.radius = halfWidth;
        shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
        shape.radiusSpread = glyphSize / (halfWidth * 2f);
        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-4f, -2.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        GlyphSheet(rain, 8);
        VfxLibrary.Order(rain, 13);

        // Traînée : caractères immobiles semés tous les glyphSize par la tête, qui virent du blanc au vert puis s'éteignent.
        ParticleSystem trail = FireballSetupBuilder.CreateParticles("Trail", rain.transform, trailMat);
        main = trail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = 0f;
        main.startSize = glyphSize;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        emission = trail.emission;
        // Une tête descend à ~3,2 m/s : environ un caractère tous les glyphSize.
        emission.rateOverTime = 3.2f / glyphSize;
        var trailShape = trail.shape;
        trailShape.enabled = false;
        GlyphSheet(trail, 0);
        VfxLibrary.ColorOverLifetime(trail,
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(matrixGreen, 0.15f), new GradientColorKey(matrixGreen * 0.5f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Order(trail, 12);
        var sub = rain.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(trail, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);

        // Caractères qui s'échappent du portail et dérivent vers l'arène en scintillant.
        ParticleSystem flying = FireballSetupBuilder.CreateParticles("FlyingGlyphs", portal, trailMat);
        flying.transform.SetPositionAndRotation(center + Vector3.back * 0.2f, Quaternion.Euler(0f, 180f, 0f));
        main = flying.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, matrixGreen);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        emission = flying.emission;
        emission.rateOverTime = 12f;
        shape = flying.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.8f;
        shape.radiusThickness = 0.3f;
        velocity = flying.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        VfxLibrary.Drag(flying, 0.4f);
        VfxLibrary.Turbulence(flying, 0.5f, 0.4f, 0.3f);
        GlyphSheet(flying, 4);
        VfxLibrary.ColorOverLifetime(flying,
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(matrixGreen, 0.3f), new GradientColorKey(matrixGreen, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        VfxLibrary.Order(flying, 12);
    }

    // Caractère tiré au hasard dans la planche ; cycles > 0 : le caractère change ce nombre de fois au cours de sa vie.
    private static void GlyphSheet(ParticleSystem ps, int cycles)
    {
        int frames = SigilLibrary.GlyphTiles * SigilLibrary.GlyphTiles;
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = SigilLibrary.GlyphTiles;
        sheet.numTilesY = SigilLibrary.GlyphTiles;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, frames - 1);
        if (cycles > 0)
        {
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = cycles;
        }
        else
        {
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        }
    }

    private static Material PortalMaterial(string name, string texture, Color hdr)
    {
        Material m = VfxLibrary.Material($"{MatDir}/{name}.mat", VfxLibrary.Tex(texture), 1f);
        m.SetColor("_BaseColor", hdr);
        m.SetFloat("_Cull", 0f);
        return m;
    }

    // ---------- Cuves à spécimens ----------

    private static void BuildSpecimenTank(Vector3 position, string alien, float alienScale)
    {
        Transform tank = Group("SpecimenTank_" + alien);
        tank.position = position;

        Place("Platforms/Platform_Round1", position + Vector3.up * 0.02f, 0f, tank, ColliderKind.None, new Vector3(0.42f, 1.6f, 0.42f));
        GameObject cap = Place("Platforms/Platform_Round1", position + Vector3.up * 3.35f, 0f, tank, ColliderKind.None, new Vector3(0.42f, 2.2f, 0.42f));
        cap.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        Place("Columns/Column_Pipes", position + new Vector3(0f, 3.35f, 0f), 0f, tank, ColliderKind.None, new Vector3(0.9f, 0.25f, 0.9f));

        // Liquide lumineux + vitre.
        Material liquid = LitMaterial("M_TankLiquid", new Color(0.1f, 0.6f, 0.25f, 0.45f), 0.95f, 0f, SpecimenGreen * 1.6f);
        MakeGlass(liquid, new Color(0.15f, 0.8f, 0.35f, 0.4f));
        Material glass = LitMaterial("M_TankGlass", new Color(0.7f, 1f, 0.85f, 0.15f), 0.98f, 0f, Color.black);
        MakeGlass(glass, new Color(0.7f, 1f, 0.85f, 0.12f));
        Cylinder("Liquid", tank, liquid, position + Vector3.up * 1.7f, new Vector3(1.55f, 1.55f, 1.55f));
        Cylinder("Glass", tank, glass, position + Vector3.up * 1.7f, new Vector3(1.7f, 1.62f, 1.7f));

        GameObject specimen = Place("Aliens/" + alien, position + Vector3.up * 1.8f, 160f, tank, ColliderKind.None, Vector3.one * alienScale);
        specimen.AddComponent<Bobbing>();
        GameObjectUtility.SetStaticEditorFlags(specimen, 0);

        // Bulles qui montent dans le liquide.
        ParticleSystem bubbles = FireballSetupBuilder.CreateParticles("Bubbles", tank,
            VfxLibrary.Material($"{MatDir}/M_Bubble.mat", VfxLibrary.Tex("T_Spark"), 2.5f));
        bubbles.transform.position = position + Vector3.up * 0.3f;
        var main = bubbles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new Color(0.75f, 1f, 0.8f, 0.8f);
        var emission = bubbles.emission;
        emission.rateOverTime = 14f;
        var shape = bubbles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        VfxLibrary.Turbulence(bubbles, 0.2f, 1.5f, 0.5f);
        FireballSetupBuilder.SetColorOverLifetime(bubbles, Color.white, Color.white, 1f, 0f);

        Light light = VfxLibrary.PointLight("TankLight", tank, SpecimenGreen, 7f, 6f);
        light.transform.position = position + new Vector3(0f, 1.8f, -1.2f);
        light.gameObject.AddComponent<LightFlicker>();
        SetFlicker(light, 0.08f, 2f);

        var blocker = tank.gameObject.AddComponent<CapsuleCollider>();
        blocker.center = new Vector3(0f, 1.7f, 0f);
        blocker.radius = 1.1f;
        blocker.height = 3.6f;
        tank.gameObject.layer = environmentLayer;
    }

    // ---------- Gyrophares ----------

    private static void BuildBeacon(Vector3 position)
    {
        Transform beacon = Group("AlarmBeacon");
        beacon.position = position;

        Material red = LitMaterial("M_BeaconRed", AlarmRed, 0.8f, 0f, AlarmRed * 6f);
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bulb";
        Object.DestroyImmediate(bulb.GetComponent<Collider>());
        bulb.transform.SetParent(beacon, false);
        bulb.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
        bulb.GetComponent<MeshRenderer>().sharedMaterial = red;

        // Deux projecteurs dos à dos qui balaient la salle.
        var rotor = new GameObject("Rotor").transform;
        rotor.SetParent(beacon, false);
        rotor.gameObject.AddComponent<Spinner>();
        SetSpin(rotor.GetComponent<Spinner>(), new Vector3(0f, 200f, 0f));
        foreach (float yaw in new[] { 0f, 180f })
        {
            var spotGo = new GameObject("Spot");
            spotGo.transform.SetParent(rotor, false);
            spotGo.transform.localRotation = Quaternion.Euler(28f, yaw, 0f);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = AlarmRed;
            spot.intensity = 60f;
            spot.range = 18f;
            spot.spotAngle = 38f;
            spot.innerSpotAngle = 12f;
            spot.shadows = LightShadows.None;
        }

        Light glow = VfxLibrary.PointLight("Glow", beacon, AlarmRed, 3f, 3f);
        glow.transform.localPosition = Vector3.zero;
    }

    // ---------- Décor ----------

    private static void BuildDecor()
    {
        Transform decor = Group("Decor");

        // Logo géant au centre et bandes lumineuses orange le long de l'allée centrale.
        Place("Decals/Decal_Logo", new Vector3(0f, 0.16f, 0.2f), 180f, decor, ColliderKind.None, Vector3.one * 1.8f);
        foreach (float sx in new[] { -1f, 1f })
            for (int iz = 0; iz < TilesZ; iz++)
                Place("Decals/Decal_Line_Straight", new Vector3(sx * 4f, 0.02f, -HalfZ + Tile * (iz + 0.5f)), 0f, decor, ColliderKind.None);
        for (int ix = 0; ix < TilesX; ix++)
            Place("Decals/Decal_Dashes", new Vector3(-HalfX + Tile * (ix + 0.5f), 0.02f, -HalfZ + 1f), 0f, decor, ColliderKind.None);

        // Consoles le long du mur nord, écrans allumés.
        foreach (float x in new[] { -15f, -12.5f, 12.5f, 15f })
            Place("Props/Prop_Computer", new Vector3(x, 0f, HalfZ - 0.9f), 180f, decor, ColliderKind.Box);

        // Rampes lumineuses au sol le long des murs latéraux, avec leur lumière.
        foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { -5f, 5f })
            {
                Place("Props/Prop_Light_Floor", new Vector3(sx * (HalfX - 0.6f), 0f, z), 90f, decor, ColliderKind.None);
                Light l = VfxLibrary.PointLight("FloorLight", decor, new Color(0.8f, 0.9f, 1f), 5f, 2.2f);
                l.transform.position = new Vector3(sx * (HalfX - 1.2f), 0.5f, z);
            }

        // Câbles, bouches d'aération et terminaux.
        Place("Props/Prop_Cable_3", new Vector3(-14f, 0.01f, 7f), 70f, decor, ColliderKind.None);
        Place("Props/Prop_Cable_3", new Vector3(14f, 0.01f, -7f), -110f, decor, ColliderKind.None);
        Place("Props/Prop_Cable_1", new Vector3(10f, 0.01f, 7.5f), 20f, decor, ColliderKind.None);
        foreach (Vector3 p in new[] { new Vector3(-6f, 0.01f, -7f), new Vector3(6f, 0.01f, -7f), new Vector3(-16f, 0.01f, -7.5f), new Vector3(16f, 0.01f, 7f) })
            Place("Props/Prop_Vent_Big", p, 0f, decor, ColliderKind.None);
        Place("Props/Prop_AccessPoint", new Vector3(-HalfX + 0.5f, 0f, 0f), 0f, decor, ColliderKind.None);
        Place("Props/Prop_AccessPoint", new Vector3(HalfX - 0.5f, 0f, 6f), 180f, decor, ColliderKind.None);

        // Murs endommagés (est, puis ouest, en miroir) : gerbes d'étincelles à intervalles et lumière qui grésille.
        BuildBreach("", new Vector3(HalfX - 0.4f, 2.2f, -HalfZ + Tile * 2.5f), -1f, decor);
        BuildBreach("_2", new Vector3(-HalfX + 0.4f, 2.2f, -HalfZ + Tile * 2.5f), 1f, decor);
    }

    // Une brèche : étincelles projetées vers l'intérieur de l'arène (inward = +1 : vers +x, -1 : vers -x) et lumière.
    private static void BuildBreach(string suffix, Vector3 breach, float inward, Transform decor)
    {
        ParticleSystem sparks = FireballSetupBuilder.CreateParticles("BreachSparks" + suffix, decor,
            VfxLibrary.Material($"{MatDir}/M_BreachSpark.mat", VfxLibrary.Tex("T_Spark"), 7f));
        sparks.transform.SetPositionAndRotation(breach, Quaternion.Euler(0f, 90f * inward, 0f));
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f));
        main.gravityModifier = 1.5f;
        var emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(12f, 30f), 1, 1.7f) { cycleCount = 0 } });
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;
        VfxLibrary.Stretch(sparks, 0.04f, 1f);
        FireballSetupBuilder.SetColorOverLifetime(sparks, Color.white, new Color(1f, 0.4f, 0.1f), 1f, 0f);
        Light breachLight = VfxLibrary.PointLight("BreachLight" + suffix, decor, new Color(1f, 0.6f, 0.25f), 6f, 3f);
        breachLight.transform.position = breach + Vector3.right * (0.6f * inward);
        breachLight.gameObject.AddComponent<LightFlicker>();
        SetFlicker(breachLight, 0.9f, 25f);
    }

    // Poussière en suspension dans toute l'arène (profondeur, lumière visible).
    private static void BuildAtmosphere()
    {
        ParticleSystem dust = FireballSetupBuilder.CreateParticles("AmbientDust", Group("Atmosphere"),
            VfxLibrary.Material($"{MatDir}/M_Dust.mat", VfxLibrary.Tex("T_Glow"), 1.5f));
        dust.transform.position = new Vector3(0f, 2f, 0f);
        var main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new Color(0.8f, 0.9f, 1f, 0.35f);
        main.maxParticles = 400;
        main.prewarm = true;
        var emission = dust.emission;
        emission.rateOverTime = 45f;
        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(HalfX * 2f, 3f, HalfZ * 2f);
        VfxLibrary.Turbulence(dust, 0.15f, 0.3f, 0.1f);
        var col = dust.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;
    }

    // ---------- Éclairage ----------

    private static void SetupLighting()
    {
        // Ambiance sombre et froide : le relief vient des lumières colorées et de l'émissif.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.2f, 0.23f, 0.29f);
        RenderSettings.ambientEquatorColor = new Color(0.11f, 0.12f, 0.15f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 1f;

        Light sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.color = new Color(0.62f, 0.72f, 0.95f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
        }

        Camera cam = GameObject.Find("Camera").GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.012f, 0.016f);

        // Reflets des métaux : capture l'arène (et ses lumières) au lancement.
        var probeGo = new GameObject("ReflectionProbe");
        probeGo.transform.SetParent(level, false);
        probeGo.transform.position = new Vector3(0f, 3f, 0f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(HalfX * 2f + 4f, 10f, HalfZ * 2f + 4f);
        probe.boxProjection = true;
        probe.resolution = 256;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = Color.black;

        // Lumière douce qui suit le joueur : il reste lisible dans la pénombre.
        GameObject player = GameObject.Find("Capsule");
        Transform old = player.transform.Find("PlayerLight");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Light playerLight = VfxLibrary.PointLight("PlayerLight", player.transform, new Color(1f, 0.92f, 0.8f), 5f, 4f);
        playerLight.transform.localPosition = new Vector3(0f, 1.6f, -0.6f);
    }

    // ---------- Gameplay ----------

    private static void SetupGameplay()
    {
        // Le sol d'origine de la scène (Square) est masqué : les dalles de l'arène en tiennent lieu.
        GameObject oldGround = GameObject.Find("Square");
        if (oldGround != null)
            oldGround.SetActive(false);

        // Zone de déplacement = intérieur de l'arène ; la caméra suit avec une petite marge.
        SetBounds("PlayerBounds", Vector3.zero, new Vector2(HalfX * 2f - 1f, HalfZ * 2f - 1f));
        SetBounds("CameraBounds", new Vector3(0f, 0f, -1f), new Vector2(8f, 4f));

        EnemyManager manager = Object.FindAnyObjectByType<EnemyManager>();
        GameObject playerBounds = GameObject.Find("PlayerBounds");
        if (manager != null && playerBounds != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("spawnArea").objectReferenceValue = playerBounds.GetComponent<MovementBounds>();
            so.ApplyModifiedProperties();
        }

        // Le joueur démarre au centre, sur le logo.
        GameObject player = GameObject.Find("Capsule");
        player.transform.position = new Vector3(0f, player.transform.position.y, -1.5f);
    }

    private static void SetBounds(string name, Vector3 position, Vector2 size)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            return;
        go.transform.position = position;
        var so = new SerializedObject(go.GetComponent<MovementBounds>());
        so.FindProperty("size").vector2Value = size;
        so.ApplyModifiedProperties();
    }

    // ---------- Utilitaires ----------

    private enum ColliderKind { None, Box, Mesh }

    private static Transform Group(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(level, false);
        return go.transform;
    }

    private static GameObject Place(string piece, Vector3 position, float yaw, Transform parent, ColliderKind collider, Vector3? scale = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFbx}/{piece}.fbx");
        if (prefab == null)
        {
            Debug.LogWarning("Pièce introuvable : " + piece);
            return new GameObject(piece + " (manquant)");
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (scale.HasValue)
            go.transform.localScale = scale.Value;

        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        foreach (Transform child in go.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, GameObjectUtility.GetStaticEditorFlags(go));

        if (collider != ColliderKind.None)
        {
            SetLayer(go, environmentLayer);
            if (collider == ColliderKind.Mesh)
            {
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            else
            {
                Bounds bounds = LocalBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }
        return go;
    }

    // Boîte englobante des maillages dans l'espace local de la racine.
    internal static Bounds LocalBounds(GameObject go)
    {
        Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
        bool first = true;
        var bounds = new Bounds();
        foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
        {
            Bounds mb = filter.sharedMesh.bounds;
            Matrix4x4 m = toLocal * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (first)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }
        return bounds;
    }

    private static void AddBoxBlocker(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.layer = environmentLayer;
        go.AddComponent<BoxCollider>().size = size;
    }

    internal static void Quad(string name, Transform parent, Material material, Vector3 position, float size, Vector3 spin)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (spin != Vector3.zero)
            SetSpin(go.AddComponent<Spinner>(), spin);
    }

    private static void Cylinder(string name, Transform parent, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    internal static void SetSpin(Spinner spinner, Vector3 degreesPerSecond)
    {
        var so = new SerializedObject(spinner);
        so.FindProperty("degreesPerSecond").vector3Value = degreesPerSecond;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetFlicker(Light light, float amount, float speed)
    {
        var so = new SerializedObject(light.GetComponent<LightFlicker>());
        so.FindProperty("amount").floatValue = amount;
        so.FindProperty("speed").floatValue = speed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetLayer(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    // Crée le layer s'il n'existe pas (premier emplacement libre à partir de 6).
    internal static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0)
            return existing;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 6; i < 32; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        throw new System.InvalidOperationException("Aucun layer libre pour " + name);
    }
}
