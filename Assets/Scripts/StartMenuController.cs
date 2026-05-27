using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    [Header("Panels")]
    public RectTransform root;
    public RectTransform mainPanel;
    public RectTransform levelPanel;
    public RectTransform settingsPanel;

    [Header("Buttons")]
    public Button startButton;
    public Button settingsButton;
    public Button exitButton;
    public Button levelBackButton;
    public Button settingsBackButton;

    [Header("Transition")]
    public float transitionDuration = 0.45f;
    public AnimationCurve transitionCurve = CreateEaseOutCurve();

    private Coroutine transitionRoutine;

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;

        WireButtons();
        SnapTo(Vector2.zero);
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    private void WireButtons()
    {
        if (startButton != null)
            startButton.onClick.AddListener(ShowLevelSelect);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);
        if (levelBackButton != null)
            levelBackButton.onClick.AddListener(ShowMainFromLevel);
        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(ShowMainFromSettings);
    }

    private void UnwireButtons()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(ShowLevelSelect);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(ShowSettings);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitGame);
        if (levelBackButton != null)
            levelBackButton.onClick.RemoveListener(ShowMainFromLevel);
        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(ShowMainFromSettings);
    }

    public void ShowLevelSelect()
    {
        SlideTo(new Vector2(-GetRootWidth(), 0f));
    }

    public void ShowSettings()
    {
        SlideTo(new Vector2(0f, GetRootHeight()));
    }

    public void ShowMainFromLevel()
    {
        SlideTo(Vector2.zero);
    }

    public void ShowMainFromSettings()
    {
        SlideTo(Vector2.zero);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SlideTo(Vector2 target)
    {
        if (root == null)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(SlideRoutine(target));
    }

    private IEnumerator SlideRoutine(Vector2 target)
    {
        Vector2 start = root.anchoredPosition;
        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = transitionCurve != null ? transitionCurve.Evaluate(t) : t;
            root.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
            yield return null;
        }

        root.anchoredPosition = target;
        transitionRoutine = null;
    }

    private void SnapTo(Vector2 target)
    {
        if (root != null)
            root.anchoredPosition = target;
    }

    private float GetRootWidth()
    {
        return root != null && root.rect.width > 0f ? root.rect.width : 1920f;
    }

    private float GetRootHeight()
    {
        return root != null && root.rect.height > 0f ? root.rect.height : 1080f;
    }

    private static AnimationCurve CreateEaseOutCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(1f, 1f, 0f, 0f));
    }
}
