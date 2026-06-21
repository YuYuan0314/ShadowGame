using UnityEngine;
using UnityEngine.UI;

public class ExposureUI : MonoBehaviour
{
    private const string ExposureCanvasName = "ExposureCanvas";

    [Header("References")]
    public PlayerRbController player;
    public Image backgroundImage;
    public Image fillImage;
    public Image frameImage;
    public Image screenFlashImage;

    [Header("Bar Sprites")]
    public Sprite barFillSprite;
    public Sprite barBackgroundSprite;
    public Sprite barFrameSprite;
    public bool tintFillByExposure;

    [Header("Bar Layout")]
    public Vector2 barAnchor = new Vector2(0f, 1f);
    public Vector2 barPosition = new Vector2(230f, -80f);
    public Vector2 barSize = new Vector2(384f, 48f);
    public Vector2 framePositionOffset = Vector2.zero;
    public Vector2 frameSizeExtra = Vector2.zero;

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
    private RectTransform barRect;
    private RectTransform frameRect;

    private void Awake()
    {
        if (player == null)
            player = FindObjectOfType<PlayerRbController>();

        CreateCanvasIfNeeded();
    }

    private void OnValidate()
    {
        ApplyBarSprites();
        ConfigureFillImage();
        ApplyBarLayout();
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
        else
        {
            ResolveBarRects();
            ConfigureFillImage();
            ApplyBarSprites();
            ApplyBarLayout();
        }

        if (screenFlashImage == null)
            CreateScreenFlash();
    }

    private void CreateExposureBar()
    {
        GameObject barGO = new GameObject("ExposureBar", typeof(RectTransform));
        barGO.transform.SetParent(canvas.transform, false);
        barRect = barGO.GetComponent<RectTransform>();

        GameObject bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(barGO.transform, false);
        backgroundImage = bgGO.GetComponent<Image>();
        backgroundImage.color = barBackgroundSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.6f);
        backgroundImage.raycastTarget = false;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(barGO.transform, false);
        fillImage = fillGO.GetComponent<Image>();
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = new Vector2(4, 4);
        fillRect.offsetMax = new Vector2(-4, -4);

        GameObject frameGO = new GameObject("Frame", typeof(Image));
        frameGO.transform.SetParent(barGO.transform, false);
        frameImage = frameGO.GetComponent<Image>();
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;
        frameRect = frameGO.GetComponent<RectTransform>();

        ConfigureFillImage();
        ApplyBarSprites();
        ApplyBarLayout();
    }

    private void ResolveBarRects()
    {
        if (fillImage != null && barRect == null)
            barRect = fillImage.transform.parent as RectTransform;

        if (frameImage != null && frameRect == null)
            frameRect = frameImage.rectTransform;
    }

    private void ApplyBarLayout()
    {
        ResolveBarRects();

        if (barRect != null)
        {
            Vector2 clampedAnchor = new Vector2(Mathf.Clamp01(barAnchor.x), Mathf.Clamp01(barAnchor.y));
            barRect.anchorMin = clampedAnchor;
            barRect.anchorMax = clampedAnchor;
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = barPosition;
            barRect.sizeDelta = new Vector2(Mathf.Max(1f, barSize.x), Mathf.Max(1f, barSize.y));
        }

        if (frameRect != null)
        {
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = framePositionOffset;
            frameRect.sizeDelta = frameSizeExtra;
        }
    }

    private void ConfigureFillImage()
    {
        if (fillImage == null)
            return;

        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;
        fillImage.preserveAspect = false;
    }

    private void ApplyBarSprites()
    {
        if (backgroundImage != null)
        {
            backgroundImage.sprite = barBackgroundSprite;
            backgroundImage.color = barBackgroundSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.6f);
        }

        if (fillImage != null)
            fillImage.sprite = barFillSprite;

        if (frameImage != null)
        {
            frameImage.sprite = barFrameSprite;
            frameImage.enabled = barFrameSprite != null;
        }
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

        if (fillImage != null)
        {
            fillImage.fillAmount = smoothFill;

            if (!tintFillByExposure)
                fillImage.color = Color.white;
            else if (ratio < 0.5f)
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
