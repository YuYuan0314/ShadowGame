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

    [Header("Gamepad Axes")]
    public string gamepadLookXAxis = "CameraLookX";
    public string gamepadLookYAxis = "CameraLookY";
    public string fallbackGamepadLookXAxis = "CameraLookXAlt";
    public string fallbackGamepadLookYAxis = "CameraLookYAlt";

    [Header("Occlusion Auto-Pull")]
    public LayerMask occlusionLayer = ~0;
    public float occlusionMinHeight = 3f;
    public float occlusionSmoothSpeed = 6f;
    public float cameraRadius = 0.3f;

    [HideInInspector] public Vector3 shakeOffset;

    private float targetYRotation;
    private float targetHeight;
    private float currentHeight;
    private float targetDistance;
    private float currentDistance;
    private Transform camTransform;

    void Start()
    {
        camTransform = Camera.main != null ? Camera.main.transform : null;
        targetYRotation = transform.eulerAngles.y;
        targetHeight = Mathf.Clamp(height, minCameraHeight, maxCameraHeight);
        currentHeight = targetHeight;
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
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

        float heightDirection = invertHeightInput ? 1f : -1f;
        targetHeight += mouseY * mouseHeightSensitivity * heightDirection;
        targetHeight += stickY * stickHeightSpeed * heightDirection * Time.deltaTime;
        targetHeight = Mathf.Clamp(targetHeight, minCameraHeight, maxCameraHeight);
        height = targetHeight;

        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetDistance = Mathf.Clamp(targetDistance - scroll * mouseWheelDistanceSpeed, minDistance, maxDistance);
            distance = targetDistance;
        }
    }

    void UpdateCameraOffset()
    {
        if (camTransform == null) return;

        float occludedHeight = targetHeight;
        float desiredDistance = targetDistance;

        if (player != null)
        {
            Vector3 playerHead = player.position + Vector3.up * lookOffset;
            Vector3 pivotPos = transform.position;
            Vector3 desiredCamPos = pivotPos + transform.rotation * new Vector3(0f, targetHeight, -desiredDistance);

            Vector3 toCam = desiredCamPos - playerHead;
            float toCamDist = toCam.magnitude;
            if (toCamDist > 0.01f)
            {
                Vector3 toCamDir = toCam / toCamDist;

                if (Physics.SphereCast(playerHead, cameraRadius, toCamDir, out RaycastHit hit, toCamDist, occlusionLayer))
                {
                    float blockedRatio = 1f - (hit.distance / toCamDist);
                    occludedHeight = Mathf.Lerp(targetHeight, occlusionMinHeight, blockedRatio);
                    desiredDistance = Mathf.Lerp(targetDistance, Mathf.Max(minDistance, hit.distance - cameraRadius), blockedRatio);
                }
            }
        }

        currentHeight = Mathf.Lerp(currentHeight, occludedHeight, Time.deltaTime * Mathf.Max(heightSmoothSpeed, occlusionSmoothSpeed));
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * distanceSmoothSpeed);

        camTransform.localPosition = new Vector3(0f, currentHeight, -currentDistance) + shakeOffset;
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
