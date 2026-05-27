using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    private enum PauseState
    {
        Gameplay,
        Opening,
        Paused,
        Closing
    }

    private struct CameraPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
    }

    [Header("Scene References")]
    public Transform player;
    public Camera controlledCamera;
    public CameraOrbit cameraOrbit;
    public Canvas worldCanvas;
    public PauseRadialTabMenu tabMenu;
    public Button continueButton;
    public Button musicButton;
    public Button exitButton;
    public GameObject musicPanel;
    public Slider musicSlider;
    public Text musicValueText;

    [Header("Input")]
    public KeyCode pauseKey = KeyCode.Escape;
    public KeyCode gamepadPauseButton = KeyCode.JoystickButton7;
    public KeyCode gamepadPauseAltButton = KeyCode.JoystickButton9;
    public KeyCode backKey = KeyCode.JoystickButton1;
    public string cancelButton = "Cancel";
    public string horizontalAxis = "Horizontal";
    public float horizontalAxisThreshold = 0.45f;
    public float musicStep = 0.05f;

    [Header("Camera Move")]
    public float cameraTransitionDuration = 0.72f;
    public AnimationCurve cameraTransitionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2.4f, 2.4f),
        new Keyframe(1f, 1f, 0f, 0f));
    public float faceHeight = 0.32f;
    public float faceForwardDistance = 1.65f;
    public float faceSideOffset = 0.58f;
    public float faceVerticalOffset = -0.04f;
    public float pauseFieldOfView = 40f;
    [Range(0f, 1f)] public float menuShowAtTransition = 0.62f;

    [Header("World Menu Pose")]
    public bool useScreenSpaceMenu = true;
    public Vector2 screenMenuAnchor = new Vector2(0.72f, 0.52f);
    public Vector2 screenMenuOffset = Vector2.zero;
    public Vector2 screenMenuPadding = new Vector2(230f, 190f);
    public float screenMenuFollowSpeed = 18f;
    public float menuCanvasScale = 0.0048f;
    public float menuScreenRightOffset = 1.15f;
    public float menuScreenUpOffset = 0.08f;
    public float menuForwardOffset = -0.05f;
    public float menuPoseFollowSpeed = 18f;

    [Header("Exit")]
    public string exitSceneName = "Start";
    public bool quitApplicationIfSceneMissing = true;

    private PauseState state = PauseState.Gameplay;
    private CameraPose gameplayPose;
    private CameraPose transitionFromPose;
    private CameraPose transitionToPose;
    private float transitionTimer;
    private bool menuShownThisPause;
    private bool cursorWasVisible;
    private CursorLockMode previousLockMode;
    private bool cameraOrbitWasEnabled;
    private readonly List<MonoBehaviour> disabledPlayerBehaviours = new List<MonoBehaviour>();
    private int horizontalAxisDirection;
    private bool cancelButtonAvailable = true;

    private void Awake()
    {
        ResolveReferences();
        WireButtons();
        HideMenuImmediate();
    }

    private void OnEnable()
    {
        WireButtons();
    }

    private void Update()
    {
        ResolveReferences();

        if (ReadPauseInputDown())
        {
            if (state == PauseState.Gameplay)
                BeginPause();
            else if (musicPanel != null && musicPanel.activeSelf)
                CloseMusicPanel();
            else if (state == PauseState.Paused)
                ContinueGame();
        }

        if (state == PauseState.Opening || state == PauseState.Paused || state == PauseState.Closing)
        {
            UpdateMenuCanvasPose(false);
            HandleMusicPanelInput();
        }

        if (state == PauseState.Opening || state == PauseState.Closing)
            UpdateCameraTransition();
    }

    public void BeginPause()
    {
        if (state != PauseState.Gameplay || controlledCamera == null || player == null)
            return;

        gameplayPose = CaptureCameraPose();
        transitionFromPose = gameplayPose;
        transitionToPose = BuildPauseCameraPose();
        transitionTimer = 0f;
        menuShownThisPause = false;
        state = PauseState.Opening;

        cursorWasVisible = Cursor.visible;
        previousLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (cameraOrbit != null)
        {
            cameraOrbitWasEnabled = cameraOrbit.enabled;
            cameraOrbit.enabled = false;
        }

        DisablePlayerControl();
        Time.timeScale = 0f;

        if (worldCanvas != null)
        {
            worldCanvas.gameObject.SetActive(true);
            UpdateMenuCanvasPose(true);
        }
    }

    public void ContinueGame()
    {
        if (state != PauseState.Paused && state != PauseState.Opening)
            return;

        CloseMusicPanel();

        if (tabMenu != null)
            tabMenu.Hide();

        transitionFromPose = CaptureCameraPose();
        transitionToPose = gameplayPose;
        transitionTimer = 0f;
        state = PauseState.Closing;
    }

    public void OpenMusicPanel()
    {
        if (musicPanel == null)
            return;

        musicPanel.SetActive(true);
        if (tabMenu != null)
            tabMenu.inputEnabled = false;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(AudioListener.volume);
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        RefreshMusicValueText();
    }

    public void CloseMusicPanel()
    {
        if (musicPanel != null)
            musicPanel.SetActive(false);

        if (tabMenu != null)
            tabMenu.inputEnabled = true;
    }

    public void SetMusicVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(AudioListener.volume);

        RefreshMusicValueText();
    }

    public void ExitLevel()
    {
        Time.timeScale = 1f;
        RestorePlayerControl();
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = previousLockMode;

        if (!string.IsNullOrEmpty(exitSceneName) && CanLoadScene(exitSceneName))
        {
            SceneManager.LoadScene(exitSceneName);
            return;
        }

        if (quitApplicationIfSceneMissing)
            Application.Quit();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                playerObject = GameObject.Find("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (controlledCamera == null)
            controlledCamera = Camera.main;

        if (cameraOrbit == null && controlledCamera != null)
            cameraOrbit = controlledCamera.GetComponentInParent<CameraOrbit>();

        if (worldCanvas != null)
        {
            if (useScreenSpaceMenu)
            {
                worldCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                worldCanvas.worldCamera = null;
            }
            else if (worldCanvas.worldCamera == null)
            {
                worldCanvas.worldCamera = controlledCamera;
            }
        }

        if (tabMenu != null)
            tabMenu.eventCamera = useScreenSpaceMenu ? null : controlledCamera;
    }

    private void WireButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (musicButton != null)
        {
            musicButton.onClick.RemoveListener(OpenMusicPanel);
            musicButton.onClick.AddListener(OpenMusicPanel);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitLevel);
            exitButton.onClick.AddListener(ExitLevel);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }
    }

    private void HideMenuImmediate()
    {
        if (worldCanvas != null)
            worldCanvas.gameObject.SetActive(false);

        if (tabMenu != null)
            tabMenu.HideImmediate();

        CloseMusicPanel();
    }

    private void UpdateCameraTransition()
    {
        transitionTimer += Time.unscaledDeltaTime;
        float normalized = cameraTransitionDuration <= 0.001f
            ? 1f
            : Mathf.Clamp01(transitionTimer / cameraTransitionDuration);
        float eased = cameraTransitionCurve != null
            ? cameraTransitionCurve.Evaluate(normalized)
            : Mathf.SmoothStep(0f, 1f, normalized);

        ApplyCameraPose(LerpCameraPose(transitionFromPose, transitionToPose, eased));

        if (state == PauseState.Opening && !menuShownThisPause && normalized >= menuShowAtTransition)
            ShowMenu();

        if (normalized < 1f)
            return;

        if (state == PauseState.Opening)
        {
            if (!menuShownThisPause)
                ShowMenu();

            state = PauseState.Paused;
        }
        else if (state == PauseState.Closing)
        {
            FinishResume();
        }
    }

    private void ShowMenu()
    {
        menuShownThisPause = true;

        if (worldCanvas != null)
        {
            worldCanvas.gameObject.SetActive(true);
            UpdateMenuCanvasPose(true);
        }

        if (tabMenu != null)
        {
            tabMenu.inputEnabled = true;
            tabMenu.Show();
        }
    }

    private void FinishResume()
    {
        ApplyCameraPose(gameplayPose);

        if (worldCanvas != null)
            worldCanvas.gameObject.SetActive(false);

        if (cameraOrbit != null)
            cameraOrbit.enabled = cameraOrbitWasEnabled;

        RestorePlayerControl();
        Time.timeScale = 1f;
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = previousLockMode;
        state = PauseState.Gameplay;
    }

    private CameraPose CaptureCameraPose()
    {
        CameraPose pose = new CameraPose();
        if (controlledCamera != null)
        {
            pose.position = controlledCamera.transform.position;
            pose.rotation = controlledCamera.transform.rotation;
            pose.fieldOfView = controlledCamera.fieldOfView;
        }

        return pose;
    }

    private CameraPose BuildPauseCameraPose()
    {
        Vector3 faceTarget = GetFaceTarget();
        Vector3 forward = GetPlanarDirection(player.forward, Vector3.forward);
        Vector3 right = GetPlanarDirection(player.right, Vector3.right);

        CameraPose pose = new CameraPose();
        pose.position = faceTarget
            + forward * faceForwardDistance
            + right * faceSideOffset
            + Vector3.up * faceVerticalOffset;

        Vector3 lookDirection = faceTarget - pose.position;
        pose.rotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : controlledCamera.transform.rotation;
        pose.fieldOfView = pauseFieldOfView;
        return pose;
    }

    private Vector3 GetFaceTarget()
    {
        if (player == null)
            return Vector3.zero;

        return player.position + Vector3.up * faceHeight;
    }

    private static Vector3 GetPlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = fallback;

        return direction.normalized;
    }

    private void ApplyCameraPose(CameraPose pose)
    {
        if (controlledCamera == null)
            return;

        controlledCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        controlledCamera.fieldOfView = pose.fieldOfView;
    }

    private static CameraPose LerpCameraPose(CameraPose from, CameraPose to, float amount)
    {
        CameraPose pose = new CameraPose();
        pose.position = Vector3.LerpUnclamped(from.position, to.position, amount);
        pose.rotation = Quaternion.SlerpUnclamped(from.rotation, to.rotation, amount);
        pose.fieldOfView = Mathf.LerpUnclamped(from.fieldOfView, to.fieldOfView, amount);
        return pose;
    }

    private void UpdateMenuCanvasPose(bool immediate)
    {
        if (worldCanvas == null || controlledCamera == null || player == null)
            return;

        if (useScreenSpaceMenu)
        {
            UpdateScreenSpaceMenuPose(immediate);
            return;
        }

        Transform cameraTransform = controlledCamera.transform;
        Vector3 targetPosition = GetFaceTarget()
            + cameraTransform.right * menuScreenRightOffset
            + cameraTransform.up * menuScreenUpOffset
            + cameraTransform.forward * menuForwardOffset;
        Quaternion targetRotation = Quaternion.LookRotation(targetPosition - cameraTransform.position, Vector3.up);
        Vector3 targetScale = Vector3.one * menuCanvasScale;

        if (immediate)
        {
            worldCanvas.transform.SetPositionAndRotation(targetPosition, targetRotation);
            worldCanvas.transform.localScale = targetScale;
            return;
        }

        float amount = 1f - Mathf.Exp(-menuPoseFollowSpeed * Time.unscaledDeltaTime);
        worldCanvas.transform.position = Vector3.Lerp(worldCanvas.transform.position, targetPosition, amount);
        worldCanvas.transform.rotation = Quaternion.Slerp(worldCanvas.transform.rotation, targetRotation, amount);
        worldCanvas.transform.localScale = Vector3.Lerp(worldCanvas.transform.localScale, targetScale, amount);
    }

    private void UpdateScreenSpaceMenuPose(bool immediate)
    {
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        RectTransform menuRoot = tabMenu != null ? tabMenu.GetComponent<RectTransform>() : null;
        if (canvasRect == null || menuRoot == null)
            return;

        worldCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        worldCanvas.worldCamera = null;
        worldCanvas.transform.localScale = Vector3.one;
        worldCanvas.transform.localPosition = Vector3.zero;
        worldCanvas.transform.localRotation = Quaternion.identity;

        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        Vector2 size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(Screen.width, Screen.height);

        Vector2 half = size * 0.5f;
        Vector2 target = new Vector2(
            Mathf.Lerp(-half.x, half.x, Mathf.Clamp01(screenMenuAnchor.x)),
            Mathf.Lerp(-half.y, half.y, Mathf.Clamp01(screenMenuAnchor.y))) + screenMenuOffset;

        Vector2 padding = new Vector2(
            Mathf.Min(screenMenuPadding.x, Mathf.Max(0f, half.x - 1f)),
            Mathf.Min(screenMenuPadding.y, Mathf.Max(0f, half.y - 1f)));
        target.x = Mathf.Clamp(target.x, -half.x + padding.x, half.x - padding.x);
        target.y = Mathf.Clamp(target.y, -half.y + padding.y, half.y - padding.y);

        menuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.localScale = Vector3.one;
        menuRoot.localRotation = Quaternion.identity;

        if (immediate)
        {
            menuRoot.anchoredPosition = target;
            return;
        }

        float amount = 1f - Mathf.Exp(-screenMenuFollowSpeed * Time.unscaledDeltaTime);
        menuRoot.anchoredPosition = Vector2.Lerp(menuRoot.anchoredPosition, target, amount);
    }

    private void DisablePlayerControl()
    {
        disabledPlayerBehaviours.Clear();
        if (player == null)
            return;

        MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled)
                continue;

            if (behaviour.GetType().Name == "PlayerRbController")
            {
                disabledPlayerBehaviours.Add(behaviour);
                behaviour.enabled = false;
            }
        }
    }

    private void RestorePlayerControl()
    {
        for (int i = 0; i < disabledPlayerBehaviours.Count; i++)
        {
            if (disabledPlayerBehaviours[i] != null)
                disabledPlayerBehaviours[i].enabled = true;
        }

        disabledPlayerBehaviours.Clear();
    }

    private void HandleMusicPanelInput()
    {
        if (musicPanel == null || !musicPanel.activeSelf)
            return;

        float delta = 0f;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            delta -= musicStep;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            delta += musicStep;

        float axis = ReadAxisSafe(horizontalAxis);
        int direction = 0;
        if (axis > horizontalAxisThreshold)
            direction = 1;
        else if (axis < -horizontalAxisThreshold)
            direction = -1;

        if (direction == 0)
        {
            horizontalAxisDirection = 0;
        }
        else if (direction != horizontalAxisDirection)
        {
            delta += direction * musicStep;
            horizontalAxisDirection = direction;
        }

        if (Mathf.Abs(delta) > 0.0001f)
            SetMusicVolume(AudioListener.volume + delta);
    }

    private void RefreshMusicValueText()
    {
        if (musicValueText != null)
            musicValueText.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
    }

    private bool ReadCancelButtonDown()
    {
        if (string.IsNullOrEmpty(cancelButton) || !cancelButtonAvailable)
            return false;

        try
        {
            return Input.GetButtonDown(cancelButton);
        }
        catch (System.ArgumentException)
        {
            cancelButtonAvailable = false;
            return false;
        }
    }

    private bool ReadPauseInputDown()
    {
        return Input.GetKeyDown(pauseKey)
            || Input.GetKeyDown(gamepadPauseButton)
            || Input.GetKeyDown(gamepadPauseAltButton)
            || Input.GetKeyDown(backKey)
            || ReadCancelButtonDown();
    }

    private static float ReadAxisSafe(string axisName)
    {
        if (string.IsNullOrEmpty(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch (System.ArgumentException)
        {
            return 0f;
        }
    }

    private static bool CanLoadScene(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
                return true;
        }

        return false;
    }
}
