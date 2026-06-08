using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRbController : MonoBehaviour
{
    [Header("References")]
    public ShadowManager shadowManager;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float turnSpeed = 15f;
    public float groundDrag = 5f;
    public float airDrag = 1.0f;
    public float minJumpForce = 4f;
    public float maxJumpForce = 20f;
    public float chargeTimeToMax = 1.5f;
    [Tooltip("Charge response curve. Default is fast at first, then slower near full charge.")]
    public AnimationCurve chargeCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("Jump Drive")]
    [Tooltip("How long the scripted upward jump force is applied after releasing Jump.")]
    public float jumpForceDuration = 0.14f;
    [Tooltip("Shape of the scripted jump force. The total applied velocity is normalized to the charge amount.")]
    public AnimationCurve jumpForceCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Fall Tuning")]
    [Tooltip("Extra gravity multiplier applied only while falling after the scripted jump force is done.")]
    public float fallGravityMultiplier = 2.5f;

    [Header("Charge Effects")]
    public float squishAmount = 0.4f;
    public float stretchAmount = 0.15f;
    public float shakeStrength = 0.08f;
    public float gravityMultiplier = 1.5f;
    public LayerMask groundLayer = ~0;

    [Header("Shadow Exposure")]
    public float maxLightTime = 2f;
    public float maxFollowDistance = 6f;
    public float shadowEdgeTolerance = 0.15f;
    public float resetTransitionDuration = 0.35f;
    public float resetGracePeriod = 0.5f;
    public Vector3 shadowOffset = new Vector3(0, 0.1f, 0);

    [Header("Outside Shadow Movement")]
    [Range(0f, 1f)] public float outsideShadowMoveMultiplier = 0.1f;

    [Header("Moving Shadow")]
    [Tooltip("If enabled, the player inherits velocity from the object casting the current shadow.")]
    public bool followMovingShadowSource = false;

    [Header("Runtime State")]
    public float currentLightTimer = 0f;

    [Header("Rumble")]
    public float rumbleIntensity = 0.5f;

    public Animator mouseAnimator;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IdleStateHash = Animator.StringToHash("idle");
    private static readonly int WalkStateHash = Animator.StringToHash("walk");
    private static readonly int JumpStateHash = Animator.StringToHash("jump");

    private Rigidbody rb;
    private Transform camTransform;
    private CameraOrbit cameraOrbit;
    private bool isGrounded;

    // 闃村奖杩借釜
    private GameObject lastActiveShadowSource;
    private Vector3 lastSourcePos;
    private Vector3 lastLocalSafePos;
    private bool wasInShadowLastFrame;
    private bool hasSafePos;
    private bool exposedBySpotlight;

    // 閲嶇疆鍔ㄧ敾
    private float resetGraceTimer;
    private bool isResetting;
    private bool jumpedThisFlight;
    private int outOfShadowFrames;
    private int groundedFrames;
    private GameObject pendingResetShadowSource;
    private Vector3 pendingResetTargetPosition;

    // Moving shadow velocity. Disabled by default so shadows do not carry the player.
    private Vector3 lastPlatformVelocity;

    // 钃勫姏璺宠穬
    private bool isChargingJump;
    private float jumpChargeStartTime;
    private bool isApplyingJumpForce;
    private float jumpForceElapsed;
    private float jumpForceTotalVelocity;
    private float jumpForceRemainingVelocity;
    private float jumpForceCurveArea;

    // 鍏紑鍙鐘舵€?(渚?JumpTrajectoryPreview 绛夌粍浠惰鍙?
    public bool IsChargingJump => isChargingJump;
    public float ChargePercent => isChargingJump ? EvaluateChargePercent() : 0f;
    public Vector3 PlatformVelocity => lastPlatformVelocity;
    private Vector3 originalScale;
    private Tween chargeShakeTween;
    private Tween chargeScaleTween;
    private int currentMouseAnimationHash;

    private float EvaluateChargePercent()
    {
        float rawPercent = Mathf.Clamp01((Time.time - jumpChargeStartTime) / Mathf.Max(0.01f, chargeTimeToMax));
        return chargeCurve != null ? Mathf.Clamp01(chargeCurve.Evaluate(rawPercent)) : rawPercent;
    }


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        ResolveShadowManager();

        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
            cameraOrbit = camTransform.GetComponentInParent<CameraOrbit>();
        }

        if (mouseAnimator == null)
        {
            GameObject mouseModel = GameObject.Find("榧犻紶妯″瀷");
            if (mouseModel != null)
                mouseAnimator = mouseModel.GetComponent<Animator>();
        }
    }

    void Update()
    {
        if (isResetting) return;

        // 鍦伴潰妫€娴嬶細甯?LayerMask + 甯х紦鍐诧紝闃叉鍗婄┖涓瑙﹀叾浠栫墿浣?
        bool rayHit = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.5f, groundLayer);
        if (rayHit)
            groundedFrames++;
        else
            groundedFrames = 0;
        isGrounded = groundedFrames >= 2;

        rb.drag = isGrounded ? groundDrag : airDrag;

        // 鐪熸钀藉湴鏃舵墠娓呴櫎璺宠穬鏍囪锛圷 閫熷害宸茬ǔ瀹氾級
        if (isGrounded && !isApplyingJumpForce && Mathf.Abs(rb.velocity.y) < 0.5f)
            jumpedThisFlight = false;

        UpdateShadowLogic();
        UpdateMouseAnimation();

        // === 钃勫姏璺宠穬 ===
        if (isGrounded && !jumpedThisFlight)
        {
            if (Input.GetButtonDown("Jump"))
            {
                isChargingJump = true;
                jumpChargeStartTime = Time.time;
                originalScale = transform.localScale;

                // 鍚姩灞忓箷鎶栧姩锛堥€氳繃 CameraOrbit.shakeOffset锛岄伩鍏嶈 LateUpdate 瑕嗙洊锛?
                if (cameraOrbit != null && !ShouldSuppressChargeScreenShake())
                    chargeShakeTween = DOTween.To(() => 0f, _ => { }, 1f, 99f)
                        .SetTarget(cameraOrbit);

                // 鎵嬫焺闇囧姩寮€濮?
                GamepadRumble.SetVibration(0.1f, 0.05f);
            }

            if (Input.GetButton("Jump") && isChargingJump)
            {
                float chargePercent = EvaluateChargePercent();

                // Y 杞村帇鎵?+ XZ 杞村皬骞呮媺浼革紙淇濇寔浣撶Н鎰燂級
                float targetSquishY = 1f - squishAmount * chargePercent;
                float targetStretchXZ = 1f + stretchAmount * chargePercent;
                transform.localScale = new Vector3(originalScale.x * targetStretchXZ,
                                                    originalScale.y * targetSquishY,
                                                    originalScale.z * targetStretchXZ);

                // 灞忓箷鎶栧姩闅忚搫鍔涘寮猴紙Perlin 鍣０鍐欏叆 CameraOrbit.shakeOffset锛?
                if (cameraOrbit != null && !ShouldSuppressChargeScreenShake())
                {
                    float s = shakeStrength * (0.3f + 0.7f * chargePercent);
                    float sx = (Mathf.PerlinNoise(0, Time.time * 35f) - 0.5f) * 2f * s;
                    float sy = (Mathf.PerlinNoise(Time.time * 35f, 0) - 0.5f) * 2f * s;
                    cameraOrbit.shakeOffset = new Vector3(sx, sy, 0);
                }
                else if (cameraOrbit != null)
                {
                    cameraOrbit.shakeOffset = Vector3.zero;
                }

                // 鎵嬫焺闇囧姩闅忚搫鍔涘寮?
                GamepadRumble.SetVibration(
                    Mathf.Lerp(0.1f, rumbleIntensity, chargePercent),
                    Mathf.Lerp(0.05f, rumbleIntensity * 0.5f, chargePercent));
            }

            if (Input.GetButtonUp("Jump") && isChargingJump)
            {
                float chargePercent = EvaluateChargePercent();
                float launchForce = Mathf.Lerp(minJumpForce, maxJumpForce, chargePercent);

                StartScriptedJumpForce(launchForce);
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

    // ==================== 闃村奖閫昏緫 ====================

    private void UpdateMouseAnimation()
    {
        if (mouseAnimator == null)
            return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool hasMoveInput = new Vector2(h, v).sqrMagnitude > 0.01f;
        bool isJumping = !isGrounded || isApplyingJumpForce || rb.velocity.y > 0.5f;
        bool isMoving = hasMoveInput && !isJumping;
        int targetStateHash = isJumping ? JumpStateHash : (isMoving ? WalkStateHash : IdleStateHash);

        mouseAnimator.SetBool(IsMovingHash, isMoving);
        mouseAnimator.SetBool(IsJumpingHash, isJumping);

        if (currentMouseAnimationHash != targetStateHash)
        {
            mouseAnimator.CrossFade(targetStateHash, 0.08f);
            currentMouseAnimationHash = targetStateHash;
        }
    }

    private void UpdateShadowLogic()
    {
        ResolveShadowManager();
        if (shadowManager == null)
        {
            exposedBySpotlight = false;
            currentLightTimer = 0f;
            wasInShadowLastFrame = true;
            return;
        }

        if (resetGraceTimer > 0f)
        {
            resetGraceTimer -= Time.deltaTime;
            currentLightTimer = 0f;
            wasInShadowLastFrame = true;
            return;
        }

        Vector3 checkPoint = transform.position + Vector3.up * 0.05f;
        GameObject source = shadowManager.GetProjectedShadowSource(checkPoint);
        exposedBySpotlight = SpotlightExposureZone.IsAnyPointExposed(checkPoint);
        bool hasDepthShadowResult = shadowManager.HasDepthResult;
        bool isInDepthShadow = hasDepthShadowResult ? shadowManager.IsPlayerInShadow : source != null;
        if (isInDepthShadow && source == null)
            source = lastActiveShadowSource;
        bool isInProjectedArea = source != null;

        // spotlight 鐓у皠鍖哄煙瑕嗙洊闃村奖鍒ゅ畾锛屽己鍒惰涓洪潪闃村奖
        bool isInShadowNow = isGrounded && isInDepthShadow && !exposedBySpotlight;

        bool isInEdgeZone = false;
        if (isGrounded && !isInProjectedArea && !exposedBySpotlight)
            isInEdgeZone = shadowManager.IsNearProjectedArea(checkPoint, shadowEdgeTolerance);

        bool isSafeForMomentum = (isInShadowNow || isInEdgeZone) && !exposedBySpotlight;

        if (wasInShadowLastFrame && !isSafeForMomentum)
        {
            if (!jumpedThisFlight)
                outOfShadowFrames++;
        }
        else
        {
            outOfShadowFrames = 0;
        }

        if (isInShadowNow)
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

            // 璺宠穬杩囩▼涓笉妫€鏌ヨ窛绂伙紝閬垮厤鍗婄┖绐佺劧鍧犺惤
            if (!jumpedThisFlight && lastActiveShadowSource != null)
            {
                float dist = Vector3.Distance(transform.position, lastActiveShadowSource.transform.position);
                if (dist > maxFollowDistance) shouldReset = true;
            }

            if (shouldReset && !isResetting)
                ExecuteShadowReset();
        }

        wasInShadowLastFrame = isSafeForMomentum;
    }

    private void StripPlatformMomentum()
    {
        // Kept for compatibility; movement no longer uses it to block leaving shadows.
    }

    private void ExecuteShadowReset()
    {
        if (isResetting) return;
        isResetting = true;

        isApplyingJumpForce = false;
        jumpForceRemainingVelocity = 0f;
        rb.velocity = Vector3.zero;
        currentLightTimer = 0f;
        pendingResetShadowSource = null;
        pendingResetTargetPosition = Vector3.zero;

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
        // Prefer the configured fallback shadow for moving shadow casters.
        if (lastActiveShadowSource != null)
        {
            var mover = FindShadowMover(lastActiveShadowSource);
            GameObject fallback = (mover != null) ? mover.GetFallbackTarget() : null;

            if (fallback != null && fallback.activeInHierarchy)
            {
                pendingResetShadowSource = fallback;
                pendingResetTargetPosition = fallback.transform.position;
                return pendingResetTargetPosition;
            }
        }

        // 鍏舵锛氳繑鍥炰笂娆″瓨妗ｇ殑鏈湴瀹夊叏鍧愭爣
        if (hasSafePos && lastActiveShadowSource != null)
        {
            return lastActiveShadowSource.transform.TransformPoint(lastLocalSafePos) + shadowOffset;
        }

        // 鍏滃簳
        return transform.position + Vector3.up * 2f;
    }

    private void OnResetComplete()
    {
        isResetting = false;
        resetGraceTimer = resetGracePeriod;

        if (pendingResetShadowSource != null)
        {
            transform.position = pendingResetTargetPosition;
            lastActiveShadowSource = pendingResetShadowSource;
            lastLocalSafePos = lastActiveShadowSource.transform.InverseTransformPoint(pendingResetTargetPosition);
            hasSafePos = true;
            if (shadowManager != null)
                shadowManager.ForceShadowSource(lastActiveShadowSource);
            pendingResetShadowSource = null;
            pendingResetTargetPosition = Vector3.zero;
        }
        else
        {
            // 鍏堟娴嬪綋鍓嶅疄闄呮墍鍦ㄧ殑 shadow source锛屽垽鏂洖寮瑰埌鐨勬槸 fallback 杩樻槸鍘熷钩鍙?
            Vector3 checkPoint = transform.position + Vector3.up * 0.05f;
            GameObject actualSource = shadowManager != null ? shadowManager.GetProjectedShadowSource(checkPoint) : null;
            bool switchedSource = actualSource != null && actualSource != lastActiveShadowSource;

            if (switchedSource)
            {
                // 鍥炲脊鍒颁簡 fallback 鐩爣 鈫?鍒囨崲璺熻釜婧愶紝涓嶇浣嶇疆
                lastActiveShadowSource = actualSource;
                hasSafePos = false;
                lastLocalSafePos = Vector3.zero;
            }
            else if (hasSafePos && lastActiveShadowSource != null)
            {
                // 鍥炲脊鍥炲師绉诲姩骞冲彴 鈫?鐢ㄥ钩鍙板綋鍓嶄綅缃噸绠楋紝寮ヨˉ DOTween 鏈熼棿骞冲彴鐨勪綅绉?
                Vector3 currentSafePos = lastActiveShadowSource.transform.TransformPoint(lastLocalSafePos) + shadowOffset;
                transform.position = currentSafePos;
            }
        }

        // 鍚屾 Rigidbody 浣嶇疆涓庨€熷害娓呴浂
        rb.position = transform.position;
        rb.velocity = Vector3.zero;
        lastPlatformVelocity = Vector3.zero;

        if (lastActiveShadowSource != null)
            lastSourcePos = lastActiveShadowSource.transform.position;

        currentLightTimer = 0f;
        wasInShadowLastFrame = true;
    }

    // ==================== 绉诲姩鐗╃悊 ====================

    void FixedUpdate()
    {
        if (isResetting) return;
        HandleMovement();
        ApplyScriptedJumpForce();
        ApplyFallGravity();
    }

    private void StartScriptedJumpForce(float totalVelocity)
    {
        float duration = Mathf.Max(0.01f, jumpForceDuration);

        lastPlatformVelocity = Vector3.zero;
        if (lastActiveShadowSource != null)
            lastSourcePos = lastActiveShadowSource.transform.position;
        rb.velocity = new Vector3(0f, 0f, 0f);

        isApplyingJumpForce = true;
        jumpForceElapsed = 0f;
        jumpForceTotalVelocity = Mathf.Max(0f, totalVelocity);
        jumpForceRemainingVelocity = jumpForceTotalVelocity;
        jumpForceCurveArea = EstimateJumpForceCurveArea();
    }

    private void ApplyScriptedJumpForce()
    {
        if (!isApplyingJumpForce)
            return;

        float duration = Mathf.Max(0.01f, jumpForceDuration);
        float t = Mathf.Clamp01(jumpForceElapsed / duration);
        float curveValue = jumpForceCurve != null ? Mathf.Max(0f, jumpForceCurve.Evaluate(t)) : 1f;
        float normalizedCurveArea = Mathf.Max(0.01f, jumpForceCurveArea);
        float velocityThisStep = jumpForceTotalVelocity * curveValue * Time.fixedDeltaTime / (duration * normalizedCurveArea);
        velocityThisStep = Mathf.Min(velocityThisStep, jumpForceRemainingVelocity);

        if (velocityThisStep > 0f)
            rb.AddForce(Vector3.up * velocityThisStep, ForceMode.VelocityChange);

        jumpForceRemainingVelocity -= velocityThisStep;
        jumpForceElapsed += Time.fixedDeltaTime;

        if (jumpForceElapsed >= duration || jumpForceRemainingVelocity <= 0.001f)
        {
            isApplyingJumpForce = false;
        }
    }

    private float EstimateJumpForceCurveArea()
    {
        if (jumpForceCurve == null)
            return 1f;

        const int samples = 12;
        float area = 0f;
        float previous = Mathf.Max(0f, jumpForceCurve.Evaluate(0f));
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float current = Mathf.Max(0f, jumpForceCurve.Evaluate(t));
            area += (previous + current) * 0.5f / samples;
            previous = current;
        }

        return Mathf.Max(0.01f, area);
    }

    private void ApplyFallGravity()
    {
        if (isGrounded || isApplyingJumpForce || rb.velocity.y >= 0f || fallGravityMultiplier <= 1f)
            return;

        rb.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
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
        float activeMoveMultiplier = 1f;

        // === Moving shadow source velocity ===
        Vector3 platformVelocity = Vector3.zero;
        Vector3 platformDisplacement = Vector3.zero;
        if (followMovingShadowSource && isGrounded && wasInShadowLastFrame && lastActiveShadowSource != null)
        {
            Vector3 currentSrcPos = lastActiveShadowSource.transform.position;
            Vector3 displacement = currentSrcPos - lastSourcePos;
            lastSourcePos = currentSrcPos;

            if (displacement.magnitude < 1f)
            {
                platformDisplacement = displacement;
                platformVelocity = displacement / Time.fixedDeltaTime;
            }
        }
        else if (lastActiveShadowSource != null)
        {
            lastSourcePos = lastActiveShadowSource.transform.position;
        }

        if (followMovingShadowSource && isChargingJump && platformDisplacement.sqrMagnitude > 0.0000001f)
        {
            rb.MovePosition(rb.position + platformDisplacement);
            platformVelocity = Vector3.zero;
        }

        lastPlatformVelocity = platformVelocity;

        // === 鏃嬭浆 ===
        if (moveDir.magnitude > 0.1f)
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(moveDir), Time.fixedDeltaTime * turnSpeed));

        // === 鐜╁杈撳叆绉诲姩 ===
        if (isGrounded)
        {
            if (moveDir.magnitude > 0.1f)
            {
                Vector3 nextPos = transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime);
                bool outsideShadow = exposedBySpotlight || (shadowManager != null && !shadowManager.IsPlayerInShadow && !shadowManager.IsInProjectedArea(nextPos));
                activeMoveMultiplier = outsideShadow ? outsideShadowMoveMultiplier : 1f;

                // Target velocity = optional moving shadow velocity + player input velocity.
                Vector3 targetVel = platformVelocity + moveDir * (moveSpeed * activeMoveMultiplier);
                Vector3 currentHoriz = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                Vector3 desiredHoriz = new Vector3(targetVel.x, 0, targetVel.z);
                rb.AddForce((desiredHoriz - currentHoriz) * 10f, ForceMode.Force);
            }
            else
            {
                // No input: only follow the moving shadow source when explicitly enabled.
                rb.velocity = new Vector3(platformVelocity.x, rb.velocity.y, platformVelocity.z);
            }
        }
        else
        {
            if (moveDir.magnitude > 0.1f && !exposedBySpotlight)
                rb.AddForce(moveDir * moveSpeed * 5f, ForceMode.Force);
        }

        // === 閫熷害闄愬埗 ===
        Vector3 horizVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float limit = isGrounded ? moveSpeed * 1.5f : moveSpeed * 1.2f;

        Vector3 platformHorizVel = new Vector3(platformVelocity.x, 0f, platformVelocity.z);
        Vector3 relativeHorizVel = isGrounded ? horizVel - platformHorizVel : horizVel;
        if (relativeHorizVel.magnitude > limit)
        {
            relativeHorizVel = relativeHorizVel.normalized * limit;
            Vector3 finalHorizVel = isGrounded ? platformHorizVel + relativeHorizVel : relativeHorizVel;
            rb.velocity = new Vector3(finalHorizVel.x, rb.velocity.y, finalHorizVel.z);
        }
    }
    public void StopMovementForCinematic()
    {
        isChargingJump = false;
        isApplyingJumpForce = false;
        jumpForceElapsed = 0f;
        jumpForceRemainingVelocity = 0f;
        jumpForceTotalVelocity = 0f;

        if (chargeShakeTween != null && chargeShakeTween.IsActive())
            chargeShakeTween.Kill();
        chargeShakeTween = null;

        if (chargeScaleTween != null && chargeScaleTween.IsActive())
            chargeScaleTween.Kill();
        chargeScaleTween = null;

        if (originalScale != Vector3.zero)
            transform.localScale = originalScale;

        if (cameraOrbit != null)
            cameraOrbit.shakeOffset = Vector3.zero;

        if (mouseAnimator != null)
        {
            mouseAnimator.SetBool(IsMovingHash, false);
            mouseAnimator.SetBool(IsJumpingHash, false);
            mouseAnimator.CrossFade(IdleStateHash, 0.08f);
            currentMouseAnimationHash = IdleStateHash;
        }

        GamepadRumble.Stop();
    }

    private bool ShouldSuppressChargeScreenShake()
    {
        if (lastActiveShadowSource == null)
            return false;

        if (FindShadowMover(lastActiveShadowSource) != null)
            return true;

        MonoBehaviour[] behaviours = lastActiveShadowSource.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IShadowMover)
                return true;
        }

        return false;
    }

    private void ResolveShadowManager()
    {
        if (shadowManager != null)
            return;

        shadowManager = FindObjectOfType<ShadowManager>();
    }

    private IShadowMover FindShadowMover(GameObject source)
    {
        if (source == null)
            return null;

        IShadowMover mover = source.GetComponent<IShadowMover>();
        if (mover != null)
            return mover;

        return source.GetComponentInParent<IShadowMover>();
    }

    private void StopChargeEffects()
    {
        // 鍋滄灞忓箷鎶栧姩
        if (chargeShakeTween != null && chargeShakeTween.IsActive())
            chargeShakeTween.Kill();
        chargeShakeTween = null;

        // 娓呴櫎 CameraOrbit 鐨勬姈鍔ㄥ亸绉?
        if (cameraOrbit != null)
            cameraOrbit.shakeOffset = Vector3.zero;

        // 鍋滄鎵嬫焺闇囧姩
        GamepadRumble.Stop();

        // 缂╂斁寮瑰洖鍘熺姸锛堝甫寮规€э級
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






