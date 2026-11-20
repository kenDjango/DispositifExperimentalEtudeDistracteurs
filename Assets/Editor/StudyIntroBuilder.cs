using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Génère la scène de présentation de l'étude, première scène du build (avant l'écran des commandes) : message
// d'information, questionnaire démographique (ParticipantProfile.Questions) et case de consentement.
public static class StudyIntroBuilder
{
    private const string ScenePath = "Assets/Scenes/StudyIntro.unity";
    private const string NextScene = "TitleScreen";

    private static readonly Color Gold = GameLoopSetupBuilder.Gold;
    private static readonly Color Title = new Color(1f, 0.85f, 0.45f);
    private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 1f);
    private static readonly Color BodyColor = new Color(0.85f, 0.86f, 0.9f);
    private static readonly Color DimColor = new Color(0.6f, 0.62f, 0.68f);

    private const float CardWidth = 1500f;
    // Colonne des intitulés, puis zone des choix.
    private const float LabelWidth = 430f;
    private const float RowHeight = 76f;

    [MenuItem("Tools/Game Loop/Build Study Intro")]
    public static void Build()
    {
        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>($"{FireballSetupBuilder.ArtRoot}/T_UIWhite.png");
        // Fond de l'écran des commandes (généré par TitleScreenBuilder), pour que les deux écrans se ressemblent.
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>($"{FireballSetupBuilder.ArtRoot}/T_TitleBackground.png");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite check = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Scene previous = SceneManager.GetActiveScene();
        if (previous.isDirty)
            EditorSceneManager.SaveScene(previous);
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

            BuildCanvas(white, background, circle, rounded, check, font);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }

        // Première scène du build : présentation, puis écran des commandes et partie.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Présentation de l'étude générée : " + ScenePath);
    }

    // ---------- Mise en page (référence 1920 x 1080) ----------

    private static void BuildCanvas(Sprite white, Sprite background, Sprite circle, Sprite rounded, Sprite check, Font font)
    {
        var canvasGo = new GameObject("StudyIntroUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = FireballSetupBuilder.CreateUI("Background", canvasGo.transform, background != null ? background : white,
            background != null ? Color.white : new Color(0.02f, 0.03f, 0.05f));
        FireballSetupBuilder.Stretch(backdrop.rectTransform, 0f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentGo.transform.SetParent(canvasGo.transform, false);
        var content = (RectTransform)contentGo.transform;
        FireballSetupBuilder.Stretch(content, 0f);

        Text title = Label("Title", content, font, 56, TextAnchor.MiddleCenter, Title);
        Place(title.rectTransform, 36f, 70f, 0f, 0f);
        title.text = "PARTICIPATION À L'ÉTUDE";

        // Message d'information (texte réglé par StudyIntroScreen, modifiable dans son inspecteur).
        RectTransform info = Card("Information", content, white, 124f, 196f);
        Text message = Body("Message", info, font, 25);
        FireballSetupBuilder.Stretch(message.rectTransform, 0f);
        message.rectTransform.offsetMin = new Vector2(44f, 24f);
        message.rectTransform.offsetMax = new Vector2(-44f, -26f);

        // Questionnaire.
        RectTransform form = Card("Questionnaire", content, white, 344f, 564f);
        Text header = Label("Header", form, font, 32, TextAnchor.MiddleLeft, Color.white);
        Place(header.rectTransform, 18f, 44f, 44f, 44f);
        header.text = "Quelques questions sur vous";
        Image line = FireballSetupBuilder.CreateUI("Separator", form, white, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
        Place(line.rectTransform, 68f, 2f, 44f, 44f);

        ParticipantProfile.Question[] questions = ParticipantProfile.Questions;
        var scales = new RatingScale[questions.Length];
        for (int i = 0; i < questions.Length; i++)
            scales[i] = BuildQuestion(form, questions[i], 84f + i * RowHeight, circle, rounded, font);

        Toggle consent = BuildConsent(form, white, check, font, 84f + questions.Length * RowHeight + 8f);

        Text hint = Label("Hint", content, font, 20, TextAnchor.MiddleCenter, DimColor);
        hint.fontStyle = FontStyle.Italic;
        Place(hint.rectTransform, 920f, 30f, 0f, 0f);

        Button next = GameLoopSetupBuilder.BuildButton("ContinueButton", content, white, font, "CONTINUER");
        RectTransform nextRect = (RectTransform)next.transform;
        nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(0.5f, 1f);
        nextRect.anchoredPosition = new Vector2(0f, -960f);
        nextRect.sizeDelta = new Vector2(340f, 80f);
        next.GetComponentInChildren<Text>().fontSize = 36;
        ColorBlock colors = next.colors;
        // Opaque : sinon le cadre doré transparaît et le bouton paraît actif.
        colors.disabledColor = new Color(0.14f, 0.14f, 0.15f, 1f);
        next.colors = colors;

        // Voile noir du passage à l'écran suivant (au-dessus de tout).
        Image fade = FireballSetupBuilder.CreateUI("Fader", canvasGo.transform, white, Color.black);
        FireballSetupBuilder.Stretch(fade.rectTransform, 0f);
        fade.raycastTarget = true;
        var fader = fade.gameObject.AddComponent<CanvasGroup>();

        var screen = canvasGo.AddComponent<StudyIntroScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("nextScene").stringValue = NextScene;
        so.FindProperty("messageText").objectReferenceValue = message;
        SerializedProperty scalesProp = so.FindProperty("questions");
        scalesProp.arraySize = scales.Length;
        for (int i = 0; i < scales.Length; i++)
            scalesProp.GetArrayElementAtIndex(i).objectReferenceValue = scales[i];
        so.FindProperty("consentToggle").objectReferenceValue = consent;
        so.FindProperty("continueButton").objectReferenceValue = next;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("content").objectReferenceValue = contentGo.GetComponent<CanvasGroup>();
        so.FindProperty("fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Une ligne : intitulé (et précision) à gauche, choix à droite. Échelle : pastilles rondes numérotées entre
    // les libellés des extrémités ; sinon, boutons arrondis portant le libellé de chaque choix.
    private static RatingScale BuildQuestion(RectTransform form, ParticipantProfile.Question question, float top, Sprite circle, Sprite rounded, Font font)
    {
        var row = new GameObject(question.Key, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(form, false);
        Place(row, top, RowHeight, 44f, 44f);

        bool detail = !string.IsNullOrEmpty(question.Detail);
        Text title = Label("Title", row, font, 26, detail ? TextAnchor.LowerLeft : TextAnchor.MiddleLeft, Title);
        Column(title.rectTransform, 0f, LabelWidth, detail ? 0.45f : 0f, 1f);
        title.text = question.Title;
        if (detail)
        {
            Text sub = Body("Detail", row, font, 19);
            sub.color = DimColor;
            sub.alignment = TextAnchor.UpperLeft;
            Column(sub.rectTransform, 0f, LabelWidth, 0f, 0.42f);
            sub.text = question.Detail;
        }

        var choices = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
        choices.SetParent(row, false);
        choices.anchorMin = new Vector2(0f, 0f);
        choices.anchorMax = new Vector2(1f, 1f);
        choices.offsetMin = new Vector2(LabelWidth, 0f);
        choices.offsetMax = Vector2.zero;

        if (!question.IsScale)
        {
            // Boutons aussi larges que la place le permet (au plus 200).
            float available = CardWidth - 88f - LabelWidth;
            float step = Mathf.Min(212f, available / question.Options.Length);
            return GameLoopSetupBuilder.BuildChoices(choices, question.Options, rounded, new Vector2(step - 10f, 50f), step, font, 18);
        }

        const float stepScale = 64f;
        RatingScale scale = GameLoopSetupBuilder.BuildChoices(choices, question.Options, circle, new Vector2(48f, 48f), stepScale, font, 21);
        float edge = (question.Options.Length - 1) * stepScale * 0.5f + 40f;
        foreach ((string text, float side) in new[] { (question.Low, -1f), (question.High, 1f) })
        {
            Text label = Label(side < 0f ? "Low" : "High", choices, font, 20, side < 0f ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, BodyColor);
            label.fontStyle = FontStyle.Normal;
            label.text = text;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(side < 0f ? 1f : 0f, 0.5f);
            rect.anchoredPosition = new Vector2(side * edge, 0f);
            rect.sizeDelta = new Vector2(160f, 40f);
        }
        return scale;
    }

    // Case à cocher dorée et phrase de consentement ; toute la ligne est cliquable.
    private static Toggle BuildConsent(RectTransform form, Sprite white, Sprite check, Font font, float top)
    {
        Image row = FireballSetupBuilder.CreateUI("Consent", form, white, new Color(1f, 1f, 1f, 0.05f));
        Place(row.rectTransform, top, 64f, 44f, 44f);
        row.raycastTarget = true;

        Image box = FireballSetupBuilder.CreateUI("Box", row.transform, white, Gold);
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.anchoredPosition = new Vector2(22f, 0f);
        boxRect.sizeDelta = new Vector2(38f, 38f);
        Image inner = FireballSetupBuilder.CreateUI("Background", box.transform, white, PanelColor);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);
        Image mark = FireballSetupBuilder.CreateUI("Checkmark", box.transform, check, Title);
        FireballSetupBuilder.Stretch(mark.rectTransform, 2f);

        Text label = Body("Label", row.transform, font, 24);
        label.alignment = TextAnchor.MiddleLeft;
        FireballSetupBuilder.Stretch(label.rectTransform, 0f);
        label.rectTransform.offsetMin = new Vector2(80f, 0f);
        label.text = "J'ai lu les informations ci-dessus et j'accepte de participer à cette étude.";

        var toggle = row.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = mark;
        toggle.isOn = false;
        ColorBlock colors = toggle.colors;
        colors.normalColor = new Color(0.75f, 0.75f, 0.75f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.fadeDuration = 0.06f;
        toggle.colors = colors;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        return toggle;
    }

    // ---------- Éléments ----------

    // Carte centrée : cadre doré, fond sombre ; renvoie le fond (contenu placé depuis son bord haut).
    private static RectTransform Card(string name, Transform parent, Sprite white, float top, float height)
    {
        Image frame = FireballSetupBuilder.CreateUI(name, parent, white, Gold);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(CardWidth, height);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, PanelColor);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);
        return inner.rectTransform;
    }

    private static Text Label(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        Text text = FireballSetupBuilder.CreateText(name, parent, font, size, anchor);
        text.color = color;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text Body(string name, Transform parent, Font font, int size)
    {
        Text text = Label(name, parent, font, size, TextAnchor.UpperLeft, BodyColor);
        text.fontStyle = FontStyle.Normal;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
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

    // Colonne de largeur fixe depuis le bord gauche, sur une portion de la hauteur du parent (0 : bas, 1 : haut).
    private static void Column(RectTransform rect, float left, float width, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0f, bottom);
        rect.anchorMax = new Vector2(0f, top);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(left + width, 0f);
    }
}
