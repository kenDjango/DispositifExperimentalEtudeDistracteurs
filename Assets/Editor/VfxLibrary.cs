using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Textures procédurales haute résolution (flipbooks de feu/fumée, étincelles, flares...) et réglages
// de particules partagés par les VFX des compétences.
public static class VfxLibrary
{
    internal const string TexRoot = FireballSetupBuilder.ArtRoot + "/Vfx";
    // Les flipbooks sont des planches de FlipbookTiles x FlipbookTiles images.
    internal const int FlipbookTiles = 8;

    // ---------- Textures ----------

    // Regénère les textures puis tous les effets des compétences.
    [MenuItem("Tools/Abilities/Rebuild All VFX")]
    public static void RebuildAll()
    {
        BuildTextures();
        FireballSetupBuilder.BuildVfxPrefabs();
        LaserSetupBuilder.BuildVfxPrefab();
        DashSetupBuilder.BuildVfxPrefab();
    }

    // Génère les textures seulement si l'une d'elles manque.
    public static void EnsureTextures()
    {
        foreach (string name in new[] { "T_FireSheet", "T_SmokeSheet", "T_Glow", "T_Spark", "T_Flare", "T_Ring", "T_Scorch", "T_Band", "T_BoltBand", "T_BeamNoise", "T_TrailStreak", "T_Runes" })
        {
            if (Tex(name) == null)
            {
                BuildTextures();
                return;
            }
        }
    }

    // Génère toutes les textures VFX.
    public static void BuildTextures()
    {
        Directory.CreateDirectory(TexRoot);
        Save("T_FireSheet", MakeFlipbook(128, FireDensity, false), false);
        Save("T_SmokeSheet", MakeFlipbook(128, SmokeDensity, true), false);
        Save("T_Glow", MakeRadial(256, d => Mathf.Exp(-d * d * 10f) * 0.8f + Mathf.Exp(-d * 4.5f) * 0.3f), false);
        Save("T_Spark", MakeRadial(128, d => Mathf.Exp(-d * d * 45f) + Mathf.Exp(-d * d * 7f) * 0.35f), false);
        Save("T_Flare", MakeFlare(256), false);
        Save("T_Ring", MakeRing(256), false);
        Save("T_Scorch", MakeScorch(256), false);
        Save("T_Band", MakeBand(32, 128, v => Mathf.Exp(-v * v * 5f) * 0.85f + Mathf.Exp(-v * v * 40f) * 0.15f), false);
        Save("T_BoltBand", MakeBand(32, 128, v => Mathf.Exp(-v * v * 70f) + Mathf.Exp(-v * v * 6f) * 0.3f), false);
        Save("T_BeamNoise", MakeTiledBand(256, 64, BeamNoise), true);
        Save("T_TrailStreak", MakeTiledBand(256, 64, TrailStreak), true);
        Save("T_Runes", MakeRuneCircle(512), false);
    }

