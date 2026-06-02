using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelRingCarousel : MonoBehaviour
{
    [System.Serializable]
    public class LevelItem
    {
        public RectTransform card;
        public Button button;
        public Image previewImage;
        public Text titleText;
        public Text subtitleText;
        public string levelName = "LEVEL";
        public string sceneName;
    }

    [Header("Items")]
    public List<LevelItem> items = new List<LevelItem>();
    public int selectedIndex;

    [Header("Arc Layout")]
    public Vector2 center = new Vector2(0f, 20f);
    public float radiusX = 560f;
    public float radiusY = 120f;
    public float angleStep = 38f;
    public float selectedScale = 1.24f;
    public float sideScale = 0.74f;
    public float farScale = 0.55f;
    public float selectedYBoost = 42f;
    public float sideAlpha = 0.72f;
    public float farAlpha = 0.34f;

    [Header("Motion")]
    public float moveSpeed = 12f;
    public float scaleSpeed = 12f;
    public float fadeSpeed = 12f;
    public bool snapOnStart = true;

    [Header("Input")]
    public bool allowKeyboardInput = true;
    public bool allowGamepadInput = true;
    public KeyCode previousKey = KeyCode.LeftArrow;
    public KeyCode previousAltKey = KeyCode.A;
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode nextAltKey = KeyCode.D;
    public KeyCode submitKey = KeyCode.Return;
    public KeyCode submitAltKey = KeyCode.Space;
    public KeyCode gamepadSubmitButton = KeyCode.JoystickButton0;
    public string horizontalAxis = "Horizontal";
    public string submitButton = "Submit";
    [Range(0.1f, 1f)] public float axisThreshold = 0.55f;
    public float axisRepeatDelay = 0.3f;
    public float axisRepeatRate = 0.14f;

    [Header("Status Text")]
    public Text selectedTitleText;
    public Text selectedSceneText;

    private int axisDirection;
    private float nextAxisMoveTime;
    private bool horizontalAxisAvailable = true;
    private bool submitButtonAvailable = true;

    private void Awake()
    {
        RebuildCards();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, items.Count - 1));
        if (snapOnStart)
            ApplyLayout(true);
    }

    private void Update()
    {
        if (items.Count == 0)
            return;

        HandleInput();
        ApplyLayout(false);
    }

    public void RebuildCards()
    {
        items.RemoveAll(item => item == null || item.card == null);
        for (int i = 0; i < items.Count; i++)
        {
            LevelItem item = items[i];
            if (item.button == null)
                item.button = item.card.GetComponent<Button>();

            LevelRingCard card = item.card.GetComponent<LevelRingCard>();
            if (card == null)
                card = item.card.gameObject.AddComponent<LevelRingCard>();

            card.carousel = this;
            card.index = i;

            if (item.button != null)
            {
                int captured = i;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => ClickCard(captured));

                Navigation navigation = item.button.navigation;
                navigation.mode = Navigation.Mode.None;
                item.button.navigation = navigation;
            }
        }
    }

    public void ClickCard(int index)
    {
        if (index < 0 || index >= items.Count)
            return;

        if (index != selectedIndex)
        {
            Select(index);
            return;
        }

        ConfirmSelection();
    }

    public void Select(int index)
    {
        if (items.Count == 0)
            return;

        selectedIndex = (index + items.Count) % items.Count;
        UpdateStatusText();
    }

    public void MoveSelection(int direction)
    {
        if (items.Count == 0 || direction == 0)
            return;

        Select(selectedIndex + direction);
    }

    public void ConfirmSelection()
    {
        if (items.Count == 0)
            return;

        LevelItem item = items[Mathf.Clamp(selectedIndex, 0, items.Count - 1)];
        if (!string.IsNullOrWhiteSpace(item.sceneName))
            SceneManager.LoadScene(item.sceneName);
    }

    public void ApplyLayoutImmediate()
    {
        RebuildCards();
        ApplyLayout(true);
    }

    private void HandleInput()
    {
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
                ConfirmSelection();
                submittedWithKey = true;
            }
        }

        if (!allowGamepadInput)
            return;

        if (!movedWithKey)
            HandleAxisInput(ReadHorizontalAxis());

        if (!submittedWithKey && (Input.GetKeyDown(gamepadSubmitButton) || ReadSubmitButtonDown()))
            ConfirmSelection();
    }

    private void HandleAxisInput(float axis)
    {
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

    private void ApplyLayout(bool immediate)
    {
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, items.Count - 1));
        int selectedSibling = 0;

        for (int i = 0; i < items.Count; i++)
        {
            LevelItem item = items[i];
            if (item.card == null)
                continue;

            int relative = GetShortestRelativeIndex(i);
            float angle = relative * angleStep * Mathf.Deg2Rad;
            float depth = Mathf.Cos(angle);
            Vector2 targetPosition = center + new Vector2(Mathf.Sin(angle) * radiusX, depth * radiusY - radiusY);
            if (relative == 0)
                targetPosition.y += selectedYBoost;

            float distance = Mathf.Abs(relative);
            float targetScale = distance < 0.01f ? selectedScale : Mathf.Lerp(sideScale, farScale, Mathf.Clamp01((distance - 1f) / 2f));
            float targetAlpha = distance < 0.01f ? 1f : Mathf.Lerp(sideAlpha, farAlpha, Mathf.Clamp01((distance - 1f) / 2f));
            float targetRotation = -relative * 7f;

            if (immediate)
            {
                item.card.anchoredPosition = targetPosition;
                item.card.localScale = Vector3.one * targetScale;
                item.card.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
                SetGraphicAlpha(item.card, targetAlpha);
            }
            else
            {
                float dt = Time.unscaledDeltaTime;
                item.card.anchoredPosition = Vector2.Lerp(item.card.anchoredPosition, targetPosition, 1f - Mathf.Exp(-moveSpeed * dt));
                item.card.localScale = Vector3.Lerp(item.card.localScale, Vector3.one * targetScale, 1f - Mathf.Exp(-scaleSpeed * dt));
                item.card.localRotation = Quaternion.Slerp(item.card.localRotation, Quaternion.Euler(0f, 0f, targetRotation), 1f - Mathf.Exp(-moveSpeed * dt));
                LerpGraphicAlpha(item.card, targetAlpha, 1f - Mathf.Exp(-fadeSpeed * dt));
            }

            if (relative == 0)
                selectedSibling = item.card.GetSiblingIndex();
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].card == null)
                continue;

            int relative = GetShortestRelativeIndex(i);
            if (relative != 0)
                items[i].card.SetSiblingIndex(Mathf.Max(0, selectedSibling));
        }

        if (items[selectedIndex].card != null)
            items[selectedIndex].card.SetAsLastSibling();

        UpdateStatusText();
    }

    private int GetShortestRelativeIndex(int index)
    {
        int count = items.Count;
        int relative = index - selectedIndex;
        int half = count / 2;
        if (relative > half)
            relative -= count;
        else if (relative < -half)
            relative += count;
        return relative;
    }

    private void UpdateStatusText()
    {
        if (items.Count == 0)
            return;

        LevelItem item = items[Mathf.Clamp(selectedIndex, 0, items.Count - 1)];
        if (selectedTitleText != null)
            selectedTitleText.text = item.levelName;
        if (selectedSceneText != null)
            selectedSceneText.text = string.IsNullOrWhiteSpace(item.sceneName) ? "未绑定场景" : item.sceneName;
    }

    private void SetGraphicAlpha(RectTransform root, float alpha)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Color color = graphics[i].color;
            color.a = alpha;
            graphics[i].color = color;
        }
    }

    private void LerpGraphicAlpha(RectTransform root, float alpha, float t)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Color color = graphics[i].color;
            color.a = Mathf.Lerp(color.a, alpha, t);
            graphics[i].color = color;
        }
    }

    private float ReadHorizontalAxis()
    {
        if (!horizontalAxisAvailable || string.IsNullOrEmpty(horizontalAxis))
            return 0f;

        try
        {
            return Input.GetAxisRaw(horizontalAxis);
        }
        catch (System.ArgumentException)
        {
            horizontalAxisAvailable = false;
            return 0f;
        }
        catch (System.InvalidOperationException)
        {
            horizontalAxisAvailable = false;
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
}

public class LevelRingCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, ISelectHandler
{
    public LevelRingCarousel carousel;
    public int index;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (carousel != null)
            carousel.ClickCard(index);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (carousel != null)
            carousel.Select(index);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (carousel != null)
            carousel.Select(index);
    }
}
