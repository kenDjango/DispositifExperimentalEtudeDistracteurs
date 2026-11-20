using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Génère la scène d'accueil (commandes, sorts, recharge du dash) affichée au lancement du jeu, avant la partie.
// À lancer avec la scène de jeu ouverte : les textes reprennent ses réglages (touches, cooldowns, points, butin).
public static class TitleScreenBuilder
{
    private const string ScenePath = "Assets/Scenes/TitleScreen.unity";
    private const string ArtRoot = FireballSetupBuilder.ArtRoot;

    private static readonly Color Gold = new Color(0.78f, 0.67f, 0.43f);
    private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 1f);
    private static readonly Color BodyColor = new Color(0.85f, 0.86f, 0.9f);
    private const string Highlight = "#FFD27A";

    // Réglages lus dans la scène de jeu.
    private struct GameInfo
    {
        public string GameScene;
        public AbilityCaster Caster;
        public float Duration;
        public ScoreRules Rules;
        public Color FireballColor;
        public Color LaserColor;
        public float DropChance;
        public float RechargeLifetime;
    }

    [MenuItem("Tools/Game Loop/Build Title Screen")]
    public static void Build()
    {
        GameInfo info = ReadGame();
        Scene game = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(game);

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/T_UIWhite.png");
        Sprite background = SaveSprite("T_TitleBackground", MakeBackground(256));
        Sprite mouse = SaveSprite("T_IconMouse", MakeMouseIcon(128));
        Sprite orb = SaveSprite("T_IconEnergyCell", MakeOrbIcon(128));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var cameraGo = new GameObject("Camera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            BuildCanvas(info, white, background, mouse, orb, font);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            SceneManager.SetActiveScene(game);
            EditorSceneManager.CloseScene(scene, true);
        }

        // L'accueil vient en tête du build, juste après la présentation de l'étude si elle existe ; la partie suit.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        int intro = scenes.FindIndex(s => s.path == "Assets/Scenes/StudyIntro.unity");
        scenes.Insert(intro + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Écran d'accueil généré : " + ScenePath);
    }

    private static GameInfo ReadGame()
    {
        var info = new GameInfo
        {
            GameScene = SceneManager.GetActiveScene().name,
            Caster = GameObject.Find("Capsule").GetComponent<AbilityCaster>(),
            Rules = new ScoreRules(),
            Duration = 180f,
            FireballColor = new Color(0.85f, 0.25f, 0.12f),
            LaserColor = new Color(0.2f, 0.45f, 0.95f),
            DropChance = 1f,
            RechargeLifetime = 12f,
        };

        GameManager game = Object.FindAnyObjectByType<GameManager>();
        if (game != null)
        {
            var so = new SerializedObject(game);
            info.Duration = so.FindProperty("duration").floatValue;
            info.Rules = game.Rules;
        }

        EnemyManager enemies = Object.FindAnyObjectByType<EnemyManager>();
        if (enemies != null)
        {
            var so = new SerializedObject(enemies);
            info.FireballColor = so.FindProperty("fireballColor").colorValue;
            info.LaserColor = so.FindProperty("laserColor").colorValue;
            info.DropChance = so.FindProperty("dashRechargeDropChance").floatValue;
            var recharge = so.FindProperty("dashRechargePrefab").objectReferenceValue as DashRecharge;
            if (recharge != null)
                info.RechargeLifetime = new SerializedObject(recharge).FindProperty("lifetime").floatValue;
        }
        return info;
    }

    // ---------- Mise en page (référence 1920 x 1080) ----------

    private static void BuildCanvas(GameInfo info, Sprite white, Sprite background, Sprite mouse, Sprite orb, Font font)
    {
        var canvasGo = new GameObject("TitleUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = FireballSetupBuilder.CreateUI("Background", canvasGo.transform, background, Color.white);
        FireballSetupBuilder.Stretch(backdrop.rectTransform, 0f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentGo.transform.SetParent(canvasGo.transform, false);
        var content = (RectTransform)contentGo.transform;
        FireballSetupBuilder.Stretch(content, 0f);

        Text title = Label("Title", content, font, 64, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.45f));
        Place(title.rectTransform, 50f, 80f, 0f, 0f);
        title.text = "COMMENT JOUER";
        Text subtitle = Label("Subtitle", content, font, 26, TextAnchor.MiddleCenter, BodyColor);
        subtitle.fontStyle = FontStyle.Normal;
        Place(subtitle.rectTransform, 140f, 40f, 0f, 0f);
        subtitle.text = $"Survivez <color={Highlight}>{FormatDuration(info.Duration)}</color> face aux robots qui se téléportent dans l'arène.";

        const float cardTop = 220f, cardHeight = 570f, cardWidth = 560f, gap = 40f;
        float left = (1920f - cardWidth * 3f - gap * 2f) * 0.5f;
        BuildMovementCard(Card("Movement", "Déplacement", content, white, font, left, cardTop, cardWidth, cardHeight), mouse, font);
        BuildAbilitiesCard(Card("Abilities", "Sorts", content, white, font, left + cardWidth + gap, cardTop, cardWidth, cardHeight), info, white, font);
        BuildRechargeCard(Card("DashRecharge", "Recharge du dash", content, white, font, left + (cardWidth + gap) * 2f, cardTop, cardWidth, cardHeight), info, orb, font);

        // Score.
        Text score = Label("Score", content, font, 24, TextAnchor.MiddleCenter, BodyColor);
        score.fontStyle = FontStyle.Normal;
        Place(score.rectTransform, 815f, 40f, 0f, 0f);
        string bonus = info.Rules.pointsPerHealthPoint > 0f || info.Rules.healthMultiplier > 0f ? "   •   Bonus de fin selon la vie restante" : "";
        score.text = $"<color={Hex(info.FireballColor, 0.35f)}>Robot rouge</color> : {info.Rules.fireballEnemyPoints} pts   •   " +
                     $"<color={Hex(info.LaserColor, 0.35f)}>Robot bleu</color> : {info.Rules.laserEnemyPoints} pts{bonus}";

        Button play = BuildButton(content, white, font, "JOUER");
        Text hint = Label("Hint", content, font, 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.62f, 0.68f));
        hint.fontStyle = FontStyle.Normal;
        Place(hint.rectTransform, 1020f, 30f, 0f, 0f);
        hint.text = "ou Entrée / Espace";

        // Voile noir du lancement de la partie (au-dessus de tout).
        Image fade = FireballSetupBuilder.CreateUI("Fader", canvasGo.transform, white, Color.black);
        FireballSetupBuilder.Stretch(fade.rectTransform, 0f);
        fade.raycastTarget = true;
        var fader = fade.gameObject.AddComponent<CanvasGroup>();

        var screen = canvasGo.AddComponent<TitleScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("gameScene").stringValue = info.GameScene;
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("content").objectReferenceValue = contentGo.GetComponent<CanvasGroup>();
        so.FindProperty("fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildMovementCard(RectTransform card, Sprite mouse, Font font)
    {
        Image icon = FireballSetupBuilder.CreateUI("MouseIcon", card, mouse, Color.white);
        Center(icon.rectTransform, 80f, new Vector2(150f, 150f));

        Text text = Body("Text", card, font);
        Place(text.rectTransform, 250f, 300f, 36f, 36f);
        text.text =
            $"<color={Highlight}><b>Clic droit</b></color> sur le sol pour vous déplacer.\n\n" +
            "Les sorts partent vers le <b>curseur</b> : visez avec la souris, lancez au clavier.\n\n" +
            "Les ennemis <b>anticipent</b> vos déplacements : tourner en rond ne suffit pas, changez de direction au bon moment pour esquiver.";
    }

    private static void BuildAbilitiesCard(RectTransform card, GameInfo info, Sprite white, Font font)
    {
        float top = 80f;
        foreach (AbilityCaster.Slot slot in info.Caster.Slots)
        {
            if (slot.ability == null)
                continue;

            // Case façon barre de compétences : icône, cadre doré, touche.
            Image frame = FireballSetupBuilder.CreateUI("Slot_" + slot.ability.DisplayName, card, white, Gold);
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition = new Vector2(36f, -top);
            frameRect.sizeDelta = new Vector2(96f, 96f);
            Image icon = FireballSetupBuilder.CreateUI("Icon", frame.transform, slot.ability.Icon, Color.white);
            FireballSetupBuilder.Stretch(icon.rectTransform, 3f);
            Image badge = FireballSetupBuilder.CreateUI("KeyBadge", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.95f));
            RectTransform badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.sizeDelta = new Vector2(32f, 28f);
            Text key = FireballSetupBuilder.CreateText("Key", badge.transform, font, 20, TextAnchor.MiddleCenter);
            FireballSetupBuilder.Stretch(key.rectTransform, 0f);
            key.text = KeyLabel(slot.keyBinding);

            Text name = Label("Name", card, font, 28, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.45f));
            Place(name.rectTransform, top - 4f, 36f, 156f, 30f);
            name.text = $"{slot.ability.DisplayName}  <size=20><color=#9AA0AA>recharge {slot.ability.Cooldown:0.#}\u00A0s</color></size>";
            Text description = Body("Description", card, font);
            description.fontSize = 21;
            Place(description.rectTransform, top + 34f, 100f, 156f, 30f);
            description.text = Describe(slot.ability);

            top += 160f;
        }
    }

    private static void BuildRechargeCard(RectTransform card, GameInfo info, Sprite orb, Font font)
    {
        Image icon = FireballSetupBuilder.CreateUI("EnergyCellIcon", card, orb, Color.white);
        Center(icon.rectTransform, 80f, new Vector2(150f, 150f));

        string dashKey = info.Caster.Slots.Where(s => s.ability is DashAbility).Select(s => KeyLabel(s.keyBinding)).FirstOrDefault() ?? "E";
        string chance = info.DropChance >= 1f ? "laissent" : $"peuvent laisser ({info.DropChance * 100f:0} %)";
        Text text = Body("Text", card, font);
        Place(text.rectTransform, 250f, 300f, 36f, 36f);
        text.text =
            $"Les <color={Hex(info.LaserColor, 0.35f)}><b>robots bleus</b></color> (laser) {chance} une <color={Highlight}><b>cellule d'énergie</b></color> en mourant.\n\n" +
            $"Passez dessus pour recharger <b>immédiatement</b> votre dash ({dashKey}) : enchaînez les esquives.\n\n" +
            $"Elle n'est ramassée que si le dash est en recharge, et disparaît au bout de {info.RechargeLifetime:0} s.";
    }

    // Description d'une compétence d'après ses réglages.
    private static string Describe(Ability ability)
    {
        var so = new SerializedObject(ability);
        switch (ability)
        {
            case FireballAbility _:
                return "Projectile vers le curseur : explose sur le premier ennemi touché.";
            case LaserAbility _:
                return $"Après une courte charge, rayon de {so.FindProperty("length").floatValue:0}\u00A0m qui traverse tous les ennemis alignés.";
            case DashAbility _:
                return $"Bond éclair vers le curseur ({so.FindProperty("maxDistance").floatValue:0}\u00A0m\u00A0max) pour esquiver les tirs.";
            default:
                return "";
        }
    }

    // "<Keyboard>/#(a)" -> "A" (touche marquée A, quelle que soit la disposition du clavier).
    private static string KeyLabel(string binding)
    {
        int open = binding.IndexOf("#(");
        if (open >= 0 && binding.EndsWith(")"))
            return binding.Substring(open + 2, binding.Length - open - 3).ToUpperInvariant();
        int slash = binding.LastIndexOf('/');
        return (slash >= 0 ? binding.Substring(slash + 1) : binding).ToUpperInvariant();
    }

    private static string FormatDuration(float seconds)
    {
        int s = Mathf.RoundToInt(seconds);
        if (s % 60 == 0)
            return s / 60 == 1 ? "1 minute" : $"{s / 60} minutes";
        return $"{s / 60}:{s % 60:00}";
    }

    // Couleur éclaircie (lisible sur fond sombre), au format #RRGGBB.
    private static string Hex(Color color, float lighten)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, lighten));
    }

    // ---------- Éléments ----------

    // Carte : cadre doré, fond sombre, titre ; renvoie la carte (contenu placé depuis son bord haut).
    private static RectTransform Card(string name, string title, Transform parent, Sprite white, Font font, float x, float top, float width, float height)
    {
        Image frame = FireballSetupBuilder.CreateUI(name, parent, white, Gold);
        RectTransform rect = frame.rectTransform;
        // Ancrée en haut au centre (x compté depuis le bord gauche de la résolution de référence) : reste centrée
        // quel que soit le format de l'écran.
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x - 960f, -top);
        rect.sizeDelta = new Vector2(width, height);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, PanelColor);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);

        Text header = Label("Header", frame.transform, font, 34, TextAnchor.MiddleCenter, Color.white);
        Place(header.rectTransform, 18f, 44f, 20f, 20f);
        header.text = title;
        Image line = FireballSetupBuilder.CreateUI("Separator", frame.transform, white, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
        Place(line.rectTransform, 66f, 2f, 60f, 60f);
        return rect;
    }

    private static Button BuildButton(Transform parent, Sprite white, Font font, string label)
    {
        Image frame = FireballSetupBuilder.CreateUI("PlayButton", parent, white, Gold);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -925f);
        rect.sizeDelta = new Vector2(340f, 84f);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, Color.white);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);
        inner.raycastTarget = true;

        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = inner;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.13f, 0.18f);
        colors.highlightedColor = new Color(0.25f, 0.22f, 0.14f);
        colors.selectedColor = new Color(0.18f, 0.17f, 0.13f);
        colors.pressedColor = new Color(0.45f, 0.38f, 0.22f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = FireballSetupBuilder.CreateText("Label", frame.transform, font, 38, TextAnchor.MiddleCenter);
        FireballSetupBuilder.Stretch(text.rectTransform, 0f);
        text.text = label;
        return button;
    }

    private static Text Label(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        Text text = FireballSetupBuilder.CreateText(name, parent, font, size, anchor);
        text.color = color;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text Body(string name, Transform parent, Font font)
    {
        Text text = Label(name, parent, font, 23, TextAnchor.UpperLeft, BodyColor);
        text.fontStyle = FontStyle.Normal;
        text.lineSpacing = 1.15f;
        return text;
    }

    // Bande horizontale à une distance donnée du haut du parent, avec marges gauche/droite.
    private static void Place(RectTransform rect, float top, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // Centré horizontalement, à une distance donnée du haut du parent.
    private static void Center(RectTransform rect, float top, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = size;
    }

    // ---------- Textures ----------

    private static Sprite SaveSprite(string name, Texture2D texture)
    {
        string path = $"{ArtRoot}/{name}.png";
        FireballSetupBuilder.SaveTexture(path, texture, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Fond bleu nuit, plus clair au centre.
    private static Texture2D MakeBackground(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) * 2f - Vector2.one;
            float k = Mathf.Clamp01(1f - p.magnitude / 1.3f);
            Color c = Color.Lerp(new Color(0.015f, 0.02f, 0.035f), new Color(0.08f, 0.1f, 0.16f), k * k);
            c.a = 1f;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // Souris vue de dessus, bouton droit allumé.
    private static Texture2D MakeMouseIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(0.5f, 0.47f);
        Vector2 half = new Vector2(0.27f, 0.4f);
        const float buttonsY = 0.6f;
        const float stroke = 0.022f;
        Color outline = new Color(0.85f, 0.86f, 0.9f);
        Color body = new Color(0.13f, 0.15f, 0.2f);
        Color lit = new Color(1f, 0.78f, 0.3f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x) / half.x, Mathf.Abs(p.y - center.y) / half.y);
            // Superellipse : silhouette arrondie de souris.
            float d = Mathf.Pow(Mathf.Pow(q.x, 2.6f) + Mathf.Pow(q.y, 2.6f), 1f / 2.6f);
            float edge = (d - 1f) * Mathf.Min(half.x, half.y);

            Color c = Color.clear;
            if (edge < 0f)
            {
                c = body;
                bool buttons = p.y > buttonsY;
                if (buttons && p.x > 0.5f + stroke * 0.5f)
                    c = Color.Lerp(lit, Color.white, Mathf.Clamp01((p.y - buttonsY) * 1.5f) * 0.3f);
                // Séparations entre les boutons et le corps.
                if ((buttons && Mathf.Abs(p.x - 0.5f) < stroke * 0.5f) || Mathf.Abs(p.y - buttonsY) < stroke * 0.5f)
                    c = outline;
            }
            float border = 1f - Mathf.Clamp01(Mathf.Abs(edge) / stroke);
            if (border > 0f)
                c = Color.Lerp(c.a > 0f ? c : new Color(outline.r, outline.g, outline.b, 0f), outline, border);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // Cellule d'énergie : boule dorée au coeur blanc, halo orange.
    private static Texture2D MakeOrbIcon(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color white = DashSetupBuilder.BoltWhite;
        Color yellow = DashSetupBuilder.SpeedYellow;
        Color orange = DashSetupBuilder.SpeedOrange;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) * 2f - Vector2.one;
            float d = p.magnitude;
            const float radius = 0.42f;
            Color c;
            if (d < radius)
            {
                float k = d / radius;
                c = Color.Lerp(white, Color.Lerp(yellow, orange, k * k), Mathf.SmoothStep(0f, 1f, k * 1.2f));
                c.a = 1f;
            }
            else
            {
                float halo = Mathf.Exp(-(d - radius) * 6f) * Mathf.Clamp01((1f - d) / 0.1f);
                c = new Color(orange.r, orange.g * 1.2f, orange.b, halo * 0.9f);
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
}
