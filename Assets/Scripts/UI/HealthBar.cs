using UnityEngine;
using UnityEngine.UI;

// Barre de vie flottante au-dessus d'une entité : UI écran qui suit sa position (taille constante à l'écran).
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    // Hauteur de la barre au-dessus du pivot de l'entité.
    [SerializeField] private float heightOffset = 1.4f;
    [SerializeField] private Vector2 size = new Vector2(90f, 11f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color backColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
    // Portion perdue récemment, qui se résorbe après un court délai.
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.6f);
    [SerializeField] private float trailDelay = 0.4f;
    [SerializeField] private float trailSpeed = 1.2f;

    private static Canvas sharedCanvas;

    private Camera viewCamera;
    private RectTransform root;
    private RectTransform fill;
    private RectTransform trail;
    private float trailRatio = 1f;
    private float trailTimer;
    private float lastRatio = 1f;

    public void SetColor(Color color)
    {
        fillColor = color;
        if (fill != null)
            fill.GetComponent<Image>().color = color;
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    private void Start()
    {
        // La caméra de jeu n'a pas forcément le tag MainCamera.
        viewCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        Build();
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root.gameObject);
    }

    private void LateUpdate()
    {
        if (root == null || health == null || viewCamera == null)
            return;

        Vector3 screen = viewCamera.WorldToScreenPoint(transform.position + Vector3.up * heightOffset);
        bool visible = screen.z > 0f && !health.IsDead;
        root.gameObject.SetActive(visible);
        if (!visible)
            return;

        var canvasRect = (RectTransform)sharedCanvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        root.anchoredPosition = local;

        float ratio = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
        if (ratio < lastRatio)
            trailTimer = trailDelay;
        lastRatio = ratio;

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
            trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime);
        trailRatio = Mathf.Max(trailRatio, ratio);

        fill.anchorMax = new Vector2(ratio, 1f);
        trail.anchorMax = new Vector2(trailRatio, 1f);
    }

    private void Build()
    {
        if (sharedCanvas == null)
            sharedCanvas = CreateCanvas();

        root = CreateRect("HealthBar_" + name, sharedCanvas.transform, backColor);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = size;

        // Fines bordures intérieures pour détacher la barre du décor.
        const float inset = 2f;
        trail = CreateRect("Trail", root, trailColor);
        Fill(trail, inset);
        fill = CreateRect("Fill", root, fillColor);
        Fill(fill, inset);
    }

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("HealthBars", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Sous la barre de compétences.
        canvas.sortingOrder = -1;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform CreateRect(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    // Remplit le parent avec une marge ; la largeur se règle ensuite via anchorMax.x.
    private static void Fill(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
