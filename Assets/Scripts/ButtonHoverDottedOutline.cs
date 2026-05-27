using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ButtonHoverDottedOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Glow Ring")]
    public Color outlineColor = Color.white;
    public float padding = 14f;
    [Range(24, 192)] public int dotCount = 96;
    public Vector2 dotSize = new Vector2(8f, 4f);
    public float cornerRadius = 22f;
    public float ringThickness = 4f;
    public float glowThickness = 18f;
    [Range(0.05f, 0.5f)] public float highlightWidth = 0.2f;

    [Header("Motion")]
    public float growSpeed = 12f;
    public float flowSpeed = 0.65f;
    public float hiddenScale = 0.84f;

    private RectTransform outlineRoot;
    private DottedOutlineGraphic outlineGraphic;
    private float target;
    private float amount;
    private float flowOffset;

    private void Awake()
    {
        EnsureOutline();
        ApplyState(true);
    }

    private void OnEnable()
    {
        EnsureOutline();
        ApplyState(true);
    }

    private void Update()
    {
        EnsureOutline();

        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * growSpeed);
        flowOffset = Mathf.Repeat(flowOffset + Time.unscaledDeltaTime * flowSpeed, 1f);
        outlineGraphic.flowOffset = flowOffset;
        ApplyState(false);
    }

    public void SetVisible(bool visible)
    {
        target = visible ? 1f : 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetVisible(false);
    }

    private void EnsureOutline()
    {
        if (outlineRoot != null && outlineGraphic != null)
            return;

        Transform existing = transform.Find("HoverDottedOutline");
        GameObject outlineObject;
        if (existing != null)
        {
            outlineObject = existing.gameObject;
        }
        else
        {
            outlineObject = new GameObject("HoverDottedOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(DottedOutlineGraphic));
            outlineObject.transform.SetParent(transform, false);
        }

        outlineRoot = outlineObject.GetComponent<RectTransform>();
        outlineRoot.anchorMin = Vector2.zero;
        outlineRoot.anchorMax = Vector2.one;
        outlineRoot.pivot = new Vector2(0.5f, 0.5f);
        outlineRoot.offsetMin = new Vector2(-padding, -padding);
        outlineRoot.offsetMax = new Vector2(padding, padding);
        outlineRoot.SetAsLastSibling();

        outlineGraphic = outlineObject.GetComponent<DottedOutlineGraphic>();
        if (outlineGraphic == null)
            outlineGraphic = outlineObject.AddComponent<DottedOutlineGraphic>();

        outlineGraphic.raycastTarget = false;
        outlineGraphic.color = outlineColor;
        outlineGraphic.segments = dotCount;
        outlineGraphic.cornerRadius = cornerRadius;
        outlineGraphic.ringThickness = ringThickness;
        outlineGraphic.glowThickness = glowThickness;
        outlineGraphic.highlightWidth = highlightWidth;
    }

    private void ApplyState(bool immediate)
    {
        if (outlineRoot == null || outlineGraphic == null)
            return;

        float t = immediate ? target : amount;
        float scale = Mathf.Lerp(hiddenScale, 1f, t);
        outlineRoot.localScale = new Vector3(scale, scale, 1f);

        Color color = outlineColor;
        color.a *= Mathf.SmoothStep(0f, 1f, t);
        outlineGraphic.color = color;
        outlineGraphic.segments = dotCount;
        outlineGraphic.cornerRadius = cornerRadius;
        outlineGraphic.ringThickness = ringThickness;
        outlineGraphic.glowThickness = glowThickness;
        outlineGraphic.highlightWidth = highlightWidth;
        outlineGraphic.SetVerticesDirty();
    }
}

public class DottedOutlineGraphic : MaskableGraphic
{
    [Range(24, 192)] public int segments = 96;
    public float cornerRadius = 22f;
    public float ringThickness = 4f;
    public float glowThickness = 18f;
    [Range(0f, 1f)] public float flowOffset;
    [Range(0.05f, 0.5f)] public float highlightWidth = 0.2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float width = rect.width;
        float height = rect.height;
        int segmentCount = Mathf.Max(24, segments);
        if (width <= 0f || height <= 0f)
            return;