    public static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexRoot}/{name}.png");

    private static void Save(string name, Texture2D texture, bool repeatU)
    {
        string path = $"{TexRoot}/{name}.png";
        FireballSetupBuilder.SaveTexture(path, texture, false);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (repeatU)
            importer.wrapModeU = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    // Planche d'animation : chaque image est la densité à l'instant t (0 -> 1) de la vie de la particule.
    private static Texture2D MakeFlipbook(int frameSize, System.Func<float, float, float, float> density, bool shaded)
    {
        int size = frameSize * FlipbookTiles;
        var pixels = new Color32[size * size];
        var frame = new float[frameSize * frameSize];
        int frames = FlipbookTiles * FlipbookTiles;

        for (int f = 0; f < frames; f++)
        {
            float t = f / (float)(frames - 1);
            for (int y = 0; y < frameSize; y++)
            for (int x = 0; x < frameSize; x++)
            {
                float u = (x + 0.5f) / frameSize * 2f - 1f;
                float v = (y + 0.5f) / frameSize * 2f - 1f;
                // Bord de l'image à zéro pour éviter les coupures nettes.
                float border = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) / 0.18f));
                frame[y * frameSize + x] = Mathf.Clamp01(density(u, v, t)) * border;
            }

            // Unity lit les planches de haut en bas : l'image 0 est en haut à gauche.
            int ox = (f % FlipbookTiles) * frameSize;
            int oy = (FlipbookTiles - 1 - f / FlipbookTiles) * frameSize;
            for (int y = 0; y < frameSize; y++)
            for (int x = 0; x < frameSize; x++)
            {
                float d = frame[y * frameSize + x];
                float lum;
                if (shaded)
                {
                    // Éclairage simulé venant du haut : relief tiré du gradient de densité, mesuré sur
                    // quelques pixels pour un modelé doux plutôt qu'un effet de gaufrage.
                    const int step = 4;
                    float dx = frame[y * frameSize + Mathf.Min(x + step, frameSize - 1)] - frame[y * frameSize + Mathf.Max(x - step, 0)];
                    float dy = frame[Mathf.Min(y + step, frameSize - 1) * frameSize + x] - frame[Mathf.Max(y - step, 0) * frameSize + x];
                    var normal = new Vector3(-dx * 1.5f, -dy * 1.5f, 1f).normalized;
                    float light = Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-0.3f, 0.7f, 0.65f).normalized));
                    lum = Mathf.Lerp(0.45f, 1f, light) * Mathf.Lerp(1f, 0.85f, d);
                }
                else
                {
                    // Feu : plus chaud (plus clair) là où il est dense.
                    lum = Mathf.Lerp(0.35f, 1f, Mathf.Pow(d, 0.6f));
                }

                byte l = (byte)(Mathf.Clamp01(lum) * 255f);
                byte a = (byte)(Mathf.Pow(d, 1.3f) * 255f);
                pixels[(oy + y) * size + ox + x] = new Color32(l, l, l, a);
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Boule de flamme turbulente qui monte, s'effiloche et se dissipe.
    private static float FireDensity(float u, float v, float t)
    {
        float z = t * 1.5f;
        // Distorsion qui monte : donne les langues de flamme.
        float qx = u + Noise.Fbm(u * 1.3f, v * 1.3f - t * 2f, z, 3) * 0.3f;
        float qy = v + Noise.Fbm(u * 1.3f + 5.2f, v * 1.3f - t * 2f + 1.3f, z, 3) * 0.3f;
        float n = Mathf.Clamp01(Noise.Fbm(qx * 2.4f, qy * 2.4f - t * 3f, z + 7f, 5) * 0.5f + 0.5f);
        float tongues = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise.Fbm(qx * 1.8f, qy * 1.2f - t * 2.5f, z + 3f, 4))), 4f);
        // Volume gaussien étiré vers le haut, qui s'élève et se rétracte en se dissipant.
        float r = new Vector2(qx, (qy - 0.15f * t) * 0.85f).magnitude / (0.62f + 0.1f * t);
        float body = Mathf.Exp(-r * r * 1.6f);
        float d = body * (0.2f + 1.1f * n + 0.5f * tongues);
        return d * (1.15f - 0.6f * t) - t * 0.25f;
    }

    // Volute de fumée cotonneuse qui gonfle et s'éclaircit.
    private static float SmokeDensity(float u, float v, float t)
    {
        float z = t * 0.8f + 30f;
        float qx = u + Noise.Fbm(u * 1.1f, v * 1.1f, z, 3) * 0.22f;
        float qy = v + Noise.Fbm(u * 1.1f + 3.1f, v * 1.1f + 8.7f, z, 3) * 0.22f;
        // Grosses boules + détail fin : aspect cumulus sans plateau plat.
        float puffs = Mathf.Clamp01(Noise.Fbm(qx * 1.8f, qy * 1.8f, z + 4f, 3) * 0.5f + 0.5f);
        float detail = Mathf.Clamp01(Noise.Fbm(qx * 4.5f, qy * 4.5f, z + 9f, 4) * 0.5f + 0.5f);
        float r = new Vector2(qx, qy).magnitude / (0.62f + 0.15f * t);
        float body = Mathf.Exp(-r * r * 1.8f);
        float d = body * (0.35f + 1f * puffs + 0.4f * detail);
        return d * (1.5f - 0.6f * t) - 0.1f;
    }

    private static Texture2D MakeRadial(int size, System.Func<float, float> profile)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / (size * 0.5f);
            float a = Mathf.Clamp01(profile(d)) * Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Éclat lumineux : halo + rayons fins (flash d'impact, sortie du laser).
    private static Texture2D MakeFlare(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) / size) * 2f - Vector2.one;
            float d = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            float glow = Mathf.Exp(-d * d * 18f) + Mathf.Exp(-d * 5f) * 0.25f;
            float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 80f) * Mathf.Exp(-d * 3.2f)
                       + Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f + 0.4f)), 160f) * Mathf.Exp(-d * 2.2f) * 0.7f;
            float a = Mathf.Clamp01(glow + rays) * Mathf.Clamp01((1f - d) / 0.1f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Onde de choc au sol : anneau irrégulier au bord net, intérieur légèrement voilé.
    private static Texture2D MakeRing(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) / size) * 2f - Vector2.one;
            float d = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            float n = Noise.Fbm(Mathf.Cos(angle) * 2f, Mathf.Sin(angle) * 2f, d * 3f, 4) * 0.5f + 0.5f;
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.07f, 2f));
            float inner = Mathf.SmoothStep(0f, 1f, d / 0.78f) * 0.25f * (d < 0.78f ? 1f : 0f);
            float a = Mathf.Clamp01((ring + inner) * Mathf.Lerp(0.45f, 1.2f, n)) * Mathf.Clamp01((1f - d) / 0.08f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Trace de brûlure : tache sombre irrégulière avec projections.
    private static Texture2D MakeScorch(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) / size) * 2f - Vector2.one;
            float d = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            float n = Noise.Fbm(p.x * 3f, p.y * 3f, 12f, 5);
            float splatter = Mathf.Pow(Mathf.Clamp01(Noise.Fbm(Mathf.Cos(angle) * 4f, Mathf.Sin(angle) * 4f, 3f, 3) * 0.5f + 0.5f), 3f);
            float shape = 1f - d / (0.55f + splatter * 0.4f) + n * 0.35f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shape * 2f)) * 0.9f * Mathf.Clamp01((1f - d) / 0.1f);
            Color c = Color.Lerp(new Color(0.16f, 0.1f, 0.07f), new Color(0.03f, 0.02f, 0.015f), Mathf.Clamp01(shape * 1.5f));
            pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Cercle magique : anneaux concentriques, graduations, glyphes et hexagramme (traits fins + léger halo).
    private static Texture2D MakeRuneCircle(int size)
    {
        var segments = new List<(Vector2 a, Vector2 b)>();
        Vector2 Polar(float r, float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

        // Graduations entre les deux anneaux extérieurs.
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48f;
            segments.Add((Polar(0.9f, angle), Polar(i % 4 == 0 ? 0.96f : 0.93f, angle)));
        }

        // Glyphes : petits traits pseudo-aléatoires dans chaque secteur de la couronne.
        var random = new System.Random(7);
        const int sectors = 16;
        for (int s = 0; s < sectors; s++)
        {
            float a0 = (s + 0.15f) * Mathf.PI * 2f / sectors;
            float a1 = (s + 0.85f) * Mathf.PI * 2f / sectors;
            int strokes = 2 + random.Next(3);
            for (int k = 0; k < strokes; k++)
            {
                float angleA = Mathf.Lerp(a0, a1, (float)random.NextDouble());
                float angleB = Mathf.Lerp(a0, a1, (float)random.NextDouble());
                float rA = Mathf.Lerp(0.67f, 0.84f, (float)random.NextDouble());
                float rB = Mathf.Lerp(0.67f, 0.84f, (float)random.NextDouble());
                segments.Add((Polar(rA, angleA), Polar(rB, angleB)));
            }
        }

        // Hexagramme inscrit dans l'anneau intérieur.
        for (int tri = 0; tri < 2; tri++)
        for (int i = 0; i < 3; i++)
        {
            float angleA = Mathf.PI * 0.5f + tri * Mathf.PI / 3f + i * Mathf.PI * 2f / 3f;
            float angleB = angleA + Mathf.PI * 2f / 3f;
            segments.Add((Polar(0.58f, angleA), Polar(0.58f, angleB)));
        }

        float[] rings = { 0.97f, 0.9f, 0.62f, 0.58f };
        const float width = 0.008f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) / size) * 2f - Vector2.one;
            float d = p.magnitude;
            float dist = float.MaxValue;
            foreach (float r in rings)
                dist = Mathf.Min(dist, Mathf.Abs(d - r));
            foreach (var (from, to) in segments)
                dist = Mathf.Min(dist, DistanceToSegment(p, from, to));

            float line = Mathf.Exp(-Mathf.Pow(dist / width, 2f));
            float halo = Mathf.Exp(-Mathf.Pow(dist / (width * 5f), 2f)) * 0.3f;
            float alpha = Mathf.Clamp01(line + halo) * Mathf.Clamp01((1f - d) / 0.02f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // Bande pour LineRenderer / TrailRenderer : u le long de la ligne, v dans la largeur (-1 -> 1).
    private static Texture2D MakeBand(int width, int height, System.Func<float, float> profile)
    {
        return MakeTiledBand(width, height, (u, v) => profile(v));
    }

    private static Texture2D MakeTiledBand(int width, int height, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width;
            float v = (y + 0.5f) / height * 2f - 1f;
            float a = Mathf.Clamp01(alpha(u, v)) * Mathf.Clamp01((1f - Mathf.Abs(v)) / 0.1f);
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Filaments électriques qui serpentent le long du rayon (répétable en u).
    private static float BeamNoise(float u, float v)
    {
        float angle = u * Mathf.PI * 2f;
        float total = 0f;
        for (int i = 0; i < 3; i++)
        {
            float n = Noise.Fbm(Mathf.Cos(angle) * 0.7f, Mathf.Sin(angle) * 0.7f, i * 11.3f, 3);
            float filament = Mathf.Exp(-Mathf.Pow((v - n * 0.6f) / 0.06f, 2f));
            total += filament * (0.6f + 0.4f * i / 2f);
        }
        float profile = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.2f);
        return total * profile + Mathf.Exp(-v * v * 8f) * 0.15f;
    }

    // Traînées de vitesse : fines stries dans la longueur (répétable en u).
    private static float TrailStreak(float u, float v)
    {
        float angle = u * Mathf.PI * 2f;
        float n = Mathf.Clamp01(Noise.Fbm(Mathf.Cos(angle) * 0.35f, Mathf.Sin(angle) * 0.35f, v * 16f, 3) * 0.5f + 0.5f);
        float profile = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.3f);
        return profile * Mathf.Clamp01(0.2f + Mathf.Pow(n, 3f) * 2.5f);
    }

    // ---------- Matériaux ----------

    // Matériau de particules dont la couleur de base est multipliée en HDR (les couleurs de particules sont limitées à 1).
    public static Material Material(string path, Texture2D texture, float intensity, bool flipbookBlending = false)
    {
        Material mat = FireballSetupBuilder.SaveParticleMaterial(path, texture, false);
        mat.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
        mat.SetFloat("_FlipbookBlending", flipbookBlending ? 1f : 0f);
        if (flipbookBlending)
            mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
        else
            mat.DisableKeyword("_FLIPBOOKBLENDING_ON");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Particules ----------

    // Joue la planche une fois sur la vie de la particule, avec fondu entre les images.
    public static void Flipbook(ParticleSystem ps)
    {
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = FlipbookTiles;
        sheet.numTilesY = FlipbookTiles;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = 1;

        ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend,
        });
    }

    public static void Turbulence(ParticleSystem ps, float strength, float frequency, float scrollSpeed)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = scrollSpeed;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.High;
    }

    public static void ColorOverLifetime(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        col.color = gradient;
    }

    public static void SizeOverLifetime(ParticleSystem ps, AnimationCurve curve)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    public static void Spin(ParticleSystem ps, float degreesPerSecond)
    {
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-degreesPerSecond * Mathf.Deg2Rad, degreesPerSecond * Mathf.Deg2Rad);
    }

    // Freine les particules (explosion qui ralentit en gonflant).
    public static void Drag(ParticleSystem ps, float drag)
    {
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = drag;
        limit.multiplyDragByParticleSize = false;
        limit.multiplyDragByParticleVelocity = true;
    }

    public static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = velocityScale;
        renderer.lengthScale = lengthScale;
    }

    public static void Order(ParticleSystem ps, int sortingOrder)
    {
        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
    }

    public static Light PointLight(string name, Transform parent, Color color, float range, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    // ---------- Bruit ----------

    // Bruit de gradient 3D (Perlin amélioré), déterministe.
    private static class Noise
    {
        private static readonly int[] Perm = BuildPermutation();

        private static int[] BuildPermutation()
        {
            var random = new System.Random(1337);
            var p = new int[256];
            for (int i = 0; i < 256; i++)
                p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            var perm = new int[512];
            for (int i = 0; i < 512; i++)
                perm[i] = p[i & 255];
            return perm;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        // Environ -1 -> 1.
        public static float Sample(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            x -= xi; y -= yi; z -= zi;
            xi &= 255; yi &= 255; zi &= 255;
            float u = Fade(x), v = Fade(y), w = Fade(z);

            int a = Perm[xi] + yi, aa = Perm[a] + zi, ab = Perm[a + 1] + zi;
            int b = Perm[xi + 1] + yi, ba = Perm[b] + zi, bb = Perm[b + 1] + zi;

            return Mathf.Lerp(
                Mathf.Lerp(Mathf.Lerp(Grad(Perm[aa], x, y, z), Grad(Perm[ba], x - 1, y, z), u),
                           Mathf.Lerp(Grad(Perm[ab], x, y - 1, z), Grad(Perm[bb], x - 1, y - 1, z), u), v),
                Mathf.Lerp(Mathf.Lerp(Grad(Perm[aa + 1], x, y, z - 1), Grad(Perm[ba + 1], x - 1, y, z - 1), u),
                           Mathf.Lerp(Grad(Perm[ab + 1], x, y - 1, z - 1), Grad(Perm[bb + 1], x - 1, y - 1, z - 1), u), v),
                w);
        }

        // Somme d'octaves, environ -1 -> 1.
        public static float Fbm(float x, float y, float z, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample(x, y, z) * amplitude;
                norm += amplitude;
                x *= 2.03f; y *= 2.03f; z *= 2.03f;
                amplitude *= 0.5f;
            }
            return sum / norm * 1.6f;
        }
    }
}
