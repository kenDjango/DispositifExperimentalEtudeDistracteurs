using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Installe la boucle de jeu dans la scène ouverte : GameManager, chrono et score en haut de l'écran,
// écran de fin, questionnaire NASA-TLX entre les parties, écran de remerciement en fin de session (et l'EventSystem
// nécessaire au clic).
public static class GameLoopSetupBuilder
{
    private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.9f);
    internal static readonly Color Gold = new Color(0.78f, 0.67f, 0.43f);

    [MenuItem("Tools/Game Loop/Setup Scene")]
    public static void SetupScene()
    {
        GameObject player = GameObject.Find("Capsule");
        EnemyManager enemies = Object.FindAnyObjectByType<EnemyManager>();

        GameManager game = Object.FindAnyObjectByType<GameManager>();
        if (game == null)
        {
            var go = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(go, "GameManager");
            game = go.AddComponent<GameManager>();
        }
        var so = new SerializedObject(game);
        so.FindProperty("player").objectReferenceValue = player.GetComponent<Health>();
        so.FindProperty("enemyManager").objectReferenceValue = enemies;
        so.ApplyModifiedProperties();

        // Mesures de performance de l'étude et leur envoi à la Google Sheet (l'URL, le participant et la
        // condition, saisis dans l'inspecteur, sont conservés).
        PerformanceMetrics metrics = game.GetComponent<PerformanceMetrics>();
        if (metrics == null)
            metrics = Undo.AddComponent<PerformanceMetrics>(game.gameObject);
        var metricsSo = new SerializedObject(metrics);
        metricsSo.FindProperty("game").objectReferenceValue = game;
        metricsSo.FindProperty("playerMover").objectReferenceValue = player.GetComponent<ClickToMove>();
        metricsSo.FindProperty("playerCaster").objectReferenceValue = player.GetComponent<AbilityCaster>();
        metricsSo.ApplyModifiedProperties();

        MetricsReporter reporter = game.GetComponent<MetricsReporter>();
        if (reporter == null)
            reporter = Undo.AddComponent<MetricsReporter>(game.gameObject);
        var reporterSo = new SerializedObject(reporter);
        reporterSo.FindProperty("metrics").objectReferenceValue = metrics;
        reporterSo.ApplyModifiedProperties();

        // Déplacement mesuré du joueur, pour la visée anticipée des ennemis.
        if (player.GetComponent<MotionTracker>() == null)
            Undo.AddComponent<MotionTracker>(player);

        BakeNavigation(enemies);
        EnsureEventSystem();
        BuildUI(game);
        EnsureInBuildSettings(player.scene.path);

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
    }

    // Carte de navigation des robots, calculée ici (les maillages du décor ne sont lisibles que dans l'éditeur) et
    // enregistrée à côté de la scène. À relancer après toute modification du décor ou de la zone de jeu.
    [MenuItem("Tools/Game Loop/Bake Arena Navigation")]
    public static void BakeNavigation()
    {
        BakeNavigation(Object.FindAnyObjectByType<EnemyManager>());
        EditorSceneManager.SaveOpenScenes();
    }

    private static void BakeNavigation(EnemyManager enemies)
    {
        if (enemies == null)
            return;
        UnityEngine.AI.NavMeshData data = enemies.CreateNavigationData();
        if (data == null)
        {
            Debug.LogWarning("Carte de navigation non calculée : l'EnemyManager n'a pas de zone d'apparition (Spawn Area).");
            return;
        }

        Scene scene = enemies.gameObject.scene;
        string path = $"{System.IO.Path.GetDirectoryName(scene.path)}/{scene.name}_ArenaNavMesh.asset".Replace('\\', '/');
        AssetDatabase.DeleteAsset(path);
        data.name = scene.name + "_ArenaNavMesh";
        AssetDatabase.CreateAsset(data, path);

        var so = new SerializedObject(enemies);
        so.FindProperty("navigationData").objectReferenceValue = data;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Carte de navigation des robots enregistrée : " + path);
    }

    // Le bouton de l'écran de fin a besoin d'un EventSystem branché sur le nouveau système d'entrée.
    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "EventSystem");
    }

    // La partie suivante recharge la scène par son index de build.
    private static void EnsureInBuildSettings(string path)
    {
        if (EditorBuildSettings.scenes.Any(s => s.path == path))
            return;
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();
    }

    // ---------- UI ----------

    private static void BuildUI(GameManager game)
    {
        // L'option d'affichage du score de performance survit à la reconstruction.
        GameObject existing = GameObject.Find("GameUI");
        bool showPerformance = false;
        if (existing != null)
        {
            GameHUD oldHud = existing.GetComponent<GameHUD>();
            if (oldHud != null)
                showPerformance = new SerializedObject(oldHud).FindProperty("showPerformanceScore").boolValue;
            Undo.DestroyObjectImmediate(existing);
        }

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>($"{FireballSetupBuilder.ArtRoot}/T_UIWhite.png");
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas séparé du HUD des compétences (reconstruit par son propre outil), affiché par-dessus.
        var canvasGo = new GameObject("GameUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "GameUI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Chrono en haut au centre, cadre doré comme les cases de compétences.
        Text timer = Box("Timer", canvasGo.transform, white, font, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(170f, 70f), 44, TextAnchor.MiddleCenter);
        // Score en haut à droite.
        Text score = Box("Score", canvasGo.transform, white, font, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(260f, 70f), 34, TextAnchor.MiddleCenter);

        // Bandeau sous le chrono pendant la partie d'entraînement.
        Text practice = Box("PracticeBadge", canvasGo.transform, white, font, new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(260f, 42f), 24, TextAnchor.MiddleCenter);
        practice.text = "ENTRAÎNEMENT";
        practice.color = new Color(0.55f, 0.8f, 1f);

        // Score de performance de l'étude, sous le score (affiché seulement si l'option du HUD est cochée).
        Text performance = Box("PerformanceScore", canvasGo.transform, white, font, new Vector2(1f, 1f), new Vector2(-24f, -100f), new Vector2(660f, 124f), 26, TextAnchor.MiddleCenter);
        performance.color = new Color(0.55f, 0.8f, 1f);
        performance.lineSpacing = 1.1f;
        performance.transform.parent.gameObject.SetActive(showPerformance);

        var hud = canvasGo.AddComponent<GameHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("showPerformanceScore").boolValue = showPerformance;
        so.FindProperty("metrics").objectReferenceValue = game.GetComponent<PerformanceMetrics>();
        so.FindProperty("performancePanel").objectReferenceValue = performance.transform.parent.gameObject;
        so.FindProperty("performanceText").objectReferenceValue = performance;
        so.FindProperty("distraction").objectReferenceValue = Object.FindAnyObjectByType<DistractionDirector>(FindObjectsInactive.Include);
        so.FindProperty("game").objectReferenceValue = game;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("scoreText").objectReferenceValue = score;
        so.FindProperty("practiceBadge").objectReferenceValue = practice.transform.parent.gameObject;
        so.ApplyModifiedProperties();

        GameOverScreen gameOver = BuildGameOverScreen(canvasGo, game, white, font);
        WorkloadForm form = BuildWorkloadForm(canvasGo, white, font);
        AwarenessForm awareness = BuildAwarenessForm(canvasGo, white, font);
        CanvasGroup thanks = BuildThanksScreen(canvasGo, white, font);

        var session = canvasGo.AddComponent<StudySession>();
        var sessionSo = new SerializedObject(session);
        sessionSo.FindProperty("game").objectReferenceValue = game;
        sessionSo.FindProperty("gameOverScreen").objectReferenceValue = gameOver;
        sessionSo.FindProperty("workloadForm").objectReferenceValue = form;
        sessionSo.FindProperty("awarenessForm").objectReferenceValue = awareness;
        sessionSo.FindProperty("reporter").objectReferenceValue = game.GetComponent<MetricsReporter>();
        sessionSo.FindProperty("thanksPanel").objectReferenceValue = thanks;
        sessionSo.ApplyModifiedProperties();
    }

    // Cadre doré + fond sombre + texte centré, ancré sur un bord de l'écran.
    private static Text Box(string name, Transform parent, Sprite white, Font font, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Image frame = FireballSetupBuilder.CreateUI(name, parent, white, Gold);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, PanelColor);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);
        Text text = FireballSetupBuilder.CreateText("Text", frame.transform, font, fontSize, alignment);
        FireballSetupBuilder.Stretch(text.rectTransform, 0f);
        return text;
    }

    private static GameOverScreen BuildGameOverScreen(GameObject canvasGo, GameManager game, Sprite white, Font font)
    {
        // Voile sombre plein écran (bloque aussi les clics vers le jeu).
        Image dim = FireballSetupBuilder.CreateUI("GameOver", canvasGo.transform, white, new Color(0f, 0f, 0f, 0.7f));
        FireballSetupBuilder.Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var group = dim.gameObject.AddComponent<CanvasGroup>();

        Image frame = FireballSetupBuilder.CreateUI("Window", dim.transform, white, Gold);
        RectTransform window = frame.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(720f, 600f);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.97f));
        FireballSetupBuilder.Stretch(inner.rectTransform, 4f);

        Text title = FireballSetupBuilder.CreateText("Title", frame.transform, font, 52, TextAnchor.MiddleCenter);
        Place(title.rectTransform, 30f, 80f);

        // Détail : deux colonnes alignées ligne à ligne.
        Text labels = FireballSetupBuilder.CreateText("Labels", frame.transform, font, 26, TextAnchor.UpperLeft);
        Place(labels.rectTransform, 140f, 220f, 60f, 260f);
        Text values = FireballSetupBuilder.CreateText("Values", frame.transform, font, 26, TextAnchor.UpperRight);
        Place(values.rectTransform, 140f, 220f, 460f, 60f);
        labels.lineSpacing = values.lineSpacing = 1.25f;
        labels.fontStyle = FontStyle.Normal;

        // Séparateur au-dessus du total.
        Image line = FireballSetupBuilder.CreateUI("Separator", frame.transform, white, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        Place(line.rectTransform, 372f, 2f, 60f, 60f);

        Text total = FireballSetupBuilder.CreateText("Total", frame.transform, font, 48, TextAnchor.MiddleCenter);
        total.color = new Color(1f, 0.85f, 0.45f);
        Place(total.rectTransform, 385f, 64f);
        Text best = FireballSetupBuilder.CreateText("Best", frame.transform, font, 22, TextAnchor.MiddleCenter);
        best.fontStyle = FontStyle.Normal;
        best.color = new Color(0.85f, 0.85f, 0.85f);
        Place(best.rectTransform, 450f, 30f);

        Button next = BuildButton("ContinueButton", frame.transform, white, font, "Continuer");
        RectTransform buttonRect = (RectTransform)next.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 36f);
        buttonRect.sizeDelta = new Vector2(300f, 70f);

        var screen = canvasGo.AddComponent<GameOverScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("game").objectReferenceValue = game;
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("window").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("labelsText").objectReferenceValue = labels;
        so.FindProperty("valuesText").objectReferenceValue = values;
        so.FindProperty("totalText").objectReferenceValue = total;
        so.FindProperty("bestText").objectReferenceValue = best;
        so.FindProperty("continueButton").objectReferenceValue = next;
        so.ApplyModifiedProperties();

        dim.gameObject.SetActive(false);
        return screen;
    }

    // ---------- Questionnaire NASA-TLX ----------

    // Fenêtre plein écran : en-tête (participant, tâche, date) comme sur le questionnaire papier, puis les six
    // questions en deux colonnes, chacune avec son échelle de 1 (faible) à 10 (élevée), et le bouton « Valider ».
    private static WorkloadForm BuildWorkloadForm(GameObject canvasGo, Sprite white, Font font)
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        Image dim = FireballSetupBuilder.CreateUI("WorkloadForm", canvasGo.transform, white, new Color(0f, 0f, 0f, 0.85f));
        FireballSetupBuilder.Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var group = dim.gameObject.AddComponent<CanvasGroup>();

        Image frame = FireballSetupBuilder.CreateUI("Window", dim.transform, white, Gold);
        RectTransform window = frame.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1660f, 1030f);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.98f));
        FireballSetupBuilder.Stretch(inner.rectTransform, 4f);

        Text title = FireballSetupBuilder.CreateText("Title", frame.transform, font, 44, TextAnchor.MiddleCenter);
        title.text = "NASA-TLX";
        title.color = new Color(1f, 0.85f, 0.45f);
        Place(title.rectTransform, 16f, 52f);
        Text subtitle = FireballSetupBuilder.CreateText("Subtitle", frame.transform, font, 22, TextAnchor.MiddleCenter);
        subtitle.text = "Évaluation de la charge de travail perçue pendant la partie";
        subtitle.fontStyle = FontStyle.Normal;
        Place(subtitle.rectTransform, 68f, 28f);

        // En-tête : trois cases encadrées.
        Image header = FireballSetupBuilder.CreateUI("Header", frame.transform, white, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        Place(header.rectTransform, 110f, 54f, 60f, 60f);
        string[] headings = { "Participant :", "Tâche :", "Date :" };
        var values = new Text[headings.Length];
        for (int i = 0; i < headings.Length; i++)
        {
            Image cell = FireballSetupBuilder.CreateUI(headings[i].TrimEnd(' ', ':'), header.transform, white, new Color(0.08f, 0.1f, 0.14f));
            RectTransform rect = cell.rectTransform;
            rect.anchorMin = new Vector2(i / 3f, 0f);
            rect.anchorMax = new Vector2((i + 1) / 3f, 1f);
            rect.offsetMin = new Vector2(i == 0 ? 2f : 1f, 2f);
            rect.offsetMax = new Vector2(i == 2 ? -2f : -1f, -2f);
            Text label = FireballSetupBuilder.CreateText("Label", cell.transform, font, 22, TextAnchor.MiddleLeft);
            label.text = headings[i];
            label.color = Gold;
            FireballSetupBuilder.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            values[i] = FireballSetupBuilder.CreateText("Value", cell.transform, font, 22, TextAnchor.MiddleLeft);
            values[i].fontStyle = FontStyle.Normal;
            FireballSetupBuilder.Stretch(values[i].rectTransform, 0f);
            values[i].rectTransform.offsetMin = new Vector2(i == 0 ? 160f : 110f, 0f);
        }

        Text instructions = FireballSetupBuilder.CreateText("Instructions", frame.transform, font, 22, TextAnchor.MiddleCenter);
        instructions.text = "Pour chaque question, sélectionnez une note de 1 (faible) à 10 (élevée).";
        instructions.fontStyle = FontStyle.Italic;
        instructions.color = new Color(0.85f, 0.85f, 0.85f);
        Place(instructions.rectTransform, 180f, 32f);

        // Six questions sur deux colonnes.
        const float gridTop = 228f, rowHeight = 222f, columnWidth = 740f, gap = 60f;
        var scales = new RatingScale[WorkloadForm.Dimensions.Length];
        for (int i = 0; i < scales.Length; i++)
        {
            var (_, heading, question) = WorkloadForm.Dimensions[i];
            float left = 60f + (i % 2) * (columnWidth + gap);
            var cell = new GameObject(heading, typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(frame.transform, false);
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(left, -(gridTop + (i / 2) * rowHeight));
            cell.sizeDelta = new Vector2(columnWidth, rowHeight - 12f);
            scales[i] = BuildQuestion(cell, heading, question, white, circle, font);
        }

        Text hint = FireballSetupBuilder.CreateText("Hint", frame.transform, font, 20, TextAnchor.MiddleCenter);
        hint.fontStyle = FontStyle.Italic;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 100f);
        hintRect.sizeDelta = new Vector2(900f, 28f);

        Button submit = BuildButton("SubmitButton", frame.transform, white, font, "Valider");
        RectTransform submitRect = (RectTransform)submit.transform;
        submitRect.anchorMin = submitRect.anchorMax = submitRect.pivot = new Vector2(0.5f, 0f);
        submitRect.anchoredPosition = new Vector2(0f, 26f);
        submitRect.sizeDelta = new Vector2(300f, 66f);
        ColorBlock colors = submit.colors;
        // Opaque : sinon le cadre doré transparaît et le bouton paraît actif.
        colors.disabledColor = new Color(0.14f, 0.14f, 0.15f, 1f);
        submit.colors = colors;

        var form = canvasGo.AddComponent<WorkloadForm>();
        var so = new SerializedObject(form);
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("window").objectReferenceValue = window;
        SerializedProperty scalesProp = so.FindProperty("scales");
        scalesProp.arraySize = scales.Length;
        for (int i = 0; i < scales.Length; i++)
            scalesProp.GetArrayElementAtIndex(i).objectReferenceValue = scales[i];
        so.FindProperty("participantText").objectReferenceValue = values[0];
        so.FindProperty("taskText").objectReferenceValue = values[1];
        so.FindProperty("dateText").objectReferenceValue = values[2];
        so.FindProperty("submitButton").objectReferenceValue = submit;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.ApplyModifiedProperties();

        dim.gameObject.SetActive(false);
        return form;
    }

    // Intitulé, question, et rangée « Faible » 1 … 10 « Élevée » sous un bandeau léger.
    private static RatingScale BuildQuestion(RectTransform cell, string heading, string question, Sprite white, Sprite circle, Font font)
    {
        Text title = FireballSetupBuilder.CreateText("Title", cell, font, 26, TextAnchor.UpperLeft);
        title.text = heading;
        title.color = new Color(1f, 0.85f, 0.45f);
        Place(title.rectTransform, 0f, 32f, 0f, 0f);

        Text text = FireballSetupBuilder.CreateText("Question", cell, font, 19, TextAnchor.UpperLeft);
        text.text = question;
        text.fontStyle = FontStyle.Normal;
        text.color = new Color(0.9f, 0.9f, 0.9f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Place(text.rectTransform, 36f, 90f, 0f, 0f);

        Image band = FireballSetupBuilder.CreateUI("Scale", cell, white, new Color(1f, 1f, 1f, 0.05f));
        Place(band.rectTransform, 136f, 64f, 0f, 0f);

        const float labelWidth = 92f, step = 55f, size = 46f;
        Text low = FireballSetupBuilder.CreateText("Low", band.transform, font, 20, TextAnchor.MiddleCenter);
        low.text = "Faible";
        low.fontStyle = FontStyle.Normal;
        Text high = FireballSetupBuilder.CreateText("High", band.transform, font, 20, TextAnchor.MiddleCenter);
        high.text = "Élevée";
        high.fontStyle = FontStyle.Normal;
        foreach ((Text label, float x) in new[] { (low, 0f), (high, 1f) })
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(x, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(labelWidth, 0f);
        }

        var labels = new string[WorkloadForm.MaxRating];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = (i + 1).ToString();
        return BuildChoices(band.transform, labels, circle, new Vector2(size, size), step, font, 20);
    }

    // Rangée de pastilles cliquables (une seule choisie, valeur 1 à N), centrée dans le parent : anneau doré qui
    // s'allume au survol, intérieur coloré par RatingScale selon la sélection, libellé. Un sprite à bords (ex.
    // UISprite) donne des boutons arrondis étirables.
    internal static RatingScale BuildChoices(Transform parent, string[] labels, Sprite sprite, Vector2 size, float step, Font font, int fontSize)
    {
        var options = new Button[labels.Length];
        var fills = new Image[options.Length];
        float first = -(options.Length - 1) * step * 0.5f;
        bool sliced = sprite.border != Vector4.zero;
        for (int i = 0; i < options.Length; i++)
        {
            Image ring = FireballSetupBuilder.CreateUI($"Option{i + 1}", parent, sprite, Gold);
            RectTransform rect = ring.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(first + i * step, 0f);
            rect.sizeDelta = size;
            ring.raycastTarget = true;
            Image disc = FireballSetupBuilder.CreateUI("Fill", ring.transform, sprite, Color.white);
            FireballSetupBuilder.Stretch(disc.rectTransform, 3f);
            if (sliced)
                ring.type = disc.type = Image.Type.Sliced;
            Text label = FireballSetupBuilder.CreateText("Label", ring.transform, font, fontSize, TextAnchor.MiddleCenter);
            label.text = labels[i];
            FireballSetupBuilder.Stretch(label.rectTransform, 0f);
            label.GetComponent<Outline>().enabled = false;

            // Survol : l'anneau, terne au repos, s'allume (l'intérieur garde la couleur de la sélection).
            var button = ring.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.55f, 0.55f, 0.55f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(0.55f, 0.55f, 0.55f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            // Pas de navigation clavier entre pastilles (sélection à la souris).
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            options[i] = button;
            fills[i] = disc;
        }

        var scale = parent.gameObject.AddComponent<RatingScale>();
        var so = new SerializedObject(scale);
        SerializedProperty optionsProp = so.FindProperty("options");
        SerializedProperty fillsProp = so.FindProperty("fills");
        optionsProp.arraySize = fillsProp.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            optionsProp.GetArrayElementAtIndex(i).objectReferenceValue = options[i];
            fillsProp.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
        }
        so.ApplyModifiedProperties();
        return scale;
    }

    // ---------- Fin de session ----------

    // Question ouverte après le dernier NASA-TLX : la question, un champ de texte libre (facultatif) et « Valider ».
    private static AwarenessForm BuildAwarenessForm(GameObject canvasGo, Sprite white, Font font)
    {
        Image dim = FireballSetupBuilder.CreateUI("AwarenessForm", canvasGo.transform, white, new Color(0f, 0f, 0f, 0.85f));
        FireballSetupBuilder.Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var group = dim.gameObject.AddComponent<CanvasGroup>();

        Image frame = FireballSetupBuilder.CreateUI("Window", dim.transform, white, Gold);
        RectTransform window = frame.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1200f, 640f);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.98f));
        FireballSetupBuilder.Stretch(inner.rectTransform, 4f);

        Text question = FireballSetupBuilder.CreateText("Question", frame.transform, font, 34, TextAnchor.MiddleCenter);
        question.text = AwarenessForm.Question;
        question.color = new Color(1f, 0.85f, 0.45f);
        question.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(question.rectTransform, 40f, 100f, 70f, 70f);

        Text instructions = FireballSetupBuilder.CreateText("Instructions", frame.transform, font, 22, TextAnchor.MiddleCenter);
        instructions.text = "Répondez librement. Si rien ne vous a marqué, vous pouvez laisser le champ vide.";
        instructions.fontStyle = FontStyle.Italic;
        instructions.color = new Color(0.85f, 0.85f, 0.85f);
        Place(instructions.rectTransform, 145f, 32f);

        // Champ de texte : cadre doré, fond sombre, texte sur plusieurs lignes.
        Image fieldFrame = FireballSetupBuilder.CreateUI("Answer", frame.transform, white, Gold);
        Place(fieldFrame.rectTransform, 200f, 300f, 70f, 70f);
        Image fieldBack = FireballSetupBuilder.CreateUI("Background", fieldFrame.transform, white, new Color(0.08f, 0.1f, 0.14f));
        FireballSetupBuilder.Stretch(fieldBack.rectTransform, 2f);
        fieldBack.raycastTarget = true;
        Text text = FireballSetupBuilder.CreateText("Text", fieldBack.transform, font, 24, TextAnchor.UpperLeft);
        text.fontStyle = FontStyle.Normal;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.GetComponent<Outline>().enabled = false;
        FireballSetupBuilder.Stretch(text.rectTransform, 16f);
        Text placeholder = FireballSetupBuilder.CreateText("Placeholder", fieldBack.transform, font, 24, TextAnchor.UpperLeft);
        placeholder.text = "Votre réponse…";
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.GetComponent<Outline>().enabled = false;
        FireballSetupBuilder.Stretch(placeholder.rectTransform, 16f);
        var field = fieldBack.gameObject.AddComponent<InputField>();
        field.targetGraphic = fieldBack;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = InputField.LineType.MultiLineNewline;
        field.characterLimit = 1000;

        Button submit = BuildButton("SubmitButton", frame.transform, white, font, "Valider");
        RectTransform submitRect = (RectTransform)submit.transform;
        submitRect.anchorMin = submitRect.anchorMax = submitRect.pivot = new Vector2(0.5f, 0f);
        submitRect.anchoredPosition = new Vector2(0f, 36f);
        submitRect.sizeDelta = new Vector2(300f, 66f);

        var form = canvasGo.AddComponent<AwarenessForm>();
        var so = new SerializedObject(form);
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("window").objectReferenceValue = window;
        so.FindProperty("answerField").objectReferenceValue = field;
        so.FindProperty("submitButton").objectReferenceValue = submit;
        so.ApplyModifiedProperties();

        dim.gameObject.SetActive(false);
        return form;
    }

    private static CanvasGroup BuildThanksScreen(GameObject canvasGo, Sprite white, Font font)
    {
        Image dim = FireballSetupBuilder.CreateUI("ThanksScreen", canvasGo.transform, white, new Color(0.02f, 0.03f, 0.05f, 0.97f));
        FireballSetupBuilder.Stretch(dim.rectTransform, 0f);
        dim.raycastTarget = true;
        var group = dim.gameObject.AddComponent<CanvasGroup>();

        Image frame = FireballSetupBuilder.CreateUI("Window", dim.transform, white, Gold);
        RectTransform window = frame.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1000f, 340f);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, new Color(0.05f, 0.07f, 0.1f, 0.98f));
        FireballSetupBuilder.Stretch(inner.rectTransform, 4f);

        Text title = FireballSetupBuilder.CreateText("Title", frame.transform, font, 54, TextAnchor.MiddleCenter);
        title.text = "Merci pour votre participation !";
        title.color = new Color(1f, 0.85f, 0.45f);
        Place(title.rectTransform, 60f, 80f);

        Text body = FireballSetupBuilder.CreateText("Message", frame.transform, font, 28, TextAnchor.UpperCenter);
        body.text = "La session est terminée. Merci d'avoir pris part à cette étude.\n\nVous pouvez prévenir la personne qui encadre l'expérience.";
        body.fontStyle = FontStyle.Normal;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(body.rectTransform, 170f, 130f, 70f, 70f);

        dim.gameObject.SetActive(false);
        return group;
    }

    internal static Button BuildButton(string name, Transform parent, Sprite white, Font font, string label)
    {
        Image frame = FireballSetupBuilder.CreateUI(name, parent, white, Gold);
        Image inner = FireballSetupBuilder.CreateUI("Background", frame.transform, white, Color.white);
        FireballSetupBuilder.Stretch(inner.rectTransform, 3f);
        inner.raycastTarget = true;

        // Fond sombre qui s'éclaire au survol (teinte appliquée à l'image blanche).
        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = inner;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.13f, 0.18f);
        colors.highlightedColor = new Color(0.25f, 0.22f, 0.14f);
        colors.selectedColor = new Color(0.18f, 0.17f, 0.13f);
        colors.pressedColor = new Color(0.45f, 0.38f, 0.22f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = FireballSetupBuilder.CreateText("Label", frame.transform, font, 30, TextAnchor.MiddleCenter);
        FireballSetupBuilder.Stretch(text.rectTransform, 0f);
        text.text = label;
        return button;
    }

    // Bande horizontale à une distance donnée du haut du parent, avec marges gauche/droite.
    private static void Place(RectTransform rect, float top, float height, float left = 40f, float right = 40f)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
