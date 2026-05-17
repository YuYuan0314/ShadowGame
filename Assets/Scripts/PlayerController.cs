using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRbController : MonoBehaviour
{
    [Header("引用")]
    public ShadowManager shadowManager;

    [Header("移动参数")]
    public float moveSpeed = 7f;
    public float turnSpeed = 15f;
    public float groundDrag = 5f;
    public float airDrag = 1.0f;
    public float minJumpForce = 4f;
    public float maxJumpForce = 20f;
    public float chargeTimeToMax = 1.5f;

    [Header("蓄力特效")]
    public float squishAmount = 0.4f;
    public float stretchAmount = 0.15f;
    public float shakeStrength = 0.08f;
    public float gravityMultiplier = 1.5f;
    public LayerMask groundLayer = ~0;

    [Header("阴影/暴露机制")]
    public float maxLightTime = 2f;
    public float maxFollowDistance = 6f;
    public float shadowEdgeTolerance = 0.15f;
    public float resetTransitionDuration = 0.35f;
    public float resetGracePeriod = 0.5f;
    public Vector3 shadowOffset = new Vector3(0, 0.1f, 0);

    [Header("当前状态")]
    public float currentLightTimer = 0f;

    [Header("手柄震动")]
    public float rumbleIntensity = 0.5f;

    private Rigidbody rb;
    private Transform camTransform;
    private CameraOrbit cameraOrbit;
    private bool isGrounded;

    // 阴影追踪
    private GameObject lastActiveShadowSource;
    private Vector3 lastSourcePos;
    private Vector3 lastLocalSafePos;
    private bool wasInShadowLastFrame;
    private bool hasSafePos;
    private bool exposedBySpotlight;

    // 重置动画
    private float resetGraceTimer;
    private bool isResetting;
    private bool jumpedThisFlight;
    private int outOfShadowFrames;
    private int groundedFrames;

    // 平台速度追踪：避免 MovePosition 导致的抽搐
    private Vector3 lastPlatformVelocity;

    // 蓄力跳跃
    private bool isChargingJump;
    private float jumpChargeStartTime;
    private Vector3 originalScale;
    private Tween chargeShakeTween;
    private Tween chargeScaleTween;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
            cameraOrbit = camTransform.GetComponentInParent<CameraOrbit>();
        }
    }

    void Update()
    {
        if (isResetting) return;

        // 地面检测：带 LayerMask + 帧缓冲，防止半空中误触其他物体
        bool rayHit = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.5f, groundLayer);
        if (rayHit)
            groundedFrames++;
        else
            groundedFrames = 0;
        isGrounded = groundedFrames >= 2;

        rb.drag = isGrounded ? groundDrag : airDrag;

        // 真正落地时才清除跳跃标记（Y 速度已稳定）
        if (isGrounded && Mathf.Abs(rb.velocity.y) < 0.5f)
            jumpedThisFlight = false;

        UpdateShadowLogic();

        // === 蓄力跳跃 ===
        if (isGrounded && !jumpedThisFlight)
        {
            if (Input.GetButtonDown("Jump"))
            {
                isChargingJump = true;
                jumpChargeStartTime = Time.time;
                originalScale = transform.localScale;

                // 启动屏幕抖动（通过 CameraOrbit.shakeOffset，避免被 LateUpdate 覆盖）
                if (cameraOrbit != null)
                    chargeShakeTween = DOTween.To(() => 0f, _ => { }, 1f, 99f)
                        .SetTarget(cameraOrbit);

                // 手柄震动开始
                GamepadRumble.SetVibration(0.1f, 0.05f);
            }

            if (Input.GetButton("Jump") && isChargingJump)
            {
                float chargePercent = Mathf.Clamp01((Time.time - jumpChargeStartTime) / chargeTimeToMax);

                // Y 轴压扁 + XZ 轴小幅拉伸（保持体积感）
                float targetSquishY = 1f - squishAmount * chargePercent;
                float targetStretchXZ = 1f + stretchAmount * chargePercent;
                transform.localScale = new Vector3(originalScale.x * targetStretchXZ,
                                                    originalScale.y * targetSquishY,
                                                    originalScale.z * targetStretchXZ);

                // 屏幕抖动随蓄力增强（Perlin 噪声写入 CameraOrbit.shakeOffset）
                if (cameraOrbit != null)
                {
                    float s = shakeStrength * (0.3f + 0.7f * chargePercent);
                    float sx = (Mathf.PerlinNoise(0, Time.time * 35f) - 0.5f) * 2f * s;
                    float sy = (Mathf.PerlinNoise(Time.time * 35f, 0) - 0.5f) * 2f * s;
                    cameraOrbit.shakeOffset = new Vector3(sx, sy, 0);
                }

                // 手柄震动随蓄力增强
                GamepadRumble.SetVibration(
                    Mathf.Lerp(0.1f, rumbleIntensity, chargePercent),
                    Mathf.Lerp(0.05f, rumbleIntensity * 0.5f, chargePercent));
            }

            if (Input.GetButtonUp("Jump") && isChargingJump)
            {
                float chargePercent = Mathf.Clamp01((Time.time - jumpChargeStartTime) / chargeTimeToMax);
                float launchForce = Mathf.Lerp(minJumpForce, maxJumpForce, chargePercent);

                rb.velocity = new Vector3(lastPlatformVelocity.x, 0, lastPlatformVelocity.z);
                rb.AddForce(Vector3.up * launchForce, ForceMode.Impulse);
                jumpedThisFlight = true;
                isChargingJump = false;

                StopChargeEffects();
            }
        }
        else
        {
            if (isChargingJump)
            {
                isChargingJump = false;
                StopChargeEffects();
            }
        }
    }

    // ==================== 阴影逻辑 ====================

    private void UpdateShadowLogic()
    {
        if (resetGraceTimer > 0f)
        {
            resetGraceTimer -= Time.deltaTime;
            currentLightTimer = 0f;
            wasInShadowLastFrame = true;
            return;
        }

        Vector3 checkPoint = transform.position + Vector3.up * 0.05f;
        GameObject source = shadowManager.GetShadowSource(checkPoint);
        exposedBySpotlight = SpotlightExposureZone.IsAnyPointExposed(checkPoint);

        // spotlight 照射区域覆盖阴影判定，强制视为非阴影
        bool isInShadowNow = (source != null) && !exposedBySpotlight;

        bool isInEdgeZone = false;
        if (!isInShadowNow && !exposedBySpotlight)
            isInEdgeZone = shadowManager.IsNearProjectedArea(checkPoint, shadowEdgeTolerance);

        bool isSafe = (isInShadowNow || isInEdgeZone) && !exposedBySpotlight;

        if (wasInShadowLastFrame && !isSafe)
        {
            if (!jumpedThisFlight)
            {
                outOfShadowFrames++;
                if (outOfShadowFrames > 3)
                    StripPlatformMomentum();
            }
        }
        else
        {
            outOfShadowFrames = 0;
        }

        if (isSafe)
        {
            currentLightTimer = 0f;

            if (source != null && source != lastActiveShadowSource)
            {
                lastActiveShadowSource = source;
                lastSourcePos = source.transform.position;
            }

            if (isInShadowNow && isGrounded && lastActiveShadowSource != null)
            {
                lastLocalSafePos = lastActiveShadowSource.transform.InverseTransformPoint(transform.position);
                hasSafePos = true;
            }
        }
        else
        {
            currentLightTimer += Time.deltaTime;

            bool shouldReset = false;
            if (currentLightTimer >= maxLightTime) shouldReset = true;

            // 跳跃过程中不检查距离，避免半空突然坠落
            if (!jumpedThisFlight && lastActiveShadowSource != null)
            {
                float dist = Vector3.Distance(transform.position, lastActiveShadowSource.transform.position);
                if (dist > maxFollowDistance) shouldReset = true;
            }

            if (shouldReset && !isResetting)
                ExecuteShadowReset();
        }

        wasInShadowLastFrame = isSafe;
    }

    private void StripPlatformMomentum()
    {
        // 离开阴影时清零水平速度，防止平台惯性带出阴影
        rb.velocity = new Vector3(0, rb.velocity.y, 0);
    }

    private void ExecuteShadowReset()
    {
        if (isResetting) return;
        isResetting = true;

        rb.velocity = Vector3.zero;
        currentLightTimer = 0f;

        Vector3 targetPos = DetermineResetTarget();
        float dist = Vector3.Distance(transform.position, targetPos);

        if (dist < 0.3f)
        {
            transform.position = targetPos;
            OnResetComplete();
        }
        else
        {
            transform.DOMove(targetPos, resetTransitionDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(OnResetComplete);

            transform.DOPunchScale(Vector3.one * 0.2f, resetTransitionDuration, 1, 0.5f);
        }
    }

    private Vector3 DetermineResetTarget()
    {
        // 优先：传送到 fallback 物体投影阴影的重心（保证在阴影正中央）
        if (lastActiveShadowSource != null)
        {
            var mover = lastActiveShadowSource.GetComponent<IShadowMover>();
            GameObject fallback = (mover != null) ? mover.GetFallbackTarget() : null;

            if (fallback != null && fallback.activeInHierarchy)
            {
                Vector3 safePos = shadowManager.GetSafePositionInShadow(fallback);
                if (safePos != Vector3.zero)
                    return safePos + shadowOffset;
            }
        }

        // 其次：返回上次存档的本地安全坐标
        if (hasSafePos && lastActiveShadowSource != null)
            return lastActiveShadowSource.transform.TransformPoint(lastLocalSafePos) + shadowOffset;

        // 兜底
        return transform.position + Vector3.up * 2f;
    }

    private void OnResetComplete()
    {
        isResetting = false;
        resetGraceTimer = resetGracePeriod;

        // 先检测当前实际所在的 shadow source，判断回弹到的是 fallback 还是原平台
        Vector3 checkPoint = transform.position + Vector3.up * 0.05f;
        GameObject actualSource = shadowManager.GetShadowSource(checkPoint);
        bool switchedSource = actualSource != null && actualSource != lastActiveShadowSource;

        if (switchedSource)
        {
            // 回弹到了 fallback 目标 → 切换跟踪源，不碰位置
            lastActiveShadowSource = actualSource;
            hasSafePos = false;
            lastLocalSafePos = Vector3.zero;
        }
        else if (hasSafePos && lastActiveShadowSource != null)
        {
            // 回弹回原移动平台 → 用平台当前位置重算，弥补 DOTween 期间平台的位移
            Vector3 currentSafePos = lastActiveShadowSource.transform.TransformPoint(lastLocalSafePos) + shadowOffset;
            transform.position = currentSafePos;
        }

        // 同步 Rigidbody 位置与速度清零
        rb.position = transform.position;
        rb.velocity = Vector3.zero;
        lastPlatformVelocity = Vector3.zero;

        if (lastActiveShadowSource != null)
            lastSourcePos = lastActiveShadowSource.transform.position;

        currentLightTimer = 0f;
        wasInShadowLastFrame = true;
    }

    // ==================== 移动物理 ====================

    void FixedUpdate()
    {
        if (isResetting) return;
        HandleMovement();
        ApplyExtraGravity();
    }

    private void ApplyExtraGravity()
    {
        if (!isGrounded && gravityMultiplier > 1f)
        {
            float extra = Physics.gravity.y * (gravityMultiplier - 1f);
            rb.AddForce(new Vector3(0, extra, 0), ForceMode.Acceleration);
        }
    }

    private void HandleMovement()
    {
        if (camTransform == null)
        {
            if (Camera.main != null) camTransform = Camera.main.transform;
            if (camTransform == null) return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        // === 平台速度追踪（用速度替代 MovePosition，避免抽搐） ===
        Vector3 platformVelocity = Vector3.zero;
        if (wasInShadowLastFrame && lastActiveShadowSource != null)
        {
            Vector3 currentSrcPos = lastActiveShadowSource.transform.position;
            Vector3 displacement = currentSrcPos - lastSourcePos;
            lastSourcePos = currentSrcPos;

            if (displacement.magnitude < 1f)
                platformVelocity = displacement / Time.fixedDeltaTime;
        }
        lastPlatformVelocity = platformVelocity;

        // === 旋转 ===
        if (moveDir.magnitude > 0.1f)
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(moveDir), Time.fixedDeltaTime * turnSpeed));

        // === 玩家输入移动 ===
        if (isGrounded)
        {
            if (moveDir.magnitude > 0.1f)
            {
                Vector3 nextPos = transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime);
                bool atShadowEdge = !shadowManager.IsInProjectedArea(nextPos) || exposedBySpotlight;
                bool isJumping = rb.velocity.y > 0.5f;

                if (atShadowEdge && !isJumping)
                {
                    // 阴影边缘：只保留平台速度，禁止玩家自主移动
                    rb.velocity = new Vector3(platformVelocity.x, rb.velocity.y, platformVelocity.z);
                }
                else
                {
                    // 目标速度 = 平台速度 + 玩家输入速度
                    Vector3 targetVel = platformVelocity + moveDir * moveSpeed;
                    Vector3 currentHoriz = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                    Vector3 desiredHoriz = new Vector3(targetVel.x, 0, targetVel.z);
                    rb.AddForce((desiredHoriz - currentHoriz) * 10f, ForceMode.Force);
                }
            }
            else
            {
                // 无输入：跟随平台速度
                rb.velocity = new Vector3(platformVelocity.x, rb.velocity.y, platformVelocity.z);
            }
        }
        else
        {
            if (moveDir.magnitude > 0.1f && !exposedBySpotlight)
                rb.AddForce(moveDir * moveSpeed * 5f, ForceMode.Force);
        }

        // === 速度限制 ===
        Vector3 horizVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float limit = isGrounded ? moveSpeed * 1.5f : moveSpeed * 1.2f;

        if (horizVel.magnitude > limit)
        {
            horizVel = horizVel.normalized * limit;
            rb.velocity = new Vector3(horizVel.x, rb.velocity.y, horizVel.z);
        }
    }

    private void StopChargeEffects()
    {
        // 停止屏幕抖动
        if (chargeShakeTween != null && chargeShakeTween.IsActive())
            chargeShakeTween.Kill();
        chargeShakeTween = null;

        // 清除 CameraOrbit 的抖动偏移
        if (cameraOrbit != null)
            cameraOrbit.shakeOffset = Vector3.zero;

        // 停止手柄震动
        GamepadRumble.Stop();

        // 缩放弹回原状（带弹性）
        chargeScaleTween = transform.DOScale(originalScale, 0.25f).SetEase(Ease.OutBack);
    }

    void OnDestroy()
    {
        DOTween.Kill(transform);
        if (cameraOrbit != null)
            cameraOrbit.shakeOffset = Vector3.zero;
        GamepadRumble.Stop();
    }
}