        float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(width, height) * 0.5f);
        float straightWidth = Mathf.Max(0f, width - radius * 2f);
        float straightHeight = Mathf.Max(0f, height - radius * 2f);
        float perimeter = 2f * (straightWidth + straightHeight) + Mathf.PI * 2f * radius;
        if (perimeter <= 0f)
            return;

        AddRing(vh, rect, radius, straightWidth, straightHeight, perimeter, glowThickness, 0.08f, 0.34f, segmentCount);
        AddRing(vh, rect, radius, straightWidth, straightHeight, perimeter, ringThickness, 0.46f, 1f, segmentCount);
    }

    private void AddRing(
        VertexHelper vh,
        Rect rect,
        float radius,
        float straightWidth,
        float straightHeight,
        float perimeter,
        float thickness,
        float baseAlpha,
        float highlightAlpha,
        int segmentCount)
    {
        float half = Mathf.Max(0.5f, thickness * 0.5f);

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1) / (float)segmentCount;
            Vector2 p0 = GetRoundedRectPoint(rect, radius, t0 * perimeter, straightWidth, straightHeight);
            Vector2 p1 = GetRoundedRectPoint(rect, radius, t1 * perimeter, straightWidth, straightHeight);
            Vector2 n0 = GetNormal(rect, radius, t0 * perimeter, straightWidth, straightHeight, perimeter);
            Vector2 n1 = GetNormal(rect, radius, t1 * perimeter, straightWidth, straightHeight, perimeter);

            Color c0 = color;
            Color c1 = color;
            c0.a *= GetAlpha(t0, baseAlpha, highlightAlpha);
            c1.a *= GetAlpha(t1, baseAlpha, highlightAlpha);

            int index = vh.currentVertCount;
            AddVertex(vh, p0 - n0 * half, c0);
            AddVertex(vh, p0 + n0 * half, c0);
            AddVertex(vh, p1 + n1 * half, c1);
            AddVertex(vh, p1 - n1 * half, c1);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }

    private float GetAlpha(float t, float baseAlpha, float highlightAlpha)
    {
        float distance = Mathf.Abs(Mathf.DeltaAngle(t * 360f, flowOffset * 360f)) / 360f;
        float highlight = Mathf.Clamp01(1f - distance / Mathf.Max(0.0001f, highlightWidth));
        highlight = Mathf.SmoothStep(0f, 1f, highlight);
        return Mathf.Clamp01(baseAlpha + highlight * highlightAlpha);
    }

    private static Vector2 GetNormal(
        Rect rect,
        float radius,
        float distance,
        float straightWidth,
        float straightHeight,
        float perimeter)
    {
        float step = Mathf.Max(0.5f, perimeter / 512f);
        Vector2 before = GetRoundedRectPoint(rect, radius, Mathf.Repeat(distance - step, perimeter), straightWidth, straightHeight);
        Vector2 after = GetRoundedRectPoint(rect, radius, Mathf.Repeat(distance + step, perimeter), straightWidth, straightHeight);
        Vector2 tangent = (after - before).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
            return Vector2.up;

        return new Vector2(-tangent.y, tangent.x);
    }

    private static Vector2 GetRoundedRectPoint(
        Rect rect,
        float radius,
        float distance,
        float straightWidth,
        float straightHeight)
    {
        float arc = Mathf.PI * 0.5f * radius;

        if (distance < straightWidth)
            return new Vector2(rect.xMin + radius + distance, rect.yMax);

        distance -= straightWidth;
        if (distance < arc)
        {
            float a = Mathf.PI * 0.5f - distance / Mathf.Max(0.0001f, radius);
            return new Vector2(rect.xMax - radius + Mathf.Cos(a) * radius, rect.yMax - radius + Mathf.Sin(a) * radius);
        }

        distance -= arc;
        if (distance < straightHeight)
            return new Vector2(rect.xMax, rect.yMax - radius - distance);

        distance -= straightHeight;
        if (distance < arc)
        {
            float a = -distance / Mathf.Max(0.0001f, radius);
            return new Vector2(rect.xMax - radius + Mathf.Cos(a) * radius, rect.yMin + radius + Mathf.Sin(a) * radius);
        }

        distance -= arc;
        if (distance < straightWidth)
            return new Vector2(rect.xMax - radius - distance, rect.yMin);

        distance -= straightWidth;
        if (distance < arc)
        {
            float a = -Mathf.PI * 0.5f - distance / Mathf.Max(0.0001f, radius);
            return new Vector2(rect.xMin + radius + Mathf.Cos(a) * radius, rect.yMin + radius + Mathf.Sin(a) * radius);
        }

        distance -= arc;
        if (distance < straightHeight)
            return new Vector2(rect.xMin, rect.yMin + radius + distance);

        distance -= straightHeight;
        float finalAngle = Mathf.PI - distance / Mathf.Max(0.0001f, radius);
        return new Vector2(rect.xMin + radius + Mathf.Cos(finalAngle) * radius, rect.yMax - radius + Mathf.Sin(finalAngle) * radius);
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vh.AddVert(vertex);
    }
}
