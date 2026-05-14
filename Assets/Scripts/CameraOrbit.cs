using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform player;
    public float followSpeed = 15f;
    public float rotateSpeed = 8f;

    [Header("Camera Offset Settings")]
    public float height = 8f;
    public float distance = 8f;
    public float lookOffset = 1f;

    [Header("Occlusion Auto-Pull")]
    public LayerMask occlusionLayer = ~0;
    public float minHeight = 3f;
    public float occlusionSmoothSpeed = 6f;
    public float cameraRadius = 0.3f;

    [HideInInspector] public Vector3 shakeOffset;

    private float targetYRotation = 0f;
    private Transform camTransform;
    private float currentHeight;

    void Start()
    {
        camTransform = Camera.main.transform;
        currentHeight = height;
        UpdateCameraOffset();
        if (player != null) targetYRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O)) targetYRotation -= 90f;
        if (Input.GetKeyDown(KeyCode.P)) targetYRotation += 90f;

        Quaternion targetRot = Quaternion.Euler(0, targetYRotation, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
    }

    void LateUpdate()
    {
        if (player == null) return;

        transform.position = Vector3.Lerp(transform.position, player.position, Time.deltaTime * followSpeed);

        UpdateCameraOffset();

        camTransform.LookAt(player.position + Vector3.up * lookOffset);
    }

    void UpdateCameraOffset()
    {
        // Default height (no occlusion)
        float targetHeight = height;

        // SphereCast from player head toward desired camera world position
        Vector3 playerHead = player.position + Vector3.up * lookOffset;
        Vector3 pivotPos = transform.position;
        Vector3 desiredCamPos = pivotPos + transform.rotation * new Vector3(0, height, -distance);

        Vector3 toCam = desiredCamPos - playerHead;
        float toCamDist = toCam.magnitude;
        if (toCamDist > 0.01f)
        {
            Vector3 toCamDir = toCam / toCamDist;

            if (Physics.SphereCast(playerHead, cameraRadius, toCamDir, out RaycastHit hit, toCamDist, occlusionLayer))
            {
                // Occlusion: lower camera height to look under the obstacle
                float hitDist = hit.distance;
                float blockedRatio = 1f - (hitDist / toCamDist);
                targetHeight = Mathf.Lerp(height, minHeight, blockedRatio);
            }
        }

        // Smoothly transition current height
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * occlusionSmoothSpeed);

        // Apply height and shake offset
        camTransform.localPosition = new Vector3(0, currentHeight, -distance) + shakeOffset;
    }
}
