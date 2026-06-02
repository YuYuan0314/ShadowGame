using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectoryPreview : MonoBehaviour
{
    [Header("References")]
    public PlayerRbController player;

    [Header("Simulation")]
    [Tooltip("Simulation step. -1 uses Time.fixedDeltaTime.")]
    public float timeStep = -1f;
    public float maxSimTime = 3f;
    [Tooltip("Optional preview-only height cap. 0 disables it.")]
    public float maxHeight = 0f;
    [Tooltip("Air horizontal speed limit multiplier used by the preview.")]
    public float airSpeedLimitMult = 1.6f;
    [Tooltip("Maximum sampled points.")]
    public int maxPoints = 100;
    public float pointMinSpacing = 0.1f;

    [Header("Preview Calibration")]
    [Tooltip("Preview-only multiplier for scripted jump velocity. Does not affect real jump.")]
    public float previewJumpVelocityScale = 1f;
    [Tooltip("Preview-only multiplier for air control acceleration. Does not affect real movement.")]
    public float previewAirAccelScale = 1.6f;
    [Tooltip("Preview-only multiplier for air horizontal speed cap. Does not affect real movement.")]
    public float previewAirSpeedLimitScale = 1.4f;
    [Tooltip("Preview-only horizontal distance multiplier. Does not affect real movement.")]
    public float previewHorizontalDistanceScale = 1.15f;
    [Tooltip("Keep simulating the last pressed charge direction as if the player keeps holding it until landing.")]
    public bool lockLastChargeDirection = true;

    [Header("Landing")]
    public LayerMask groundLayer = ~0;
    [Tooltip("Distance from player transform center to collider bottom.")]
    public float colliderBottomOffset = 0.5f;

    [Header("Trajectory Line")]
    public float scrollSpeed = 4f;
    public Color lineColor = new Color(1f, 0.95f, 0.5f, 0.7f);
    public float startWidth = 0.06f;
    public float endWidth = 0.02f;
    public float dotDensity = 50f;

    [Header("Landing Ring")]
    public float ringRadius = 0.35f;
    public float ringWidth = 0.08f;
    public Color ringColor = new Color(1f, 0.95f, 0.5f, 0.9f);
    public int ringSegments = 40;

    private LineRenderer lr;
    private Material lineMat;
    private Vector3[] points;
    private Transform camTransform;

    private GameObject ringObj;
    private LineRenderer ringLr;
    private Material ringMat;
    private Vector3 lockedChargeMoveDir;
    private bool hasLockedChargeMoveDir;

    void Awake()
    {
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
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;

        points = new Vector3[Mathf.Max(2, maxPoints)];

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
        ringLr.positionCount = Mathf.Max(8, ringSegments);

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

        Vector3 moveDir = ResolvePreviewMoveDirection();
        bool shouldShow = player.IsChargingJump;
        lr.enabled = shouldShow;
        ringLr.enabled = shouldShow;

        if (!shouldShow)
            return;

        if (points == null || points.Length != Mathf.Max(2, maxPoints))
            points = new Vector3[Mathf.Max(2, maxPoints)];

        float step = timeStep > 0f ? timeStep : Time.fixedDeltaTime;
        Vector3 landingPoint = UpdateTrajectory(player.transform.position, moveDir, player.ChargePercent, step);

        lineMat.mainTextureScale = new Vector2(dotDensity, 1f);
        Vector2 offset = lineMat.mainTextureOffset;
        offset.x += scrollSpeed * Time.deltaTime;
        lineMat.mainTextureOffset = offset;

        UpdateLandingRing(landingPoint);
    }

    private Vector3 ResolvePreviewMoveDirection()
    {
        if (!player.IsChargingJump)
        {
            hasLockedChargeMoveDir = false;
            lockedChargeMoveDir = Vector3.zero;
            return Vector3.zero;
        }

        Vector3 currentMoveDir = ReadCameraRelativeMoveDirection();
        if (currentMoveDir.sqrMagnitude > 0.01f)
        {
            lockedChargeMoveDir = currentMoveDir;
            hasLockedChargeMoveDir = true;
        }

        if (lockLastChargeDirection && hasLockedChargeMoveDir)
            return lockedChargeMoveDir;

        return currentMoveDir;
    }

    private Vector3 ReadCameraRelativeMoveDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 camForward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
        return (camForward * v + camRight * h).normalized;
    }

    private Vector3 UpdateTrajectory(Vector3 startPos, Vector3 moveDir, float chargePercent, float step)
    {
        float scriptedJumpVelocity = Mathf.Lerp(player.minJumpForce, player.maxJumpForce, chargePercent)
            * Mathf.Max(0f, previewJumpVelocityScale);

        if (maxHeight > 0f)
        {
            float cappedVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * maxHeight);
            scriptedJumpVelocity = Mathf.Min(scriptedJumpVelocity, cappedVelocity);
        }

        Vector3 pos = startPos;
        Vector3 vel = Vector3.zero;
        float horizontalDistanceScale = Mathf.Max(0f, previewHorizontalDistanceScale);
        Vector3 airAcceleration = moveDir.sqrMagnitude > 0.01f
            ? moveDir * (player.moveSpeed * 5f * Mathf.Max(0f, previewAirAccelScale) * horizontalDistanceScale)
            : Vector3.zero;

        float duration = Mathf.Max(0.01f, player.jumpForceDuration);
        float elapsedJumpForce = 0f;
        float remainingJumpVelocity = scriptedJumpVelocity;
        float curveArea = EstimateJumpForceCurveArea(player.jumpForceCurve);

        int count = 0;
        float simTime = 0f;
        bool hasLeftGround = false;
        float distSinceLast = pointMinSpacing;
        Vector3 lastRecorded = pos;

        while (count < points.Length && simTime < maxSimTime)
        {
            Vector3 previousPos = pos;
            bool applyingJumpForce = elapsedJumpForce < duration && remainingJumpVelocity > 0.001f;

            if (applyingJumpForce)
            {
                float t = Mathf.Clamp01(elapsedJumpForce / duration);
                float curveValue = player.jumpForceCurve != null ? Mathf.Max(0f, player.jumpForceCurve.Evaluate(t)) : 1f;
                float velocityThisStep = scriptedJumpVelocity * curveValue * step / (duration * curveArea);
                velocityThisStep = Mathf.Min(velocityThisStep, remainingJumpVelocity);

                vel += Vector3.up * velocityThisStep;
                remainingJumpVelocity -= velocityThisStep;
                elapsedJumpForce += step;
                applyingJumpForce = elapsedJumpForce < duration && remainingJumpVelocity > 0.001f;
            }

            vel += Physics.gravity * step;

            if (!applyingJumpForce && vel.y < 0f && player.fallGravityMultiplier > 1f)
                vel += Physics.gravity * (player.fallGravityMultiplier - 1f) * step;

            if (airAcceleration.sqrMagnitude > 0f)
                vel += airAcceleration * step;

            Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
            float airLimit = player.moveSpeed * airSpeedLimitMult * Mathf.Max(0f, previewAirSpeedLimitScale) * horizontalDistanceScale;
            if (airLimit > 0f && horizVel.magnitude > airLimit)
            {
                horizVel = horizVel.normalized * airLimit;
                vel = new Vector3(horizVel.x, vel.y, horizVel.z);
            }

            pos += vel * step;
            simTime += step;

            if (!hasLeftGround && pos.y > startPos.y + 0.05f)
                hasLeftGround = true;

            if (hasLeftGround && vel.y < 0f && TryFindLandingPoint(previousPos, pos, out Vector3 landingCenter, out Vector3 contactPoint))
            {
                pos = landingCenter;
                points[count++] = contactPoint;
                break;
            }

            distSinceLast += Vector3.Distance(lastRecorded, pos);
            if (distSinceLast >= Mathf.Max(0.001f, pointMinSpacing))
            {
                points[count++] = pos;
                distSinceLast = 0f;
                lastRecorded = pos;
            }
        }

        lr.positionCount = count;
        for (int i = 0; i < count; i++)
            lr.SetPosition(i, points[i]);

        return count > 0 ? points[count - 1] : startPos;
    }

    private bool TryFindLandingPoint(Vector3 previousCenter, Vector3 currentCenter, out Vector3 landingCenter, out Vector3 contactPoint)
    {
        landingCenter = currentCenter;
        contactPoint = currentCenter + Vector3.down * colliderBottomOffset;

        Vector3 previousBottom = previousCenter + Vector3.down * colliderBottomOffset;
        Vector3 currentBottom = currentCenter + Vector3.down * colliderBottomOffset;
        Vector3 sweep = currentBottom - previousBottom;
        float sweepDistance = sweep.magnitude;

        if (sweepDistance <= 0.0001f)
            return false;

        if (Physics.Raycast(previousBottom, sweep.normalized, out RaycastHit hit, sweepDistance, groundLayer))
        {
            contactPoint = hit.point;
            landingCenter = hit.point + Vector3.up * colliderBottomOffset;
            return true;
        }

        return false;
    }

    private float EstimateJumpForceCurveArea(AnimationCurve curve)
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

    private void UpdateLandingRing(Vector3 center)
    {
        int segments = Mathf.Max(8, ringSegments);
        ringLr.positionCount = segments;
        Vector3 ringCenter = center + Vector3.up * 0.03f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * ringRadius;
            float z = Mathf.Sin(angle) * ringRadius;
            ringLr.SetPosition(i, new Vector3(ringCenter.x + x, ringCenter.y, ringCenter.z + z));
        }
    }

    private Texture2D CreateDotTexture()
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



