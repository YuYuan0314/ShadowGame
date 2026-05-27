using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartMenuSetupUtility
{
    [MenuItem("Tools/Configure Start Menu Panels")]
    public static void ConfigureStartScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Start.unity");

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject menuRoot = FindDirectChild(canvas.transform, "MenuRoot");
        if (menuRoot == null)
        {
            menuRoot = new GameObject("MenuRoot", typeof(RectTransform));
            menuRoot.transform.SetParent(canvas.transform, false);
        }

        Stretch(menuRoot.GetComponent<RectTransform>());

        GameObject mainPanel = GetOrCreatePanel(menuRoot.transform, "MainPanel", Vector2.zero);
        GameObject levelPanel = GetOrCreatePanel(menuRoot.transform, "LevelSelectPanel", new Vector2(1920f, 0f));
        GameObject settingsPanel = GetOrCreatePanel(menuRoot.transform, "SettingsPanel", new Vector2(0f, -1080f));

        Sprite startBg = LoadSprite("Assets/UI/开始页面底.png");
        Sprite levelBg = LoadSprite("Assets/UI/选关.png");
        MoveLegacyCanvasBackgrounds(canvas.transform, menuRoot.transform, mainPanel.transform);
        CreateImageChild(mainPanel.transform, "背景", startBg);
        CreateImageChild(levelPanel.transform, "选关背景", levelBg != null ? levelBg : startBg);
        CreateImageChild(settingsPanel.transform, "设置背景", startBg);
        RemoveDuplicateNamedChildren(mainPanel.transform, "背景");

        MoveExistingToPanel("开始游戏", mainPanel.transform);
        MoveExistingToPanel("设定", mainPanel.transform);
        MoveExistingToPanel("退出", mainPanel.transform);

        Button startButton = GetButton("开始游戏");
        Button settingsButton = GetButton("设定");
        Button exitButton = GetButton("退出");
        SetButtonOnTop(startButton);
        SetButtonOnTop(settingsButton);
        SetButtonOnTop(exitButton);
        Button levelBackButton = CreateTextButton(levelPanel.transform, "选关返回", "返回", new Vector2(-820f, 430f));
        Button settingsBackButton = CreateTextButton(settingsPanel.transform, "设置返回", "返回", new Vector2(-820f, 430f));
        EnsureHoverEffect(startButton);
        EnsureHoverEffect(settingsButton);
        EnsureHoverEffect(exitButton);
        EnsureHoverEffect(levelBackButton);
        EnsureHoverEffect(settingsBackButton);
        EnsureRotatingCardMenu(mainPanel.transform, startButton, settingsButton, exitButton);

        CreateTitle(settingsPanel.transform, "设置标题", "设置", new Vector2(0f, 260f));

        StartMenuController controller = menuRoot.GetComponent<StartMenuController>();
        if (controller == null)
            controller = menuRoot.AddComponent<StartMenuController>();

        controller.root = menuRoot.GetComponent<RectTransform>();
        controller.mainPanel = mainPanel.GetComponent<RectTransform>();
        controller.levelPanel = levelPanel.GetComponent<RectTransform>();
        controller.settingsPanel = settingsPanel.GetComponent<RectTransform>();
        controller.startButton = startButton;
        controller.settingsButton = settingsButton;
        controller.exitButton = exitButton;
        controller.levelBackButton = levelBackButton;
        controller.settingsBackButton = settingsBackButton;
        controller.transitionDuration = 0.45f;
        controller.transitionCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(1f, 1f, 0f, 0f));

        ClearPersistentClicks(startButton);
        ClearPersistentClicks(settingsButton);
        ClearPersistentClicks(exitButton);
        ClearPersistentClicks(levelBackButton);
        ClearPersistentClicks(settingsBackButton);

        EditorUtility.SetDirty(canvas.gameObject);
        EditorUtility.SetDirty(menuRoot);
        menuRoot.transform.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child.gameObject;
        }

        return null;
    }

    private static GameObject GetOrCreatePanel(Transform parent, string name, Vector2 anchoredPosition)
    {
        GameObject go = FindDirectChild(parent, name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1920f, 1080f);
        rt.anchoredPosition = anchoredPosition;
        rt.localScale = Vector3.one;
        return go;
    }

    private static GameObject CreateImageChild(Transform parent, string name, Sprite sprite)
    {
        GameObject go = FindDirectChild(parent, name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        Stretch(go.GetComponent<RectTransform>());

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;

        go.transform.SetAsFirstSibling();
        return go;
    }

    private static void MoveExistingToPanel(string objectName, Transform panel)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null && existing.transform.parent != panel)
            existing.transform.SetParent(panel, false);
    }

    private static void MoveLegacyCanvasBackgrounds(Transform canvas, Transform menuRoot, Transform mainPanel)
    {
        for (int i = canvas.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.GetChild(i);
            if (child == menuRoot)
                continue;

            if (child.name == "背景")
            {
                child.SetParent(mainPanel, false);
                child.SetAsFirstSibling();
            }
        }
    }

    private static void RemoveDuplicateNamedChildren(Transform parent, string childName)
    {
        bool keptOne = false;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name != childName)
                continue;

            if (!keptOne)
            {
                keptOne = true;
                child.SetAsFirstSibling();
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void SetButtonOnTop(Button button)
    {
        if (button != null)
            button.transform.SetAsLastSibling();
    }

    private static void EnsureHoverEffect(Button button)
    {
        if (button == null)
            return;

        ButtonHoverDottedOutline effect = button.GetComponent<ButtonHoverDottedOutline>();
        if (effect == null)
            effect = button.gameObject.AddComponent<ButtonHoverDottedOutline>();

        effect.outlineColor = Color.white;
        effect.padding = 16f;
        effect.dotCount = 96;
        effect.dotSize = new Vector2(9f, 4f);
        effect.cornerRadius = 22f;
        effect.ringThickness = 4f;
        effect.glowThickness = 18f;
        effect.highlightWidth = 0.2f;
        effect.hiddenScale = 0.84f;
        effect.growSpeed = 12f;
        effect.flowSpeed = 0.65f;
    }

    private static void EnsureRotatingCardMenu(Transform mainPanel, Button startButton, Button settingsButton, Button exitButton)
    {
        if (mainPanel == null)
            return;

        RotatingTabCarousel carousel = mainPanel.GetComponent<RotatingTabCarousel>();
        if (carousel == null)
            carousel = mainPanel.gameObject.AddComponent<RotatingTabCarousel>();

        carousel.cards.Clear();
        AddCarouselCard(carousel, startButton);
        AddCarouselCard(carousel, settingsButton);
        AddCarouselCard(carousel, exitButton);

        carousel.sideCenter = new Vector2(685f, -165f);
        carousel.sideSpacing = new Vector2(34f, -150f);
        carousel.sideRotationStep = -10f;
        carousel.sideScale = 0.9f;
        carousel.focusedPosition = new Vector2(600f, -165f);
        carousel.focusedScale = 1.22f;
        carousel.focusedRotationZ = 0f;
        carousel.moveSpeed = 12f;
        carousel.rotateSpeed = 12f;
        carousel.scaleSpeed = 12f;
        carousel.selectFirstOnStart = false;
        carousel.pointerRetractDelay = 0.22f;
        carousel.pointerSettlePositionTolerance = 3f;
        carousel.pointerSettleScaleTolerance = 0.03f;
        carousel.pointerSettleRotationTolerance = 2f;
        carousel.requireSettledBeforeNavigation = true;
        carousel.allowKeyboardInput = true;
        carousel.allowGamepadInput = true;
        carousel.ApplyLayoutImmediate();
    }

    private static void AddCarouselCard(RotatingTabCarousel carousel, Button button)
    {
        if (carousel == null || button == null)
            return;

        RectTransform card = button.transform as RectTransform;
        if (card != null && !carousel.cards.Contains(card))
            carousel.cards.Add(card);
    }

    private static Button GetButton(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static Button CreateTextButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject go = FindDirectChild(parent, name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());

            Text text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 32;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 64f);
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.2f, 0.36f, 0.88f);

        return go.GetComponent<Button>();
    }

    private static void CreateTitle(Transform parent, string name, string label, Vector2 position)
    {
        GameObject go = FindDirectChild(parent, name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 100f);
        rt.anchoredPosition = position;

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = 56;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static void ClearPersistentClicks(Button button)
    {
        if (button == null)
            return;

        SerializedObject serializedObject = new SerializedObject(button);
        SerializedProperty calls = serializedObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls != null)
        {
            calls.ClearArray();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
