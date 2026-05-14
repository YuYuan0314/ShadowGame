using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ExposureUI : MonoBehaviour
{
    [Header("引用（留空则自动创建）")]
    public PlayerRbController player;
    public Image fillImage;
    public Image screenFlashImage;

    [Header("进度条颜色")]
    public Color safeColor = new Color(0.2f, 0.85f, 0.25f);
    public Color warningColor = new Color(1f, 0.85f, 0.1f);
    public Color dangerColor = new Color(1f, 0.15f, 0.05f);

    [Header("闪红设置")]
    [Range(0f, 1f)] public float flashStartRatio = 0.6f;
    public float flashMaxAlpha = 0.3f;
    public float flashPulseSpeed = 5f;

    private Canvas canvas;
    private float smoothFill = 1f;
    private RectTransform fillRect;

    void Awake()
    {
        if (player == null)
            player = FindObjectOfType<PlayerRbController>();

        CreateCanvasIfNeeded();
    }

    private void CreateCanvasIfNeeded()
    {
        // 检查是否已有 Canvas
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            var canvasGO = new GameObject("ExposureCanvas");
            canvasGO.transform.SetParent(transform);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // 进度条
        if (fillImage == null)
        {
            var barGO = new GameObject("ExposureBar", typeof(RectTransform));
            barGO.transform.SetParent(canvas.transform, false);
            var barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.02f, 0.90f);
            barRT.anchorMax = new Vector2(0.22f, 0.94f);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = Vector2.zero;

            // 背景
            var bgGO = new GameObject("Background", typeof(Image));
            bgGO.transform.SetParent(barGO.transform, false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 填充：左侧锚定，通过 anchorMax.x 控制宽度（从右向左缩短）
            var fillGO = new GameObject("Fill", typeof(Image));
            fillGO.transform.SetParent(barGO.transform, false);
            fillImage = fillGO.GetComponent<Image>();
            fillImage.raycastTarget = false;
            fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = new Vector2(4, 4);
            fillRect.offsetMax = new Vector2(-4, -4);

            // 标签
            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(barGO.transform, false);
            var label = labelGO.GetComponent<Text>();
            label.text = "";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(6, 0);
            labelRT.offsetMax = new Vector2(0, 0);
        }

        // 屏幕闪红
        if (screenFlashImage == null)
        {
            var flashGO = new GameObject("ScreenFlash", typeof(Image));
            flashGO.transform.SetParent(canvas.transform, false);
            screenFlashImage = flashGO.GetComponent<Image>();
            screenFlashImage.color = new Color(1f, 0f, 0f, 0f);
            screenFlashImage.raycastTarget = false;
            var flashRT = flashGO.GetComponent<RectTransform>();
            flashRT.anchorMin = Vector2.zero;
            flashRT.anchorMax = Vector2.one;
            flashRT.offsetMin = Vector2.zero;
            flashRT.offsetMax = Vector2.zero;
        }
    }

    void Update()
    {
        if (player == null) return;

        float ratio = Mathf.Clamp01(player.currentLightTimer / player.maxLightTime);
        float targetFill = 1f - ratio;
        smoothFill = Mathf.Lerp(smoothFill, targetFill, Time.deltaTime * 10f);

        // 进度条：anchorMax.x 从 1→0，右侧向左消退（向左变短）
        if (fillRect != null && fillImage != null)
        {
            fillRect.anchorMax = new Vector2(smoothFill, fillRect.anchorMax.y);

            if (ratio < 0.5f)
                fillImage.color = Color.Lerp(safeColor, warningColor, ratio * 2f);
            else
                fillImage.color = Color.Lerp(warningColor, dangerColor, (ratio - 0.5f) * 2f);
        }

        // 屏幕闪红
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
