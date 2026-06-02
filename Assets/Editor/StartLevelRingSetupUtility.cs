using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StartLevelRingSetupUtility
{
    private const string ScenePath = "Assets/Scenes/Start.unity";
    private const string AutoRunKey = "Codex.StartLevelRingSetup.v1";
    private const string CircleSpritePath = "Assets/UI/Generated/LevelRingCircle.png";

    static StartLevelRingSetupUtility()
    {
        EditorApplication.delayCall += AutoConfigureOnce;
    }

    [MenuItem("Tools/Configure Start Level Ring")]
    public static void ConfigureStartLevelRing()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        bool openedStart = previousScene.path != ScenePath;
        if (openedStart)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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

        Transform menuRoot = FindDirectChild(canvas.transform, "MenuRoot");
        if (menuRoot == null)
        {
            GameObject menuRootGo = new GameObject("MenuRoot", typeof(RectTransform));
            menuRootGo.transform.SetParent(canvas.transform, false);
            menuRoot = menuRootGo.transform;
            Stretch(menuRoot.GetComponent<RectTransform>());
        }

        Transform levelPanel = FindDirectChild(menuRoot, "LevelSelectPanel");
        if (levelPanel == null)
        {
            GameObject levelPanelGo = new GameObject("LevelSelectPanel", typeof(RectTransform));
            levelPanelGo.transform.SetParent(menuRoot, false);
            levelPanel = levelPanelGo.transform;
            RectTransform panelRt = levelPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1920f, 1080f);
            panelRt.anchoredPosition = new Vector2(1920f, 0f);
        }

        EnsureLevelBackground(levelPanel);

        Transform oldRing = FindDirectChild(levelPanel, "RingLevelSelect");
        if (oldRing != null)
            Object.DestroyImmediate(oldRing.gameObject);

        Sprite circleSprite = EnsureCircleSprite();
        Sprite previewOne = LoadSprite("Assets/UI/选关.png");
        Sprite previewTwo = LoadSprite("Assets/UI/开始.png");
        Sprite previewThree = LoadSprite("Assets/UI/设定.png");

        GameObject ringGo = new GameObject("RingLevelSelect", typeof(RectTransform));
        ringGo.transform.SetParent(levelPanel, false);
        RectTransform ringRt = ringGo.GetComponent<RectTransform>();
        ringRt.anchorMin = new Vector2(0.5f, 0.5f);
        ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.sizeDelta = new Vector2(1600f, 760f);
        ringRt.anchoredPosition = new Vector2(0f, -40f);

        CreateGlowRail(ringGo.transform);
        Text title = CreateText(ringGo.transform, "LevelRingTitle", "LEVEL SELECT", new Vector2(0f, 345f), new Vector2(560f, 72f), 48, Color.white);
        title.fontStyle = FontStyle.Bold;

        Text selectedTitle = CreateText(ringGo.transform, "SelectedLevelTitle", "第一关", new Vector2(0f, -310f), new Vector2(720f, 70f), 44, Color.white);
        selectedTitle.fontStyle = FontStyle.Bold;
        Text selectedScene = CreateText(ringGo.transform, "SelectedLevelScene", "第一关", new Vector2(0f, -365f), new Vector2(720f, 46f), 24, new Color(0.72f, 0.95f, 1f, 0.82f));
        CreateText(ringGo.transform, "LevelRingHint", "← / →  切换    Enter / A  确认", new Vector2(0f, -420f), new Vector2(780f, 44f), 24, new Color(1f, 1f, 1f, 0.68f));

        LevelRingCarousel carousel = ringGo.AddComponent<LevelRingCarousel>();
        carousel.items.Clear();
        carousel.selectedTitleText = selectedTitle;
        carousel.selectedSceneText = selectedScene;
        carousel.center = new Vector2(0f, 50f);
        carousel.radiusX = 560f;
        carousel.radiusY = 125f;
        carousel.angleStep = 38f;
        carousel.selectedScale = 1.24f;
        carousel.sideScale = 0.74f;
        carousel.farScale = 0.52f;
        carousel.selectedYBoost = 48f;
        carousel.sideAlpha = 0.72f;
        carousel.farAlpha = 0.36f;
        carousel.previousKey = KeyCode.LeftArrow;
        carousel.previousAltKey = KeyCode.A;
        carousel.nextKey = KeyCode.RightArrow;
        carousel.nextAltKey = KeyCode.D;
        carousel.submitKey = KeyCode.Return;
        carousel.submitAltKey = KeyCode.Space;
        carousel.gamepadSubmitButton = KeyCode.JoystickButton0;
        carousel.horizontalAxis = "Horizontal";
        carousel.submitButton = "Submit";

        AddLevelCard(carousel, ringGo.transform, circleSprite, previewOne, "第一关", "FIRST SHADOW", "第一关");
        AddLevelCard(carousel, ringGo.transform, circleSprite, previewTwo, "第二关", "COMING SOON", "");
        AddLevelCard(carousel, ringGo.transform, circleSprite, previewThree, "第三关", "COMING SOON", "");
        carousel.selectedIndex = 0;
        carousel.ApplyLayoutImmediate();

        EnsureBackButtonOnTop(levelPanel);

        EditorUtility.SetDirty(ringGo);
        EditorUtility.SetDirty(levelPanel.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void AutoConfigureOnce()
    {
        if (EditorPrefs.GetBool(AutoRunKey, false))
            return;

        if (!File.Exists(ScenePath))
            return;

        EditorPrefs.SetBool(AutoRunKey, true);
        ConfigureStartLevelRing();
    }

    private static void AddLevelCard(LevelRingCarousel carousel, Transform parent, Sprite circleSprite, Sprite previewSprite, string title, string subtitle, string sceneName)
    {
        GameObject cardGo = new GameObject("LevelCard_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardGo.transform.SetParent(parent, false);
        RectTransform cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(360f, 360f);

        Image ring = cardGo.GetComponent<Image>();
        ring.sprite = circleSprite;
        ring.color = new Color(0.02f, 0.02f, 0.035f, 0.96f);
        ring.raycastTarget = true;

        Button button = cardGo.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 0.92f, 0.35f, 1f),
            pressedColor = new Color(0.65f, 1f, 0.95f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(1f, 1f, 1f, 0.35f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };

        GameObject maskGo = new GameObject("PreviewMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGo.transform.SetParent(cardGo.transform, false);
        RectTransform maskRt = maskGo.GetComponent<RectTransform>();
        Stretch(maskRt);
        maskRt.offsetMin = new Vector2(24f, 24f);
        maskRt.offsetMax = new Vector2(-24f, -24f);
        Image maskImage = maskGo.GetComponent<Image>();
        maskImage.sprite = circleSprite;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        Mask mask = maskGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject previewGo = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        previewGo.transform.SetParent(maskGo.transform, false);
        RectTransform previewRt = previewGo.GetComponent<RectTransform>();
        Stretch(previewRt);
        Image preview = previewGo.GetComponent<Image>();
        preview.sprite = previewSprite;
        preview.color = Color.white;
        preview.preserveAspect = false;
        preview.raycastTarget = false;

        GameObject shineGo = new GameObject("GlassShine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shineGo.transform.SetParent(cardGo.transform, false);
        RectTransform shineRt = shineGo.GetComponent<RectTransform>();
        shineRt.anchorMin = new Vector2(0.5f, 0.5f);
        shineRt.anchorMax = new Vector2(0.5f, 0.5f);
        shineRt.pivot = new Vector2(0.5f, 0.5f);
        shineRt.sizeDelta = new Vector2(280f, 54f);
        shineRt.anchoredPosition = new Vector2(-16f, 78f);
        shineRt.localRotation = Quaternion.Euler(0f, 0f, -17f);
        Image shine = shineGo.GetComponent<Image>();
        shine.color = new Color(1f, 1f, 1f, 0.24f);
        shine.raycastTarget = false;

        GameObject labelGo = new GameObject("LabelBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        labelGo.transform.SetParent(cardGo.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(300f, 64f);
        labelRt.anchoredPosition = new Vector2(0f, -116f);
        Image labelImage = labelGo.GetComponent<Image>();
        labelImage.color = new Color(0.18f, 0.12f, 0.45f, 0.92f);
        labelImage.raycastTarget = false;

        Text titleText = CreateText(labelGo.transform, "Title", title, Vector2.zero, new Vector2(290f, 38f), 28, Color.white);
        titleText.fontStyle = FontStyle.Bold;
        Text subtitleText = CreateText(labelGo.transform, "Subtitle", subtitle, new Vector2(0f, -25f), new Vector2(290f, 24f), 16, new Color(0.72f, 0.95f, 1f, 0.82f));

        LevelRingCarousel.LevelItem item = new LevelRingCarousel.LevelItem
        {
            card = cardRt,
            button = button,
            previewImage = preview,
            titleText = titleText,
            subtitleText = subtitleText,
            levelName = title,
            sceneName = sceneName
        };
        carousel.items.Add(item);
    }

    private static void CreateGlowRail(Transform parent)
    {
        GameObject railGo = new GameObject("NeonArcRail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        railGo.transform.SetParent(parent, false);
        RectTransform rt = railGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1280f, 24f);
        rt.anchoredPosition = new Vector2(0f, -45f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 9f);
        Image image = railGo.GetComponent<Image>();
        image.color = new Color(0.1f, 1f, 0.24f, 0.72f);
        image.raycastTarget = false;
        railGo.transform.SetAsFirstSibling();
    }

    private static void EnsureLevelBackground(Transform levelPanel)
    {
        Sprite bg = LoadSprite("Assets/UI/选关.png");
        Transform existing = FindDirectChild(levelPanel, "LevelRingBackground");
        if (existing == null)
        {
            GameObject bgGo = new GameObject("LevelRingBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(levelPanel, false);
            existing = bgGo.transform;
        }

        Stretch(existing.GetComponent<RectTransform>());
        Image image = existing.GetComponent<Image>();
        image.sprite = bg;
        image.color = new Color(1f, 1f, 1f, 0.92f);
        image.preserveAspect = false;
        image.raycastTarget = false;
        existing.SetAsFirstSibling();
    }

    private static void EnsureBackButtonOnTop(Transform levelPanel)
    {
        for (int i = 0; i < levelPanel.childCount; i++)
        {
            Transform child = levelPanel.GetChild(i);
            if (child.name.Contains("返回") || child.name.ToLowerInvariant().Contains("back"))
                child.SetAsLastSibling();
        }
    }

    private static Text CreateText(Transform parent, string name, string textValue, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = textValue;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite EnsureCircleSprite()
    {
        string fullPath = Path.Combine(Application.dataPath, "UI/Generated/LevelRingCircle.png");
        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(fullPath))
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CircleSpritePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
}
