using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Textures procédurales de tracés au trait : cercles de transmutation (inspirés de Fullmetal Alchemist),
// caractères façon « Matrix », anneaux holographiques et rayon de téléportation.
// Elles sont rangées avec les autres textures VFX et se chargent avec VfxLibrary.Tex(nom).
public static class SigilLibrary
{
    // Planche de caractères : GlyphTiles x GlyphTiles caractères.
    internal const int GlyphTiles = 8;

    // Rayons (normalisés, 1 = bord de la texture) des anneaux principaux de chaque cercle : les arcs électriques les suivent.
    internal static readonly float[] RingsA = { 0.97f, 0.78f, 0.39f };
    internal static readonly float[] RingsB = { 0.97f, 0.86f, 0.6f };
    internal static readonly float[] RingsC = { 0.97f, 0.8f, 0.42f };
    internal static readonly float[] RingsGate = { 0.97f, 0.76f, 0.54f };

    private static readonly string[] Names =
    {
        "T_Transmutation_A", "T_Transmutation_B", "T_Transmutation_C", "T_Transmutation_Gate",
        "T_Glyphs", "T_HoloRing", "T_HoloSigil", "T_BeamCore", "T_Pixel",
    };

    [MenuItem("Tools/Abilities/Rebuild Sigil Textures")]
    public static void BuildTextures()
    {
        Save("T_Transmutation_A", CircleA(1024));
        Save("T_Transmutation_B", CircleB(1024));
        Save("T_Transmutation_C", CircleC(1024));
        Save("T_Transmutation_Gate", CircleGate(512));
        Save("T_Glyphs", Glyphs(512));
        Save("T_HoloRing", HoloRing(512));
        Save("T_HoloSigil", HoloSigil(512));
        Save("T_BeamCore", BeamCore(64, 256));
        Save("T_Pixel", Pixel(32));
    }

    // Génère les textures seulement si l'une d'elles manque.
    public static void EnsureTextures()
    {
        VfxLibrary.EnsureTextures();
        foreach (string name in Names)
        {
            if (VfxLibrary.Tex(name) == null)
            {
                BuildTextures();
                return;
            }
        }
    }

