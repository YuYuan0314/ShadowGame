using UnityEngine;
using UnityEngine.UI;

public class ExposureUI : MonoBehaviour
{
    private const string ExposureCanvasName = "ExposureCanvas";

    [Header("References")]
    public PlayerRbController player;
    public Image fillImage;
    public Image screenFlashImage;

    [Header("Bar Colors")]
    public Color safeColor = new Color(0.2f, 0.85f, 0.25f);
    public Color warningColor = new Color(1f, 0.85f, 0.1f);
    public Color dangerColor = new Color(1f, 0.15f, 0.05f);

    [Header("Screen Flash")]
    [Range(0f, 1f)] public float flashStartRatio = 0.6f;
    public float flashMaxAlpha = 0.3f;
    public float flashPulseSpeed = 5f;

    private Canvas canvas;
    private float smoothFill = 1f;
    private RectTransform fillRect;

    private void Awake()
    {
        if (player == null)
            player = FindObjectOfType<PlayerRbController>();

        CreateCanvasIfNeeded();
    }

    private void OnEnable()
    {
        if (canvas != null)
            canvas.enabled = true;
    }

    private void CreateCanvasIfNeeded()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            GameObject existingCanvas = GameObject.Find(ExposureCanvasName);
            if (existingCanvas != null)
                canvas = existingCanvas.GetComponent<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject(ExposureCanvasName);
            canvas = canvasGO.AddComponent<Canvas>();

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.enabled = true;

        if (fillImage == null)
            CreateExposureBar();
        else if (fillRect == null)
            fillRect = fillImage.GetComponent<RectTransform>();

        if (screenFlashImage == null)
            CreateScreenFlash();
    }

    private void CreateExposureBar()
    {
        GameObject barGO = new GameObject("ExposureBar", typeof(RectTransform));
        barGO.transform.SetParent(canvas.transform, false);
        RectTransform barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0.02f, 0.90f);
        barRT.anchorMax = new Vector2(0.22f, 0.94f);
        barRT.offsetMin = Vector2.zero;
        barRT.offsetMax = Vector2.zero;

        GameObject bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(barGO.transform, false);
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(barGO.transform, false);
        fillImage = fillGO.GetComponent<Image>();
        fillImage.raycastTarget = false;
        fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.offsetMin = new Vector2(4, 4);
        fillRect.offsetMax = new Vector2(-4, -4);

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(barGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(6, 0);
        labelRT.offsetMax = new Vector2(0, 0);
    }

    private void CreateScreenFlash()
    {
        GameObject flashGO = new GameObject("ScreenFlash", typeof(Image));
        flashGO.transform.SetParent(canvas.transform, false);
        screenFlashImage = flashGO.GetComponent<Image>();
        screenFlashImage.color = new Color(1f, 0f, 0f, 0f);
        screenFlashImage.raycastTarget = false;
        RectTransform flashRT = flashGO.GetComponent<RectTransform>();
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.offsetMin = Vector2.zero;
        flashRT.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerRbController>();
            if (player == null)
                return;
        }

        if (canvas != null && !canvas.enabled)
            canvas.enabled = true;

        float ratio = Mathf.Clamp01(player.currentLightTimer / player.maxLightTime);
        float targetFill = 1f - ratio;
        smoothFill = Mathf.Lerp(smoothFill, targetFill, Time.deltaTime * 10f);

        if (fillRect != null && fillImage != null)
        {
            fillRect.anchorMax = new Vector2(smoothFill, fillRect.anchorMax.y);

            if (ratio < 0.5f)
                fillImage.color = Color.Lerp(safeColor, warningColor, ratio * 2f);
            else
                fillImage.color = Color.Lerp(warningColor, dangerColor, (ratio - 0.5f) * 2f);
        }

        if (screenFlashImage != null)
        {
            float targetAlpha = 0f;
            if (ratio >= flashStartRatio)
            {
                float t = (ratio - flashStartRatio) / (1f - flashStartRatio);
                float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * flashPulseSpeed));
                targetAlpha = t * flashMaxAlpha * (0.5f + 0.5f * pulse);
            }

            float a = Mathf.Lerp(screenFlashImage.color.a, targetAlpha, Time.deltaTime * 12f);
            screenFlashImage.color = new Color(1f, 0f, 0f, a);
        }
    }
}
