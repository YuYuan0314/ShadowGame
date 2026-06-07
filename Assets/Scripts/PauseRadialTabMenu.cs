using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseRadialTabMenu : MonoBehaviour
{
    private enum FocusSource
    {
        None,
        Pointer,
        Navigation
    }

    [Header("Cards")]
    public List<RectTransform> cards = new List<RectTransform>();
    public List<Button> buttons = new List<Button>();
    public Camera eventCamera;

    [Header("Ring Layout")]
    public Vector2 center = Vector2.zero;
    public float radius = 145f;
    public float startAngle = 72f;
    public float angleStep = -72f;
    public float sideScale = 0.88f;
    public float sideRotationMultiplier = 0.22f;

    [Header("Focused Card")]
    public Vector2 focusedPosition = new Vector2(-12f, 0f);
    public float focusedScale = 1.24f;
    public float focusedRotationZ = 0f;

    [Header("Motion")]
    public float openSpeed = 7f;
    public float moveSpeed = 14f;
    public float rotateSpeed = 14f;
    public float scaleSpeed = 14f;
    public float hiddenScale = 0.08f;
    public bool selectFirstOnShow = true;
    public bool requireSettledForPointer = true;
    public float pointerRetractDelay = 0.18f;
    public float settlePositionTolerance = 3f;
    public float settleScaleTolerance = 0.035f;
    public float settleRotationTolerance = 2.5f;

    [Header("Input")]
    public bool inputEnabled = true;
    public bool allowKeyboardInput = true;
    public bool allowGamepadInput = true;
    public KeyCode previousKey = KeyCode.UpArrow;
    public KeyCode previousAltKey = KeyCode.W;
    public KeyCode nextKey = KeyCode.DownArrow;
    public KeyCode nextAltKey = KeyCode.S;
    public KeyCode submitKey = KeyCode.Return;
    public KeyCode submitAltKey = KeyCode.Space;
    public KeyCode gamepadSubmitButton = KeyCode.JoystickButton0;
    public string verticalAxis = "Vertical";
    public bool invertGamepadVerticalAxis = true;
    public bool suppressKeyboardAxisInput = true;
    public string submitButton = "Submit";
    [Range(0.1f, 1f)] public float axisThreshold = 0.55f;
    public float axisRepeatDelay = 0.3f;
    public float axisRepeatRate = 0.14f;

    private int focusedIndex = -1;
    private FocusSource focusSource;
    private float openAmount;
    private bool visible;
    private int axisDirection;
    private float nextAxisMoveTime;
    private float lastPointerInsideTime;
    private bool verticalAxisAvailable = true;
    private bool submitButtonAvailable = true;

    private readonly List<Vector2> targetPositions = new List<Vector2>();
    private readonly List<float> targetRotations = new List<float>();
    private readonly List<float> targetScales = new List<float>();

    private void Awake()
    {
        Rebuild();
        BuildTargets();
        SnapToTargets();
        ApplyHighlights();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void Update()
    {
        if (cards.Count == 0)
            return;

        float targetOpen = visible ? 1f : 0f;
        openAmount = Mathf.MoveTowards(openAmount, targetOpen, Time.unscaledDeltaTime * openSpeed);

        if (visible && inputEnabled && openAmount > 0.85f)
        {
            HandleSelectionInput();
            HandlePointerHover();
        }

        BuildTargets();
        ApplyMotion();
        ApplyHighlights();
    }

    public void Show()
    {
        visible = true;
        gameObject.SetActive(true);
        Rebuild();

        if (selectFirstOnShow && focusedIndex < 0 && cards.Count > 0)
            Focus(0, FocusSource.Navigation);
    }

    public void Hide()
    {
        visible = false;
        focusedIndex = -1;
        focusSource = FocusSource.None;
    }

    public void HideImmediate()
    {
        visible = false;
        focusedIndex = -1;
        focusSource = FocusSource.None;
        openAmount = 0f;
        BuildTargets();
        SnapToTargets();
        ApplyHighlights();
    }

    public void Focus(int index)
    {
        Focus(index, FocusSource.Navigation);
    }

    public void Rebuild()
    {
        cards.RemoveAll(card => card == null);
        buttons.RemoveAll(button => button == null);

        while (buttons.Count < cards.Count)
        {
            Button button = cards[buttons.Count] != null ? cards[buttons.Count].GetComponent<Button>() : null;
            buttons.Add(button);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            Button button = cards[i].GetComponent<Button>();
            if (button != null)
            {
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }

            if (i < buttons.Count && buttons[i] == null)
                buttons[i] = button;
        }
    }

    private void HandleSelectionInput()
    {
        bool movedWithKey = false;
        bool submittedWithKey = false;

        if (allowKeyboardInput)
        {
            if (Input.GetKeyDown(previousKey) || Input.GetKeyDown(previousAltKey))
            {
                MoveSelection(1);
                axisDirection = 0;
                nextAxisMoveTime = Time.unscaledTime + axisRepeatDelay;
                movedWithKey = true;
            }
            else if (Input.GetKeyDown(nextKey) || Input.GetKeyDown(nextAltKey))
            {
                MoveSelection(-1);
                axisDirection = 0;
                nextAxisMoveTime = Time.unscaledTime + axisRepeatDelay;
                movedWithKey = true;
            }

            if (Input.GetKeyDown(submitKey) || Input.GetKeyDown(submitAltKey))
            {
                SubmitSelection();
                submittedWithKey = true;
            }
        }

        if (!allowGamepadInput)
            return;

        if (!movedWithKey)
            HandleAxisInput(ReadVerticalAxis());

        if (!submittedWithKey && (Input.GetKeyDown(gamepadSubmitButton) || ReadSubmitButtonDown()))
            SubmitSelection();
    }

    private void HandleAxisInput(float axis)
    {
        if (invertGamepadVerticalAxis)
            axis = -axis;

        int direction = 0;
        if (axis > axisThreshold)
            direction = 1;
        else if (axis < -axisThreshold)
            direction = -1;

        if (direction == 0)
        {
            axisDirection = 0;
            return;
        }

        float now = Time.unscaledTime;
        bool changedDirection = direction != axisDirection;
        if (changedDirection || now >= nextAxisMoveTime)
        {
            MoveSelection(direction);
            axisDirection = direction;
            nextAxisMoveTime = now + (changedDirection ? axisRepeatDelay : axisRepeatRate);
        }
    }

    private void MoveSelection(int direction)
    {
        if (cards.Count == 0)
            return;

        int index = focusedIndex;
        if (index < 0)
            index = direction < 0 ? cards.Count - 1 : 0;
        else
            index = (index + direction + cards.Count) % cards.Count;

        Focus(index, FocusSource.Navigation);
    }

    private void SubmitSelection()
    {
        if (focusedIndex < 0 || focusedIndex >= buttons.Count || buttons[focusedIndex] == null)
            return;

        buttons[focusedIndex].onClick.Invoke();
    }

    private void HandlePointerHover()
    {
        if (requireSettledForPointer && !IsSettled())
            return;

        int hoveredIndex = GetHoveredCardIndex();
        if (hoveredIndex >= 0)
        {
            lastPointerInsideTime = Time.unscaledTime;
            Focus(hoveredIndex, FocusSource.Pointer);
            return;
        }

        if (focusSource == FocusSource.Pointer && Time.unscaledTime - lastPointerInsideTime >= pointerRetractDelay)
        {
            focusedIndex = -1;
            focusSource = FocusSource.None;
        }
    }

    private int GetHoveredCardIndex()
    {
        Camera cam = GetEventCamera();
        Vector2 mousePosition = Input.mousePosition;

        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null || !card.gameObject.activeInHierarchy)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(card, mousePosition, cam))
                return i;
        }

        return -1;
    }

    private Camera GetEventCamera()
    {
        if (eventCamera != null)
            return eventCamera;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        return null;
    }

    private void Focus(int index, FocusSource source)
    {
        focusedIndex = Mathf.Clamp(index, -1, cards.Count - 1);
        focusSource = focusedIndex >= 0 ? source : FocusSource.None;
        BringFocusedCardToFront();
    }

    private void BringFocusedCardToFront()
    {
        if (focusedIndex < 0 || focusedIndex >= cards.Count || cards[focusedIndex] == null)
            return;

        cards[focusedIndex].SetAsLastSibling();
    }

    private void BuildTargets()
    {
        EnsureTargetCapacity();

        for (int i = 0; i < cards.Count; i++)
        {
            float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector2 ringPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float ringRotation = Mathf.Sin(angle) * radius * sideRotationMultiplier;

            Vector2 targetPosition = ringPosition;
            float targetRotation = ringRotation;
            float targetScale = sideScale;

            if (i == focusedIndex)
            {
                targetPosition = focusedPosition;
                targetRotation = focusedRotationZ;
                targetScale = focusedScale;
            }

            targetPositions[i] = Vector2.Lerp(center, targetPosition, openAmount);
            targetRotations[i] = Mathf.Lerp(0f, targetRotation, openAmount);
            targetScales[i] = Mathf.Lerp(hiddenScale, targetScale, openAmount);
        }
    }

    private void ApplyMotion()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            float positionAmount = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
            float rotationAmount = 1f - Mathf.Exp(-rotateSpeed * Time.unscaledDeltaTime);
            float scaleAmount = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);

            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, targetPositions[i], positionAmount);
            float z = Mathf.LerpAngle(card.localEulerAngles.z, targetRotations[i], rotationAmount);
            card.localRotation = Quaternion.Euler(0f, 0f, z);
            float scale = Mathf.Lerp(card.localScale.x, targetScales[i], scaleAmount);
            card.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void SnapToTargets()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            card.anchoredPosition = targetPositions[i];
            card.localRotation = Quaternion.Euler(0f, 0f, targetRotations[i]);
            card.localScale = new Vector3(targetScales[i], targetScales[i], 1f);
        }
    }

    private void ApplyHighlights()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null)
                continue;

            ButtonHoverDottedOutline outline = cards[i].GetComponent<ButtonHoverDottedOutline>();
            if (outline != null)
                outline.SetVisible(visible && openAmount > 0.8f && i == focusedIndex);
        }
    }

    private bool IsSettled()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            if (Vector2.Distance(card.anchoredPosition, targetPositions[i]) > settlePositionTolerance)
                return false;

            if (Mathf.Abs(card.localScale.x - targetScales[i]) > settleScaleTolerance)
                return false;

            if (Mathf.Abs(Mathf.DeltaAngle(card.localEulerAngles.z, targetRotations[i])) > settleRotationTolerance)
                return false;
        }

        return true;
    }

    private void EnsureTargetCapacity()
    {
        while (targetPositions.Count < cards.Count)
            targetPositions.Add(Vector2.zero);
        while (targetRotations.Count < cards.Count)
            targetRotations.Add(0f);
        while (targetScales.Count < cards.Count)
            targetScales.Add(hiddenScale);
    }

    private float ReadVerticalAxis()
    {
        if (string.IsNullOrEmpty(verticalAxis) || !verticalAxisAvailable)
            return 0f;

        if (suppressKeyboardAxisInput
            && (Input.GetKey(previousKey)
                || Input.GetKey(previousAltKey)
                || Input.GetKey(nextKey)
                || Input.GetKey(nextAltKey)))
            return 0f;

        try
        {
            return Input.GetAxisRaw(verticalAxis);
        }
        catch (System.ArgumentException)
        {
            verticalAxisAvailable = false;
            return 0f;
        }
    }

    private bool ReadSubmitButtonDown()
    {
        if (string.IsNullOrEmpty(submitButton) || !submitButtonAvailable)
            return false;

        try
        {
            return Input.GetButtonDown(submitButton);
        }
        catch (System.ArgumentException)
        {
            submitButtonAvailable = false;
            return false;
        }
    }
}