    private static void Save(string name, Texture2D texture)
    {
        string path = $"{VfxLibrary.TexRoot}/{name}.png";
        FireballSetupBuilder.SaveTexture(path, texture, false);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    // ---------- Cercles de transmutation ----------

    // Cercle classique : triangle inscrit, cercles d'éléments aux sommets, carré et soleil au centre.
    private static Texture2D CircleA(int size)
    {
        var c = new Canvas(size);
        var rng = new System.Random(11);
        const float w = 0.007f;
        c.Circle(Vector2.zero, 0.97f, w * 1.3f);
        c.Circle(Vector2.zero, 0.92f, w);
        Script(c, rng, 0.795f, 0.905f, 44, w * 0.7f);
        c.Circle(Vector2.zero, 0.78f, w * 1.2f);

        Vector2[] tri = c.Polygon(Vector2.zero, 0.78f, 3, 90f, w);
        string[] elements = { "fire", "water", "earth" };
        for (int i = 0; i < 3; i++)
        {
            c.Disc(tri[i], 0.13f, 0f);
            c.Circle(tri[i], 0.13f, w);
            c.Circle(tri[i], 0.105f, w * 0.7f);
            Symbol(c, elements[i], tri[i], 0.065f, w * 0.8f);
        }

        c.Circle(Vector2.zero, 0.39f, w);
        c.Polygon(Vector2.zero, 0.39f, 4, 45f, w * 0.8f);
        c.Circle(Vector2.zero, 0.2f, w * 0.8f);
        Symbol(c, "sun", Vector2.zero, 0.1f, w * 0.8f);

        // Rayons du cercle intérieur vers le milieu des côtés, symboles dans les segments extérieurs.
        string[] outer = { "mercury", "sulfur", "salt" };
        for (int i = 0; i < 3; i++)
        {
            float angle = (-90f + i * 120f) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            c.Line(dir * 0.2f, dir * 0.39f, w * 0.7f);
            Symbol(c, outer[i], dir * 0.6f, 0.075f, w * 0.8f);
        }
        return c.ToTexture();
    }

    // Cercle « rituel » : octogramme de deux carrés, triangle inversé, croissant de lune au centre.
    private static Texture2D CircleB(int size)
    {
        var c = new Canvas(size);
        var rng = new System.Random(23);
        const float w = 0.007f;
        c.Circle(Vector2.zero, 0.97f, w * 1.3f);
        c.Circle(Vector2.zero, 0.94f, w);
        Script(c, rng, 0.87f, 0.93f, 60, w * 0.6f);
        c.Circle(Vector2.zero, 0.86f, w * 1.2f);

        Vector2[] a = c.Polygon(Vector2.zero, 0.86f, 4, 0f, w);
        Vector2[] b = c.Polygon(Vector2.zero, 0.86f, 4, 45f, w);
        foreach (Vector2 p in a)
        {
            c.Disc(p, 0.065f, 0f);
            c.Circle(p, 0.065f, w * 0.8f);
            c.Disc(p, 0.02f, 1f);
        }
        foreach (Vector2 p in b)
        {
            c.Disc(p, 0.065f, 0f);
            c.Circle(p, 0.065f, w * 0.8f);
            Symbol(c, "salt", p, 0.035f, w * 0.6f);
        }

        c.Circle(Vector2.zero, 0.6f, w);
        Script(c, rng, 0.615f, 0.68f, 36, w * 0.5f);
        c.Circle(Vector2.zero, 0.69f, w * 0.7f);
        Vector2[] tri = c.Polygon(Vector2.zero, 0.6f, 3, -90f, w);
        string[] corners = { "air", "fire", "water" };
        for (int i = 0; i < 3; i++)
            Symbol(c, corners[i], tri[i] * 0.62f, 0.05f, w * 0.7f);
        c.Circle(Vector2.zero, 0.3f, w);
        Symbol(c, "moon", Vector2.zero, 0.14f, w);
        return c.ToTexture();
    }

    // Cercle géométrique : graduations, heptagramme, carré intérieur, soufre au centre.
    private static Texture2D CircleC(int size)
    {
        var c = new Canvas(size);
        var rng = new System.Random(37);
        const float w = 0.007f;
        c.Circle(Vector2.zero, 0.97f, w * 1.3f);
        c.Circle(Vector2.zero, 0.9f, w);
        for (int i = 0; i < 72; i++)
        {
            float angle = i * 5f * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            c.Line(dir * 0.9f, dir * (i % 6 == 0 ? 0.97f : 0.935f), w * 0.6f);
        }
        c.Circle(Vector2.zero, 0.8f, w * 1.2f);

        Vector2[] hept = c.Polygon(Vector2.zero, 0.8f, 7, 90f, w * 0.8f);
        for (int i = 0; i < 7; i++)
            c.Line(hept[i], hept[(i + 3) % 7], w);
        string[] symbols = { "sun", "moon", "mercury", "sulfur", "salt", "air", "earth" };
        for (int i = 0; i < 7; i++)
        {
            c.Disc(hept[i], 0.075f, 0f);
            c.Circle(hept[i], 0.075f, w * 0.8f);
            Symbol(c, symbols[i], hept[i], 0.042f, w * 0.6f);
        }

        c.Circle(Vector2.zero, 0.42f, w);
        Script(c, rng, 0.43f, 0.5f, 28, w * 0.5f);
        c.Circle(Vector2.zero, 0.51f, w * 0.7f);
        c.Polygon(Vector2.zero, 0.42f, 4, 0f, w * 0.8f);
        c.Circle(Vector2.zero, 0.3f, w * 0.8f);
        Symbol(c, "sulfur", Vector2.zero, 0.13f, w);
        return c.ToTexture();
    }

    // Petit cercle des portes : carré, cercle, triangle et symbole de l'air.
    private static Texture2D CircleGate(int size)
    {
        var c = new Canvas(size);
        var rng = new System.Random(5);
        const float w = 0.012f;
        c.Circle(Vector2.zero, 0.97f, w * 1.2f);
        c.Circle(Vector2.zero, 0.9f, w * 0.8f);
        Script(c, rng, 0.78f, 0.88f, 22, w * 0.6f);
        c.Circle(Vector2.zero, 0.76f, w);
        c.Polygon(Vector2.zero, 0.76f, 4, 45f, w);
        c.Circle(Vector2.zero, 0.54f, w);
        c.Polygon(Vector2.zero, 0.54f, 3, 90f, w);
        c.Circle(Vector2.zero, 0.27f, w * 0.8f);
        Symbol(c, "air", Vector2.zero, 0.13f, w);
        return c.ToTexture();
    }

    // Écriture alchimique : petites lettres de 1 à 3 traits, orientées le long de la couronne.
    private static void Script(Canvas c, System.Random rng, float rIn, float rOut, int count, float width)
    {
        float h = (rOut - rIn) * 0.8f;
        float rMid = (rIn + rOut) * 0.5f;
        float letterWidth = 2f * Mathf.PI * rMid / count * 0.6f;
        for (int k = 0; k < count; k++)
        {
            float angle = k * Mathf.PI * 2f / count;
            var n = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var t = new Vector2(-n.y, n.x);
            Vector2 center = n * rMid;
            Vector2 P(int u, int v) => center + t * ((u - 1) * 0.5f * letterWidth) + n * ((v - 1) * 0.5f * h);

            int strokes = 1 + rng.Next(3);
            for (int s = 0; s < strokes; s++)
            {
                int u0 = rng.Next(3), v0 = rng.Next(3), u1 = rng.Next(3), v1 = rng.Next(3);
                if (u0 == u1 && v0 == v1)
                    v1 = (v1 + 2) % 3;
                c.Line(P(u0, v0), P(u1, v1), width);
            }
            if (rng.Next(6) == 0)
                c.Circle(P(1, 1), h * 0.22f, width);
        }
    }

    // Symboles alchimiques (éléments, planètes, principes) centrés en p, de rayon s.
    private static void Symbol(Canvas c, string kind, Vector2 p, float s, float width)
    {
        switch (kind)
        {
            case "fire":
                c.Polygon(p, s, 3, 90f, width);
                break;
            case "water":
                c.Polygon(p, s, 3, -90f, width);
                break;
            case "air":
                c.Polygon(p, s, 3, 90f, width);
                c.Line(p + new Vector2(-s * 0.75f, s * 0.15f), p + new Vector2(s * 0.75f, s * 0.15f), width);
                break;
            case "earth":
                c.Polygon(p, s, 3, -90f, width);
                c.Line(p + new Vector2(-s * 0.75f, -s * 0.15f), p + new Vector2(s * 0.75f, -s * 0.15f), width);
                break;
            case "sun":
                c.Circle(p, s, width);
                c.Disc(p, s * 0.22f, 1f);
                break;
            case "moon":
                c.Arc(p, s, 60f, 300f, width);
                c.Arc(p + new Vector2(-s * 0.45f, 0f), s * 0.78f, 70f, 290f, width);
                break;
            case "salt":
                c.Circle(p, s, width);
                c.Line(p + new Vector2(-s, 0f), p + new Vector2(s, 0f), width);
                break;
            case "sulfur":
                c.Polygon(p + new Vector2(0f, s * 0.35f), s * 0.65f, 3, 90f, width);
                c.Line(p + new Vector2(0f, s * 0.02f), p + new Vector2(0f, -s), width);
                c.Line(p + new Vector2(-s * 0.4f, -s * 0.5f), p + new Vector2(s * 0.4f, -s * 0.5f), width);
                break;
            case "mercury":
                c.Circle(p + new Vector2(0f, s * 0.15f), s * 0.38f, width);
                c.Arc(p + new Vector2(0f, s * 0.75f), s * 0.35f, 200f, 340f, width);
                c.Line(p + new Vector2(0f, -s * 0.23f), p + new Vector2(0f, -s), width);
                c.Line(p + new Vector2(-s * 0.3f, -s * 0.62f), p + new Vector2(s * 0.3f, -s * 0.62f), width);
                break;
        }
    }

    // ---------- Caractères « Matrix » ----------

    // Planche de caractères : chiffres puis pseudo-katakana (souvent en miroir), traits nets.
    private static Texture2D Glyphs(int size)
    {
        var c = new Canvas(size);
        var rng = new System.Random(1999);
        float cell = 2f / GlyphTiles;
        Vector2[][] strokes =
        {
            new[] { new Vector2(-0.6f, 0.8f), new Vector2(0.6f, 0.8f) },
            new[] { new Vector2(-0.6f, 0.15f), new Vector2(0.6f, 0.15f) },
            new[] { new Vector2(0f, 0.8f), new Vector2(0f, -0.85f) },
            new[] { new Vector2(0.6f, 0.8f), new Vector2(0.6f, -0.1f), new Vector2(-0.2f, -0.85f) },
            new[] { new Vector2(0.3f, 0.6f), new Vector2(-0.6f, -0.85f) },
            new[] { new Vector2(0f, 0.1f), new Vector2(0.6f, -0.85f) },
            new[] { new Vector2(-0.4f, 0.5f), new Vector2(-0.1f, 0.25f) },
            new[] { new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.4f) },
            new[] { new Vector2(-0.6f, -0.85f), new Vector2(0.6f, -0.85f) },
            new[] { new Vector2(-0.6f, 0.4f), new Vector2(0.6f, 0.4f), new Vector2(0.1f, -0.85f) },
            new[] { new Vector2(-0.5f, -0.2f), new Vector2(-0.1f, -0.6f) },
        };
        // Chiffres en segments : a b c d e f g (afficheur 7 segments).
        int[] digits = { 0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011, 0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011 };
        var segA = new[] { new Vector2(-0.45f, 0.8f), new Vector2(0.45f, 0.8f), new Vector2(0.45f, 0f), new Vector2(0.45f, -0.8f), new Vector2(-0.45f, -0.8f), new Vector2(-0.45f, 0f) };

        for (int k = 0; k < GlyphTiles * GlyphTiles; k++)
        {
            int col = k % GlyphTiles, row = k / GlyphTiles;
            var origin = new Vector2(-1f + (col + 0.5f) * cell, 1f - (row + 0.5f) * cell);
            float scale = cell * 0.5f * 0.78f;
            bool mirror = rng.Next(2) == 0;
            Vector2 G(Vector2 v) => origin + new Vector2(mirror ? -v.x : v.x, v.y) * scale;
            float width = 0.11f * scale;

            if (k < 10)
            {
                // a = haut, b = haut droite, c = bas droite, d = bas, e = bas gauche, f = haut gauche, g = milieu.
                int bits = digits[k];
                (int from, int to)[] segs = { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 0), (5, 2) };
                for (int s = 0; s < 7; s++)
                    if ((bits & (1 << (6 - s))) != 0)
                        c.Line(G(segA[segs[s].from]), G(segA[segs[s].to]), width, 0.15f);
                continue;
            }

            int count = 2 + rng.Next(3);
            var used = new HashSet<int>();
            while (used.Count < count)
                used.Add(rng.Next(strokes.Length));
            foreach (int s in used)
                for (int i = 0; i + 1 < strokes[s].Length; i++)
                    c.Line(G(strokes[s][i]), G(strokes[s][i + 1]), width, 0.15f);
            // Petits traits diacritiques, comme les ゛ des katakana.
            if (rng.Next(5) == 0)
            {
                c.Line(G(new Vector2(0.55f, 0.95f)), G(new Vector2(0.65f, 0.72f)), width * 0.8f, 0.15f);
                c.Line(G(new Vector2(0.8f, 0.95f)), G(new Vector2(0.9f, 0.72f)), width * 0.8f, 0.15f);
            }
        }
        return c.ToTexture(false);
    }

    // ---------- Holographique / téléportation ----------

    // Anneau de scan : cercle net et couronne de tirets.
    private static Texture2D HoloRing(int size)
    {
        var c = new Canvas(size);
        c.Circle(Vector2.zero, 0.86f, 0.025f, 0.4f);
        for (int i = 0; i < 16; i++)
        {
            float a0 = i * 22.5f + 3f;
            c.Arc(Vector2.zero, 0.95f, a0, a0 + 14f, 0.018f, 0.3f);
        }
        return c.ToTexture();
    }

    // Réticule au sol : arcs segmentés, graduations, hexagone et croix de visée.
    private static Texture2D HoloSigil(int size)
    {
        var c = new Canvas(size);
        const float w = 0.012f;
        c.Circle(Vector2.zero, 0.96f, w * 0.8f);
        for (int i = 0; i < 3; i++)
            c.Arc(Vector2.zero, 0.86f, i * 120f + 10f, i * 120f + 100f, w * 2f);
        for (int i = 0; i < 36; i++)
        {
            float angle = i * 10f * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            c.Line(dir * 0.7f, dir * (i % 3 == 0 ? 0.79f : 0.75f), w * 0.7f);
        }
        c.Polygon(Vector2.zero, 0.52f, 6, 0f, w);
        c.Circle(Vector2.zero, 0.3f, w);
        foreach (float a in new[] { 0f, 90f, 180f, 270f })
        {
            float r = a * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            c.Line(dir * 0.12f, dir * 0.24f, w);
        }
        return c.ToTexture();
    }

    // Coeur du rayon : profil gaussien en largeur (u), léger fondu aux deux extrémités (le sens du v dépend du rendu).
    private static Texture2D BeamCore(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width * 2f - 1f;
            float v = (y + 0.5f) / height;
            float across = Mathf.Exp(-u * u * 18f) + Mathf.Exp(-u * u * 4f) * 0.35f;
            float along = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.12f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.12f));
            float a = Mathf.Clamp01(across * along) * Mathf.Clamp01((1f - Mathf.Abs(u)) / 0.15f);
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Petit carré lumineux (pixels de numérisation).
    private static Texture2D Pixel(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
            float v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
            float d = Mathf.Max(u, v);
            float a = Mathf.Clamp01((0.62f - d) / 0.08f) + Mathf.Exp(-d * d * 6f) * 0.3f;
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * Mathf.Clamp01((1f - d) / 0.1f) * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // ---------- Tracé ----------

    // Toile de tracé en coordonnées normalisées (-1 -> 1, y vers le haut). Chaque trait n'écrit que dans sa boîte
    // englobante (rapide même en 1024) ; les traits se combinent par maximum, avec un léger halo.
    private sealed class Canvas
    {
        private readonly int size;
        private readonly float[] alpha;

        public Canvas(int size)
        {
            this.size = size;
            alpha = new float[size * size];
        }

        private Vector2 ToPixels(Vector2 p) => (p * 0.5f + Vector2.one * 0.5f) * size;

        public void Line(Vector2 a, Vector2 b, float width, float glow = 0.25f)
        {
            Vector2 pa = ToPixels(a), pb = ToPixels(b);
            float w = Mathf.Max(0.6f, width * size * 0.5f);
            float reach = w * 5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - reach));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + reach));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - reach));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + reach));
            Vector2 ab = pb - pa;
            float lengthSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / lengthSq);
                float d = Vector2.Distance(p, pa + ab * t);
                if (d > reach)
                    continue;
                float v = Mathf.Exp(-(d / w) * (d / w)) + glow * Mathf.Exp(-(d / reach) * (d / reach) * 4f);
                int i = y * size + x;
                if (v > alpha[i])
                    alpha[i] = v;
            }
        }

        public void Arc(Vector2 center, float radius, float fromDegrees, float toDegrees, float width, float glow = 0.25f)
        {
            int segments = Mathf.Max(8, Mathf.CeilToInt(radius * size * Mathf.Abs(toDegrees - fromDegrees) * Mathf.Deg2Rad / 6f));
            Vector2 previous = Point(center, radius, fromDegrees);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = Point(center, radius, Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments));
                Line(previous, next, width, glow);
                previous = next;
            }
        }

        public void Circle(Vector2 center, float radius, float width, float glow = 0.25f) => Arc(center, radius, 0f, 360f, width, glow);

        // Polygone régulier inscrit ; renvoie ses sommets.
        public Vector2[] Polygon(Vector2 center, float radius, int sides, float rotationDegrees, float width)
        {
            var vertices = new Vector2[sides];
            for (int i = 0; i < sides; i++)
                vertices[i] = Point(center, radius, rotationDegrees + i * 360f / sides);
            for (int i = 0; i < sides; i++)
                Line(vertices[i], vertices[(i + 1) % sides], width);
            return vertices;
        }

        // Disque plein de valeur value (0 efface ce qui est dessous : fond des médaillons).
        public void Disc(Vector2 center, float radius, float value)
        {
            Vector2 pc = ToPixels(center);
            float r = radius * size * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(pc.x - r - 1)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(pc.x + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pc.y - r - 1)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(pc.y + r + 1));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), pc);
                float inside = Mathf.Clamp01(r - d + 0.5f);
                int i = y * size + x;
                alpha[i] = Mathf.Lerp(alpha[i], value, inside);
            }
        }

        private static Vector2 Point(Vector2 center, float radius, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * radius;
        }

        // fadeEdge : fondu sur le bord du cercle inscrit (inutile pour une planche de caractères).
        public Texture2D ToTexture(bool fadeEdge = true)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha[y * size + x]);
                if (fadeEdge)
                {
                    float d = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                    a *= Mathf.Clamp01((1f - d) / 0.01f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
