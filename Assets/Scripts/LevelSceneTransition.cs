using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSceneTransition : MonoBehaviour
{
    private const string ShaderName = "Hidden/UI/CircleTransitionMask";

    public float focusCloseDuration = 0.22f;
    public float focusHoldDuration = 0.18f;
    public float fullCoverDuration = 0.14f;
    public float postLoadHoldDuration = 0.08f;
    public float revealSightDuration = 0.42f;
    public float revealFullDuration = 0.18f;
    public float focusPadding = 1.12f;
    public float fallbackFocusRadius = 0.18f;
    public float revealStartRadius = 0.035f;
    public float revealSightRadius = 0.23f;
    public float maskSoftness = 0.012f;

    public static bool IsTransitioning { get; private set; }

    private static LevelSceneTransition instance;

    private Canvas canvas;
    private Image maskImage;
    private Material maskMaterial;
    private readonly Vector3[] corners = new Vector3[4];

    public static bool LoadScene(string sceneName, RectTransform focusTarget)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || IsTransitioning)
            return false;

        LevelSceneTransition transition = GetOrCreate();
        transition.StartCoroutine(transition.LoadSceneRoutine(sceneName, focusTarget));
        return true;
    }

    private static LevelSceneTransition GetOrCreate()
    {
        if (instance != null)
            return instance;

        GameObject go = new GameObject("LevelSceneTransition");
        instance = go.AddComponent<LevelSceneTransition>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        HideOverlay();
    }

    private IEnumerator LoadSceneRoutine(string sceneName, RectTransform focusTarget)
    {
        IsTransitioning = true;
        EnsureOverlay();
        ShowOverlay();

        Vector2 focusCenter = Vector2.one * 0.5f;
        float focusRadius = fallbackFocusRadius;
        TryGetFocusCircle(focusTarget, out focusCenter, out focusRadius);

        float maxRadius = GetRadiusToCoverScreen(focusCenter);
        SetMask(focusCenter, maxRadius);
        yield return AnimateRadius(focusCenter, maxRadius, focusRadius, focusCloseDuration, true);
        yield return WaitUnscaled(focusHoldDuration);
        yield return AnimateRadius(focusCenter, focusRadius, 0f, fullCoverDuration, true);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogError("Unable to load scene: " + sceneName, this);
            HideOverlay();
            IsTransitioning = false;
            yield break;
        }

        while (!load.isDone)
            yield return null;

        yield return null;
        yield return WaitUnscaled(postLoadHoldDuration);

        Vector2 revealCenter = FindPlayerScreenCenter();
        SetMask(revealCenter, revealStartRadius);
        yield return AnimateRadius(revealCenter, revealStartRadius, revealSightRadius, revealSightDuration, false);
        yield return AnimateRadius(revealCenter, revealSightRadius, GetRadiusToCoverScreen(revealCenter), revealFullDuration, false);

        HideOverlay();
        IsTransitioning = false;
    }

    private void EnsureOverlay()
    {
        if (canvas != null && maskImage != null)
            return;

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.enabled = false;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        GameObject mask = new GameObject("CircleMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mask.transform.SetParent(transform, false);

        RectTransform rect = mask.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        maskImage = mask.GetComponent<Image>();
        maskImage.color = Color.black;
        maskImage.raycastTarget = true;

        Shader shader = Shader.Find(ShaderName);
        if (shader != null)
        {
            maskMaterial = new Material(shader);
            maskMaterial.hideFlags = HideFlags.HideAndDontSave;
            maskImage.material = maskMaterial;
        }
    }

    private void ShowOverlay()
    {
        if (canvas != null)
            canvas.enabled = true;
        if (maskImage != null)
            maskImage.enabled = true;
    }

    private void HideOverlay()
    {
        if (maskImage != null)
            maskImage.enabled = false;
        if (canvas != null)
            canvas.enabled = false;
    }

    private void SetMask(Vector2 center, float radius)
    {
        if (maskMaterial == null)
        {
            if (maskImage != null)
                maskImage.color = radius <= 0.001f ? Color.black : new Color(0f, 0f, 0f, 0f);
            return;
        }

        maskMaterial.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
        maskMaterial.SetFloat("_Radius", Mathf.Max(0f, radius));
        maskMaterial.SetFloat("_Softness", Mathf.Max(0.0001f, maskSoftness));
        maskMaterial.SetColor("_Color", Color.black);
    }

    private IEnumerator AnimateRadius(Vector2 center, float from, float to, float duration, bool easeIn)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = easeIn ? t * t : 1f - Mathf.Pow(1f - t, 3f);
            SetMask(center, Mathf.LerpUnclamped(from, to, t));
            yield return null;
        }

        SetMask(center, to);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float endTime = Time.unscaledTime + Mathf.Max(0f, duration);
        while (Time.unscaledTime < endTime)
            yield return null;
    }

    private bool TryGetFocusCircle(RectTransform focusTarget, out Vector2 center, out float radius)
    {
        center = Vector2.one * 0.5f;
        radius = fallbackFocusRadius;

        if (focusTarget == null || Screen.width <= 0 || Screen.height <= 0)
            return false;

        Canvas sourceCanvas = focusTarget.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera
            : null;

        focusTarget.GetWorldCorners(corners);
        Vector2 centerPixels = Vector2.zero;
        for (int i = 0; i < corners.Length; i++)
            centerPixels += RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[i]);
        centerPixels /= corners.Length;

        float maxPixelDistance = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 cornerPixels = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[i]);
            maxPixelDistance = Mathf.Max(maxPixelDistance, Vector2.Distance(centerPixels, cornerPixels));
        }

        center = new Vector2(centerPixels.x / Screen.width, centerPixels.y / Screen.height);
        center.x = Mathf.Clamp01(center.x);
        center.y = Mathf.Clamp01(center.y);
        radius = Mathf.Max(0.01f, maxPixelDistance / Screen.height * focusPadding);
        return true;
    }

    private Vector2 FindPlayerScreenCenter()
    {
        Camera cam = Camera.main;
        if (cam == null || Screen.width <= 0 || Screen.height <= 0)
            return Vector2.one * 0.5f;

        Transform target = null;
        PlayerRbController player = FindObjectOfType<PlayerRbController>();
        if (player != null)
            target = player.transform;

        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                target = playerObject.transform;
        }

        if (target == null)
            return Vector2.one * 0.5f;

        Vector3 screen = cam.WorldToScreenPoint(target.position + Vector3.up * 0.45f);
        if (screen.z < 0f)
            return Vector2.one * 0.5f;

        return new Vector2(
            Mathf.Clamp01(screen.x / Screen.width),
            Mathf.Clamp01(screen.y / Screen.height));
    }

    private float GetRadiusToCoverScreen(Vector2 center)
    {
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;
        Vector2 a = new Vector2((0f - center.x) * aspect, 0f - center.y);
        Vector2 b = new Vector2((1f - center.x) * aspect, 0f - center.y);
        Vector2 c = new Vector2((0f - center.x) * aspect, 1f - center.y);
        Vector2 d = new Vector2((1f - center.x) * aspect, 1f - center.y);
        return Mathf.Max(a.magnitude, b.magnitude, c.magnitude, d.magnitude) + 0.05f;
    }

    private void OnDestroy()
    {
        if (maskMaterial != null)
            Destroy(maskMaterial);

        if (instance == this)
            instance = null;
    }
}
