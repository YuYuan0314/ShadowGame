using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform player;
    public float followSpeed = 15f;
    public float rotateSpeed = 12f;

    [Header("Camera Offset Settings")]
    public float height = 8f;
    public float distance = 8f;
    public float lookOffset = 1f;

    [Header("Free Look")]
    public bool enableMouseLook = true;
    public bool requireMouseButton = false;
    public int mouseButton = 1;
    public float mouseYawSensitivity = 4f;
    public float mouseHeightSensitivity = 0.08f;
    public float stickYawSpeed = 180f;
    public float stickHeightSpeed = 6f;
    public float lookDeadZone = 0.15f;
    public bool invertHeightInput = false;

    [Header("Height Limits")]
    public float minCameraHeight = 3f;
    public float maxCameraHeight = 12f;
    public float heightSmoothSpeed = 10f;

    [Header("Distance Limits")]
    public float minDistance = 4f;
    public float maxDistance = 14f;
    public float mouseWheelDistanceSpeed = 2f;
    public float distanceSmoothSpeed = 10f;

    [Header("Gamepad Zoom")]
    public bool enableGamepadShoulderZoom = true;
    public KeyCode gamepadZoomInButton = KeyCode.JoystickButton5;
    public KeyCode gamepadZoomOutButton = KeyCode.JoystickButton4;
    public float gamepadButtonDistanceSpeed = 5f;

    [Header("Gamepad Axes")]
    public string gamepadLookXAxis = "CameraLookX";
    public string gamepadLookYAxis = "CameraLookY";
    public string fallbackGamepadLookXAxis = "CameraLookXAlt";
    public string fallbackGamepadLookYAxis = "CameraLookYAlt";

    [Header("Vertical Lock")]
    public bool lockVerticalView = true;
    public float lockedCameraHeight = 8f;

    [Header("Zoom View Angle")]
    public bool keepViewAngleWhenZooming = true;
    public float viewAngleReferenceDistance = 0f;

    [HideInInspector] public Vector3 shakeOffset;

    private float targetYRotation;
    private float targetHeight;
    private float currentHeight;
    private float targetDistance;
    private float currentDistance;
    private float runtimeViewAngleReferenceDistance;
    private Transform camTransform;

    void Start()
    {
        camTransform = Camera.main != null ? Camera.main.transform : null;
        targetYRotation = transform.eulerAngles.y;
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        runtimeViewAngleReferenceDistance = viewAngleReferenceDistance > 0f ? viewAngleReferenceDistance : targetDistance;
        targetHeight = GetConfiguredHeight(targetDistance);
        currentHeight = targetHeight;
        currentDistance = targetDistance;
        UpdateCameraOffset();
    }

    void Update()
    {
        HandleLookInput();

        Quaternion targetRot = Quaternion.Euler(0f, targetYRotation, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
    }

    void LateUpdate()
    {
        if (player == null || camTransform == null) return;

        transform.position = Vector3.Lerp(transform.position, player.position, Time.deltaTime * followSpeed);

        UpdateCameraOffset();

        camTransform.LookAt(player.position + Vector3.up * lookOffset);
    }

    void HandleLookInput()
    {
        float mouseX = 0f;
        float mouseY = 0f;
        if (enableMouseLook && (!requireMouseButton || Input.GetMouseButton(mouseButton)))
        {
            mouseX = Input.GetAxisRaw("Mouse X");
            mouseY = Input.GetAxisRaw("Mouse Y");
        }

        float stickX = GetLookAxis(gamepadLookXAxis, fallbackGamepadLookXAxis);
        float stickY = GetLookAxis(gamepadLookYAxis, fallbackGamepadLookYAxis);

        targetYRotation += mouseX * mouseYawSensitivity;
        targetYRotation += stickX * stickYawSpeed * Time.deltaTime;

        if (!lockVerticalView)
        {
            float heightDirection = invertHeightInput ? 1f : -1f;
            targetHeight += mouseY * mouseHeightSensitivity * heightDirection;
            targetHeight += stickY * stickHeightSpeed * heightDirection * Time.deltaTime;
            targetHeight = Mathf.Clamp(targetHeight, minCameraHeight, maxCameraHeight);
        }

        float distanceDelta = 0f;

        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            distanceDelta -= scroll * mouseWheelDistanceSpeed;

        if (enableGamepadShoulderZoom)
        {
            if (Input.GetKey(gamepadZoomInButton))
                distanceDelta -= gamepadButtonDistanceSpeed * Time.deltaTime;

            if (Input.GetKey(gamepadZoomOutButton))
                distanceDelta += gamepadButtonDistanceSpeed * Time.deltaTime;
        }

        if (Mathf.Abs(distanceDelta) > 0.0001f)
        {
            targetDistance = Mathf.Clamp(targetDistance + distanceDelta, minDistance, maxDistance);
            distance = targetDistance;
        }

        if (lockVerticalView)
        {
            targetHeight = GetConfiguredHeight(targetDistance);
        }

        height = targetHeight;
    }

    void UpdateCameraOffset()
    {
        if (camTransform == null) return;

        float desiredDistance = targetDistance;
        float desiredHeight = lockVerticalView ? GetConfiguredHeight(desiredDistance) : targetHeight;
        currentHeight = Mathf.Lerp(currentHeight, desiredHeight, Time.deltaTime * heightSmoothSpeed);

        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * distanceSmoothSpeed);

        camTransform.localPosition = new Vector3(0f, currentHeight, -currentDistance) + shakeOffset;
    }

    float GetConfiguredHeight()
    {
        return GetConfiguredHeight(targetDistance);
    }

    float GetConfiguredHeight(float forDistance)
    {
        float configuredHeight = lockVerticalView ? lockedCameraHeight : height;

        if (lockVerticalView && keepViewAngleWhenZooming)
        {
            float referenceDistance = Mathf.Max(runtimeViewAngleReferenceDistance, 0.01f);
            float heightAboveLookTarget = Mathf.Max(lockedCameraHeight - lookOffset, 0.01f);
            configuredHeight = lookOffset + heightAboveLookTarget * (Mathf.Max(forDistance, 0.01f) / referenceDistance);
            return Mathf.Max(configuredHeight, minCameraHeight);
        }

        return Mathf.Clamp(configuredHeight, minCameraHeight, maxCameraHeight);
    }

    float GetAxisSafe(string axisName)
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

    float GetLookAxis(string primaryAxis, string fallbackAxis)
    {
        float value = ApplyDeadZone(GetAxisSafe(primaryAxis));
        if (Mathf.Abs(value) > 0f)
            return value;

        return ApplyDeadZone(GetAxisSafe(fallbackAxis));
    }

    float ApplyDeadZone(float value)
    {
        return Mathf.Abs(value) >= lookDeadZone ? value : 0f;
    }
}
