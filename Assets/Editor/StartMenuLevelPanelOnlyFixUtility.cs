using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StartMenuLevelPanelOnlyFixUtility
{
    private const string ScenePath = "Assets/Scenes/Start.unity";
    private const string AutoRunKey = "Codex.StartMenuLevelPanelOnlyFix.v2";

    static StartMenuLevelPanelOnlyFixUtility()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    [MenuItem("Tools/Fix Start Menu Level Panel Only")]
    public static void Fix()
    {
        bool openedStart = SceneManager.GetActiveScene().path != ScenePath;
        if (openedStart)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Transform menuRoot = FindDirectChild(canvas.transform, "MenuRoot");
        if (menuRoot == null)
            return;

        Transform mainPanel = FindDirectChild(menuRoot, "MainPanel");
        Transform levelPanel = FindDirectChild(menuRoot, "LevelSelectPanel");
        StartMenuController controller = menuRoot.GetComponent<StartMenuController>();

        if (mainPanel != null)
        {
            RemoveComponent<RotatingTabCarousel>(mainPanel.gameObject);
            RemoveWrongRingFrom(mainPanel);
            RestoreMainButton(controller != null ? controller.startButton : null, mainPanel, new Vector2(600f, -120f));
            RestoreMainButton(controller != null ? controller.settingsButton : null, mainPanel, new Vector2(640f, -270f));
            RestoreMainButton(controller != null ? controller.exitButton : null, mainPanel, new Vector2(680f, -420f));
        }

        if (levelPanel != null)
        {
            EnsureRingLivesUnderLevelPanel(levelPanel);
            Transform ring = FindDirectChild(levelPanel, "RingLevelSelect");
            if (ring != null)
            {
                LevelRingCarousel carousel = ring.GetComponent<LevelRingCarousel>();
                if (carousel != null)
                {
                    carousel.ApplyLayoutImmediate();
                    EditorUtility.SetDirty(carousel);
                }
                ring.SetAsLastSibling();
            }

            if (controller != null && controller.levelBackButton != null)
                controller.levelBackButton.transform.SetAsLastSibling();
        }

        if (mainPanel != null)
            EditorUtility.SetDirty(mainPanel.gameObject);
        if (levelPanel != null)
            EditorUtility.SetDirty(levelPanel.gameObject);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(AutoRunKey, false))
            return;

        EditorPrefs.SetBool(AutoRunKey, true);
        Fix();
    }

    private static void RestoreMainButton(Button button, Transform mainPanel, Vector2 position)
    {
        if (button == null || mainPanel == null)
            return;

        button.transform.SetParent(mainPanel, false);
        RemoveComponent<RotatingTabCard>(button.gameObject);
        RemoveComponent<LevelRingCard>(button.gameObject);

        RectTransform rt = button.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;
        button.transform.SetAsLastSibling();
        EditorUtility.SetDirty(button.gameObject);
    }

    private static void EnsureRingLivesUnderLevelPanel(Transform levelPanel)
    {
        LevelRingCarousel[] carousels = Object.FindObjectsOfType<LevelRingCarousel>(true);
        for (int i = 0; i < carousels.Length; i++)
        {
            if (carousels[i] == null)
                continue;

            Transform ring = carousels[i].transform;
            if (ring.name == "RingLevelSelect" && ring.parent != levelPanel)
                ring.SetParent(levelPanel, false);
        }
    }

    private static void RemoveWrongRingFrom(Transform parent)
    {
        Transform ring = FindDirectChild(parent, "RingLevelSelect");
        if (ring != null)
            Object.DestroyImmediate(ring.gameObject);

        LevelRingCarousel carousel = parent.GetComponent<LevelRingCarousel>();
        if (carousel != null)
            Object.DestroyImmediate(carousel);
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        if (go == null)
            return;

        T component = go.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component);
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;
        }

        return null;
    }
}
