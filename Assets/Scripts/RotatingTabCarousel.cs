using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RotatingTabCarousel : MonoBehaviour
{
    private enum FocusSource
    {
        None,
        Pointer,
        Navigation
    }

    [Header("Cards")]
    public List<RectTransform> cards = new List<RectTransform>();

    [Header("Side Layout")]
    public Vector2 sideCenter = new Vector2(685f, -165f);
    public Vector2 sideSpacing = new Vector2(34f, -150f);
    public float sideRotationStep = -10f;
    public float sideScale = 0.9f;

    [Header("Focused Card")]
    public Vector2 focusedPosition = new Vector2(600f, -165f);
    public float focusedScale = 1.22f;
    public float focusedRotationZ = 0f;

    [Header("Motion")]
    public float moveSpeed = 12f;
    public float rotateSpeed = 12f;
    public float scaleSpeed = 12f;
    public bool selectFirstOnStart = false;
    public float pointerRetractDelay = 0.22f;
    public float pointerSettlePositionTolerance = 3f;
    public float pointerSettleScaleTolerance = 0.03f;
    public float pointerSettleRotationTolerance = 2f;
    public bool requireSettledBeforeNavigation = true;

    [Header("Input")]
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
    public string submitButton = "Submit";
    [Range(0.1f, 1f)] public float axisThreshold = 0.55f;
    public float axisRepeatDelay = 0.3f;
    public float axisRepeatRate = 0.14f;

    private int focusedIndex = -1;
    private FocusSource focusSource;
    private float lastPointerInsideTime;
    private int axisDirection;
    private float nextAxisMoveTime;
    private bool verticalAxisAvailable = true;
    private bool submitButtonAvailable = true;
    private readonly List<Vector2> targetPositions = new List<Vector2>();
    private readonly List<float> targetRotations = new List<float>();
    private readonly List<float> targetScales = new List<float>();

    private void Awake()
    {
        RebuildCards();
        if (selectFirstOnStart && cards.Count > 0)
            Focus(0, FocusSource.Navigation);
        BuildTargets();
        SnapToTargets();
        ApplyCardHighlights();
    }

    private void OnValidate()
    {
        pointerRetractDelay = Mathf.Max(0f, pointerRetractDelay);
        pointerSettlePositionTolerance = Mathf.Max(0.1f, pointerSettlePositionTolerance);
        pointerSettleScaleTolerance = Mathf.Max(0.001f, pointerSettleScaleTolerance);
        pointerSettleRotationTolerance = Mathf.Max(0.1f, pointerSettleRotationTolerance);
        axisRepeatDelay = Mathf.Max(0.01f, axisRepeatDelay);
        axisRepeatRate = Mathf.Max(0.01f, axisRepeatRate);
        BuildTargets();
    }

    private void Update()
    {
        if (cards.Count == 0)
            return;

        HandleSelectionInput();
        BuildTargets();
        ApplyCardMotion();
        HandleSettledPointerHover();
        ApplyCardHighlights();
    }

    public void PointerEntered(RectTransform card)
    {
        int index = cards.IndexOf(card);
        if (index < 0)
            return;

        if (!IsMenuRootNearMainPosition() || !IsPointerInputSettled())
            return;

        lastPointerInsideTime = Time.unscaledTime;
        Focus(index, FocusSource.Pointer);
    }

    public void PointerExited(RectTransform card)
    {
        if (!IsPointerInputSettled())
            return;

        if (focusSource == FocusSource.Pointer && !IsMouseOverAnyCard())
            lastPointerInsideTime = Time.unscaledTime - pointerRetractDelay;
    }

    public void FocusFromNavigation(RectTransform card)
    {
        int index = cards.IndexOf(card);
        if (index >= 0)
            Focus(index, FocusSource.Navigation);
    }

    public void Focus(RectTransform card)
    {
        FocusFromNavigation(card);
    }

    public void Focus(int index)
    {
        Focus(index, FocusSource.Navigation);
    }

    public void ClearFocus()
    {
        focusedIndex = -1;
        focusSource = FocusSource.None;
    }

    public void ApplyLayoutImmediate()
    {
        RebuildCards();
        BuildTargets();
        SnapToTargets();
        ApplyCardHighlights();
    }

    public void RebuildCards()
    {
        cards.RemoveAll(card => card == null);

        for (int i = 0; i < cards.Count; i++)
        {
            RotatingTabCard tabCard = cards[i].GetComponent<RotatingTabCard>();
            if (tabCard == null)
                tabCard = cards[i].gameObject.AddComponent<RotatingTabCard>();

            tabCard.carousel = this;
            tabCard.card = cards[i];

            Button button = cards[i].GetComponent<Button>();
            if (button != null)
            {
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }
        }
    }

    private void Focus(int index, FocusSource source)
    {
        focusedIndex = Mathf.Clamp(index, -1, cards.Count - 1);
        focusSource = focusedIndex >= 0 ? source : FocusSource.None;
    }

    private void HandleSelectionInput()
    {
        if (!IsMenuRootNearMainPosition())
            return;

        bool movedWithKey = false;
        bool submittedWithKey = false;

        if (allowKeyboardInput)
        {
            if (Input.GetKeyDown(previousKey) || Input.GetKeyDown(previousAltKey))
            {
                MoveSelection(-1);
                movedWithKey = true;
            }
            else if (Input.GetKeyDown(nextKey) || Input.GetKeyDown(nextAltKey))
            {
                MoveSelection(1);
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
        int direction = 0;
        if (axis > axisThreshold)
            direction = -1;
        else if (axis < -axisThreshold)
            direction = 1;

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

        if (requireSettledBeforeNavigation && !IsPointerInputSettled())
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
        if (cards.Count == 0)
            return;

        if (focusedIndex < 0)
        {
            Focus(0, FocusSource.Navigation);
            return;
        }

        Button button = cards[focusedIndex].GetComponent<Button>();
        if (button != null && button.IsActive() && button.IsInteractable())
            button.onClick.Invoke();
    }

    private float ReadVerticalAxis()
    {
        if (!verticalAxisAvailable || string.IsNullOrEmpty(verticalAxis))
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
        catch (System.InvalidOperationException)
        {
            verticalAxisAvailable = false;
            return 0f;
        }
    }

    private bool ReadSubmitButtonDown()
    {
        if (!submitButtonAvailable || string.IsNullOrEmpty(submitButton))
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
        catch (System.InvalidOperationException)
        {
            submitButtonAvailable = false;
            return false;
        }
    }

    private void HandleSettledPointerHover()
    {
        if (!IsMenuRootNearMainPosition())
        {
            if (focusSource == FocusSource.Pointer)
                ClearFocus();
            return;
        }

        if (!IsPointerInputSettled())
            return;

        RectTransform hoveredCard = GetCardUnderMouse();
        if (hoveredCard != null)
        {
            lastPointerInsideTime = Time.unscaledTime;
            int hoveredIndex = cards.IndexOf(hoveredCard);
            if (hoveredIndex >= 0 && (focusSource != FocusSource.Pointer || focusedIndex != hoveredIndex))
                Focus(hoveredIndex, FocusSource.Pointer);

            return;
        }

        if (focusSource != FocusSource.Pointer)
            return;

        if (Time.unscaledTime - lastPointerInsideTime >= pointerRetractDelay)
            ClearFocus();
    }

    private RectTransform GetCardUnderMouse()
    {
        Vector2 mousePosition = Input.mousePosition;
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card != null && RectTransformUtility.RectangleContainsScreenPoint(card, mousePosition, null))
                return card;
        }

        return null;
    }

    private bool IsPointerInputSettled()
    {
        if (cards.Count == 0 || targetPositions.Count != cards.Count || targetRotations.Count != cards.Count || targetScales.Count != cards.Count)
            return false;

        float positionToleranceSqr = pointerSettlePositionTolerance * pointerSettlePositionTolerance;
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            if ((card.anchoredPosition - targetPositions[i]).sqrMagnitude > positionToleranceSqr)
                return false;

            if (Mathf.Abs(card.localScale.x - targetScales[i]) > pointerSettleScaleTolerance)
                return false;

            if (Mathf.Abs(Mathf.DeltaAngle(card.localEulerAngles.z, targetRotations[i])) > pointerSettleRotationTolerance)
                return false;
        }

        return true;
    }

    private bool IsMouseOverAnyCard()
    {
        Vector2 mousePosition = Input.mousePosition;
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card != null && RectTransformUtility.RectangleContainsScreenPoint(card, mousePosition, null))
                return true;
        }

        return false;
    }

    private bool IsMenuRootNearMainPosition()
    {
        RectTransform root = transform.parent as RectTransform;
        if (root == null)
            return true;

        return root.anchoredPosition.sqrMagnitude < 4f;
    }

    private void ApplyCardMotion()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null || i >= targetPositions.Count)
                continue;

            float dt = Time.unscaledDeltaTime;
            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, targetPositions[i], 1f - Mathf.Exp(-moveSpeed * dt));
            card.localRotation = Quaternion.Slerp(card.localRotation, Quaternion.Euler(0f, 0f, targetRotations[i]), 1f - Mathf.Exp(-rotateSpeed * dt));
            card.localScale = Vector3.Lerp(card.localScale, Vector3.one * targetScales[i], 1f - Mathf.Exp(-scaleSpeed * dt));

            if (i == focusedIndex)
                card.SetAsLastSibling();
        }
    }

    private void ApplyCardHighlights()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            ButtonHoverDottedOutline effect = card.GetComponent<ButtonHoverDottedOutline>();
            if (effect != null)
                effect.SetVisible(i == focusedIndex);
        }
    }

    private void BuildTargets()
    {
        targetPositions.Clear();
        targetRotations.Clear();
        targetScales.Clear();

        int count = Mathf.Max(1, cards.Count);
        float middle = (count - 1) * 0.5f;

        for (int i = 0; i < cards.Count; i++)
        {
            float offset = i - middle;
            Vector2 position = sideCenter + new Vector2(sideSpacing.x * Mathf.Abs(offset), sideSpacing.y * offset);
            float rotation = sideRotationStep * offset;
            float scale = sideScale;

            if (focusedIndex == i)
            {
                position = focusedPosition;
                rotation = focusedRotationZ;
                scale = focusedScale;
            }
            else if (focusedIndex >= 0)
            {
                int relative = i < focusedIndex ? -1 : 1;
                int distance = Mathf.Abs(i - focusedIndex);
                position = focusedPosition + new Vector2(sideSpacing.x * distance, sideSpacing.y * relative * distance);
                rotation = sideRotationStep * relative * distance;
                scale = Mathf.Max(0.75f, sideScale - 0.04f * distance);
            }

            targetPositions.Add(position);
            targetRotations.Add(rotation);
            targetScales.Add(scale);
        }
    }

    private void SnapToTargets()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || i >= targetPositions.Count)
                continue;

            cards[i].anchoredPosition = targetPositions[i];
            cards[i].localRotation = Quaternion.Euler(0f, 0f, targetRotations[i]);
            cards[i].localScale = Vector3.one * targetScales[i];
        }
    }
}
