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
    [Tooltip("曲线上采样点最大数量")]
    public int maxPoints = 100;
    [Tooltip("采样点最小间距")]
    public float pointMinSpacing = 0.1f;

    [Header("落地校准")]
    [Tooltip("地面检测层")]
    public LayerMask groundLayer = ~0;
    [Tooltip("落点高度偏移：角色碰撞体底部到 transform 中心的距离。胶囊体 = halfHeight - radius。默认 0.5，可在 Play 模式下看着落点微调")]
    public float colliderBottomOffset = 0.5f;

    [Header("视觉效果")]
    public float scrollSpeed = 4f;
    public Color lineColor = new Color(1f, 0.95f, 0.5f, 0.7f);
    public float startWidth = 0.06f;
    public float endWidth = 0.02f;

    private LineRenderer lr;
    private Material lineMat;
    private Vector3[] points;
    private Transform camTransform;

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
        lineMat.mainTextureScale = new Vector2(25f, 1f);
        lineMat.color = lineColor;
        lr.material = lineMat;
        lr.textureMode = LineTextureMode.Tile;
        lr.colorGradient = CreateAlphaGradient();

        lr.startWidth = startWidth;
        lr.endWidth = endWidth;

        points = new Vector3[maxPoints];
    }

    void OnDestroy()
    {
        if (lineMat != null)
            Destroy(lineMat);
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

        if (!shouldShow)
        {
            lr.enabled = false;
            return;
        }

        lr.enabled = true;

        float step = timeStep > 0f ? timeStep : Time.fixedDeltaTime;
        UpdateTrajectory(player.transform.position, moveDir, player.ChargePercent,
            player.PlatformVelocity, step);

        Vector2 offset = lineMat.mainTextureOffset;
        offset.x += scrollSpeed * Time.deltaTime;
        lineMat.mainTextureOffset = offset;
    }

    void UpdateTrajectory(Vector3 startPos, Vector3 moveDir, float chargePercent,
        Vector3 platformVel, float step)
    {
        float launchForce = Mathf.Lerp(player.minJumpForce, player.maxJumpForce, chargePercent);

        // 限制抛物线最高点：h = v²/(2g) → v = sqrt(2·|g|·h)，取 min(实际初速, 上限初速)
        if (maxHeight > 0f)
        {
            float g = Mathf.Abs(Physics.gravity.y * player.gravityMultiplier);
            float cappedLaunch = Mathf.Sqrt(2f * g * maxHeight);
            launchForce = Mathf.Min(launchForce, cappedLaunch);
        }

        // 射线检测实际地面高度
        float groundY = startPos.y - colliderBottomOffset; // 兜底
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, 5f, groundLayer))
            groundY = hit.point.y;

        // 角色落地时 transform 中心的目标 Y = 地面 + 碰撞体底部偏移
        float landingCenterY = groundY + colliderBottomOffset;

        // 初始速度（和 PlayerRbController 完全一致）
        // rb.velocity = (platformVel.x, 0, platformVel.z); rb.AddForce(Vector3.up * launch, Impulse)
        Vector3 vel = new Vector3(platformVel.x, launchForce, platformVel.z);

        // 加速度（和 PlayerRbController 完全一致）
        Vector3 gravityAccel = new Vector3(0f, Physics.gravity.y * player.gravityMultiplier, 0f);
        Vector3 airAccel = moveDir * (player.moveSpeed * 5f); // ForceMode.Force, mass=1
        Vector3 totalAccel = gravityAccel + airAccel;

        Vector3 pos = startPos;
        int count = 0;
        float simTime = 0f;
        float distSinceLast = 0f;
        Vector3 lastRecorded = pos;

        // 不记录起点，从第一帧模拟后的位置开始
        // 这样线从玩家前方出发，更直观
        Vector3 prevPos = pos;

        while (count < maxPoints && simTime < maxSimTime)
        {
            vel += totalAccel * step;

            // 水平速度限制（和 PlayerController 一致：空中上限 moveSpeed * 1.2f）
            Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
            float airLimit = player.moveSpeed * airSpeedLimitMult;
            if (horizVel.magnitude > airLimit)
            {
                horizVel = horizVel.normalized * airLimit;
                vel = new Vector3(horizVel.x, vel.y, horizVel.z);
            }

            pos += vel * step;
            simTime += step;

            // 落地检测：只在下落时判断 (vel.y < 0)，防止第一帧就误判落地
            if (vel.y < 0f && pos.y <= landingCenterY)
            {
                float prevY = prevPos.y;
                float deltaY = pos.y - prevY;
                float t = deltaY != 0f ? (landingCenterY - prevY) / deltaY : 0f;
                t = Mathf.Clamp01(t);
                pos = Vector3.Lerp(prevPos, pos, t);
                pos.y = landingCenterY;
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

            prevPos = pos;
        }

        lr.positionCount = count;
        for (int i = 0; i < count; i++)
            lr.SetPosition(i, points[i]);
    }

    Texture2D CreateDotTexture()
    {
        int w = 32, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var colors = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            Color32 c = x < 6 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                colors[y * w + x] = c;
        }
        tex.SetPixels32(colors);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    Gradient CreateAlphaGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(lineColor, 0f),
                new GradientColorKey(lineColor, 0.8f),
                new GradientColorKey(new Color(lineColor.r, lineColor.g, lineColor.b, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(lineColor.a, 0f),
                new GradientAlphaKey(lineColor.a * 0.5f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return g;
    }
}
