using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectoryPreview : MonoBehaviour
{
    [Header("引用")]
    public PlayerRbController player;

    [Header("轨迹模拟")]
    [Tooltip("物理模拟步长，默认 FixedDeltaTime (0.02s)，与实际 FixedUpdate 一致")]
    public float timeStep = -1f;
    [Tooltip("最大模拟时间（秒）")]
    public float maxSimTime = 3f;
    [Tooltip("抛物线最高点限制（相对起跳点的高度）。0 = 不限制")]
    public float maxHeight = 0f;
    [Tooltip("空中水平速度上限倍率，相对 moveSpeed。默认 1.2 和 PlayerController 一致")]
    public float airSpeedLimitMult = 1.2f;
    [Header("预览校准")]
    [Tooltip("只影响预览线的起跳上升速度倍率，不影响真实跳跃。预览太高就调低。")]
    public float previewJumpVelocityScale = 0.85f;
    [Tooltip("只影响预览线的空中横向加速倍率，不影响真实移动。预览太远就调低。")]
    public float previewAirAccelScale = 0.75f;
    [Tooltip("只影响预览线的空中水平速度上限倍率，不影响真实移动。预览太远就调低。")]
    public float previewAirSpeedLimitScale = 0.9f;
    [Tooltip("曲线上采样点最大数量")]
    public int maxPoints = 100;
    [Tooltip("采样点最小间距")]
    public float pointMinSpacing = 0.1f;

    [Header("落地校准")]
    [Tooltip("地面检测层")]
    public LayerMask groundLayer = ~0;
    [Tooltip("落点高度偏移：角色碰撞体底部到 transform 中心的距离。胶囊体 = halfHeight - radius。默认 0.5，可在 Play 模式下看着落点微调")]
    public float colliderBottomOffset = 0.5f;

    [Header("轨迹线视觉")]
    public float scrollSpeed = 4f;
    public Color lineColor = new Color(1f, 0.95f, 0.5f, 0.7f);
    public float startWidth = 0.06f;
    public float endWidth = 0.02f;
    [Tooltip("虚线密度，值越大点越密（默认 50）")]
    public float dotDensity = 50f;

    [Header("落点圆环")]
    [Tooltip("落点环半径")]
    public float ringRadius = 0.35f;
    [Tooltip("落点环宽度")]
    public float ringWidth = 0.08f;
    [Tooltip("落点环颜色")]
    public Color ringColor = new Color(1f, 0.95f, 0.5f, 0.9f);
    [Tooltip("落点环分段数（越大越圆）")]
    public int ringSegments = 40;

    private LineRenderer lr;
    private Material lineMat;
    private Vector3[] points;
    private Transform camTransform;

    // 落点环
    private GameObject ringObj;
    private LineRenderer ringLr;
    private Material ringMat;

    void Awake()
    {
        // === 轨迹线 ===
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.enabled = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.allowOcclusionWhenDynamic = false;

        lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.SetTexture("_MainTex", CreateDotTexture());
        lineMat.mainTextureScale = new Vector2(dotDensity, 1f);
        lineMat.color = lineColor;
        lr.material = lineMat;
        lr.textureMode = LineTextureMode.Tile;

        // 统一颜色，不做渐变
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;

        points = new Vector3[maxPoints];

        // === 落点圆环 ===
        ringObj = new GameObject("LandingRing");
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;
        ringObj.hideFlags = HideFlags.HideAndDontSave;

        ringLr = ringObj.AddComponent<LineRenderer>();
        ringLr.useWorldSpace = true;
        ringLr.enabled = false;
        ringLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringLr.receiveShadows = false;
        ringLr.loop = true;
        ringLr.positionCount = ringSegments;

        ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.SetTexture("_MainTex", Texture2D.whiteTexture);
        ringMat.color = ringColor;
        ringLr.material = ringMat;
        ringLr.startColor = ringColor;
        ringLr.endColor = ringColor;
        ringLr.startWidth = ringWidth;
        ringLr.endWidth = ringWidth;
    }

    void OnDestroy()
    {
        if (lineMat != null) Destroy(lineMat);
        if (ringMat != null) Destroy(ringMat);
        if (ringObj != null) Destroy(ringObj);
    }

    void Update()
    {
        if (camTransform == null)
        {
            if (Camera.main != null)
                camTransform = Camera.main.transform;
            else
                return;
        }

        if (player == null)
            return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 camForward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        bool hasDirection = moveDir.magnitude > 0.1f;
        bool shouldShow = player.IsChargingJump && hasDirection;

        lr.enabled = shouldShow;
        ringLr.enabled = shouldShow;

        if (!shouldShow)
            return;

        float step = timeStep > 0f ? timeStep : Time.fixedDeltaTime;
        Vector3 landingPoint = UpdateTrajectory(player.transform.position, moveDir,
            player.ChargePercent, player.PlatformVelocity, step);

        // 滚动虚线
        lineMat.mainTextureScale = new Vector2(dotDensity, 1f);
        Vector2 offset = lineMat.mainTextureOffset;
        offset.x += scrollSpeed * Time.deltaTime;
        lineMat.mainTextureOffset = offset;

        // 落点圆环
        UpdateLandingRing(landingPoint);
    }

    Vector3 UpdateTrajectory(Vector3 startPos, Vector3 moveDir, float chargePercent,
        Vector3 platformVel, float step)
    {
        float jumpVelocity = Mathf.Lerp(player.minJumpForce, player.maxJumpForce, chargePercent)
            * Mathf.Max(0f, previewJumpVelocityScale);

        if (maxHeight > 0f)
        {
            float cappedLaunch = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * maxHeight);
            jumpVelocity = Mathf.Min(jumpVelocity, cappedLaunch);
        }

        Vector3 vel = new Vector3(platformVel.x, 0f, platformVel.z);
        Vector3 airAccel = moveDir * (player.moveSpeed * 5f * Mathf.Max(0f, previewAirAccelScale));

        Vector3 pos = startPos;
        int count = 0;
        float simTime = 0f;
        bool hasLeftGround = false;
        float jumpForceElapsed = 0f;
        float jumpForceRemainingVelocity = jumpVelocity;
        float jumpForceDuration = Mathf.Max(0.01f, player.jumpForceDuration);
        float jumpForceCurveArea = EstimateJumpForceCurveArea(player.jumpForceCurve);
        float distSinceLast = 0f;
        Vector3 lastRecorded = pos;
        Vector3 prevPos = pos;

        while (count < maxPoints && simTime < maxSimTime)
        {
            prevPos = pos;

            bool applyingJumpForce = jumpForceElapsed < jumpForceDuration && jumpForceRemainingVelocity > 0.001f;
            if (applyingJumpForce)
            {
                float t = Mathf.Clamp01(jumpForceElapsed / jumpForceDuration);
                float curveValue = player.jumpForceCurve != null ? Mathf.Max(0f, player.jumpForceCurve.Evaluate(t)) : 1f;
                float velocityThisStep = jumpVelocity * curveValue * step / (jumpForceDuration * jumpForceCurveArea);
                velocityThisStep = Mathf.Min(velocityThisStep, jumpForceRemainingVelocity);

                vel += Vector3.up * velocityThisStep;
                jumpForceRemainingVelocity -= velocityThisStep;
                jumpForceElapsed += step;
            }

            vel += airAccel * step;
            vel += Physics.gravity * step;

            applyingJumpForce = jumpForceElapsed < jumpForceDuration && jumpForceRemainingVelocity > 0.001f;
            if (!applyingJumpForce && vel.y < 0f && player.fallGravityMultiplier > 1f)
                vel += Physics.gravity * (player.fallGravityMultiplier - 1f) * step;

            Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
            float airLimit = player.moveSpeed * airSpeedLimitMult * Mathf.Max(0f, previewAirSpeedLimitScale);
            if (horizVel.magnitude > airLimit)
            {
                horizVel = horizVel.normalized * airLimit;
                vel = new Vector3(horizVel.x, vel.y, horizVel.z);
            }

            pos += vel * step;
            simTime += step;

            if (!hasLeftGround && pos.y > startPos.y + 0.05f)
                hasLeftGround = true;

            if (hasLeftGround && vel.y < 0f && TryFindLandingPoint(prevPos, pos, out Vector3 landingPoint))
            {
                pos = landingPoint;
                points[count++] = pos;
                break;
            }

            distSinceLast += Vector3.Distance(lastRecorded, pos);
            if (distSinceLast >= pointMinSpacing)
            {
                points[count++] = pos;
                distSinceLast = 0f;
                lastRecorded = pos;
            }
        }

        lr.positionCount = count;
        for (int i = 0; i < count; i++)
            lr.SetPosition(i, points[i]);

        // 返回落点（最后记录的点）
        return count > 0 ? points[count - 1] : startPos;
    }

    bool TryFindLandingPoint(Vector3 previousCenter, Vector3 currentCenter, out Vector3 landingCenter)
    {
        landingCenter = currentCenter;

        Vector3 previousBottom = previousCenter + Vector3.down * colliderBottomOffset;
        Vector3 currentBottom = currentCenter + Vector3.down * colliderBottomOffset;
        Vector3 sweep = currentBottom - previousBottom;
        float sweepDistance = sweep.magnitude;

        if (sweepDistance <= 0.0001f)
            return false;

        if (Physics.Raycast(previousBottom, sweep.normalized, out RaycastHit hit, sweepDistance, groundLayer))
        {
            landingCenter = hit.point + Vector3.up * colliderBottomOffset;
            return true;
        }

        return false;
    }

    float EstimateJumpForceCurveArea(AnimationCurve curve)
    {
        if (curve == null)
            return 1f;

        const int samples = 12;
        float area = 0f;
        float previous = Mathf.Max(0f, curve.Evaluate(0f));
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float current = Mathf.Max(0f, curve.Evaluate(t));
            area += (previous + current) * 0.5f / samples;
            previous = current;
        }

        return Mathf.Max(0.01f, area);
    }

    void UpdateLandingRing(Vector3 center)
    {
        ringLr.positionCount = ringSegments;

        Vector3 ringCenter = center + Vector3.up * 0.03f; // 微微抬高避免 Z-fighting

        for (int i = 0; i < ringSegments; i++)
        {
            float angle = (float)i / ringSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * ringRadius;
            float z = Mathf.Sin(angle) * ringRadius;
            ringLr.SetPosition(i, new Vector3(ringCenter.x + x, ringCenter.y, ringCenter.z + z));
        }
    }

    Texture2D CreateDotTexture()
    {
        int w = 32, h = 4;
        int dotWidth = 10;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var colors = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            Color32 c = x < dotWidth ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                colors[y * w + x] = c;
        }
        tex.SetPixels32(colors);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }
}
