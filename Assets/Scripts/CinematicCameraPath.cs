using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class CinematicCameraPath : MonoBehaviour
{
    public enum LookMode { None, AlongPath, Target, WaypointTargets }

    [Header("Path Points")]
    [Tooltip("Camera positions in order. The last item is the final cinematic point.")]
    public Transform[] waypoints;

    [Header("Path Settings")]
    public PathType pathType = PathType.CatmullRom;
    [Tooltip("Total travel time along the path, in seconds.")]
    public float travelDuration = 3f;
    public Ease travelEase = Ease.InOutCubic;

    [Header("Look")]
    public LookMode lookMode = LookMode.AlongPath;
    [Tooltip("Look-ahead amount for AlongPath mode.")]
    public float lookAhead = 0.08f;
    [Tooltip("Single look target for Target mode.")]
    public Transform lookAtTarget;
    [Tooltip("WaypointTargets mode: each camera waypoint looks at the matching Transform position.")]
    public Transform[] lookAtPoints;
    [Tooltip("0 snaps to each computed look direction. 1 follows it directly. Values between 0 and 1 ease toward it.")]
    [Range(0f, 1f)]
    public float lookSmoothness = 1f;

    [Header("Finish")]
    [Tooltip("How long to hold on the final point, in seconds.")]
    public float holdDuration = 1f;
    [Tooltip("How long to return to the player camera, in seconds.")]
    public float returnDuration = 1f;
    public Ease returnEase = Ease.InOutCubic;

    [Header("Trigger")]
    [Tooltip("Only trigger this cinematic once.")]
    public bool triggerOnce = true;

    private bool triggered;
    private Sequence sequence;
    private Tween activePathTween;
    private CameraOrbit cameraOrbit;
    private PlayerRbController playerController;
    private Rigidbody playerRb;
    private Transform camTransform;
    private Vector3 camOriginalLocalPos;
    private Quaternion camOriginalLocalRot;
    private Vector3 camOriginalWorldPos;
    private Quaternion camOriginalWorldRot;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered && triggerOnce) return;
        if (waypoints == null || waypoints.Length == 0) return;

        var pc = other.GetComponent<PlayerRbController>();
        if (pc == null) return;

        triggered = true;
        PlayCinematic(pc);
    }

    void PlayCinematic(PlayerRbController pc)
    {
        playerController = pc;
        playerRb = pc.GetComponent<Rigidbody>();

        camTransform = Camera.main.transform;
        cameraOrbit = camTransform.GetComponentInParent<CameraOrbit>();

        camOriginalWorldPos = camTransform.position;
        camOriginalWorldRot = camTransform.rotation;
        camOriginalLocalPos = camTransform.localPosition;
        camOriginalLocalRot = camTransform.localRotation;

        playerRb.velocity = Vector3.zero;
        playerRb.isKinematic = true;
        playerController.enabled = false;

        if (cameraOrbit != null)
            cameraOrbit.enabled = false;

        sequence = DOTween.Sequence();

        Vector3[] pathPoints = new Vector3[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
            pathPoints[i] = waypoints[i].position;

        var pathTween = camTransform.DOPath(pathPoints, travelDuration, pathType)
            .SetEase(travelEase);
        activePathTween = pathTween;

        switch (lookMode)
        {
            case LookMode.AlongPath:
                pathTween.SetLookAt(lookAhead);
                break;
            case LookMode.Target:
                if (lookAtTarget != null)
                    pathTween.SetLookAt(lookAtTarget);
                break;
            case LookMode.WaypointTargets:
                pathTween.OnUpdate(UpdateWaypointTargetLook);
                UpdateWaypointTargetLook();
                break;
        }

        sequence.Append(pathTween);
        sequence.AppendInterval(holdDuration);
        sequence.Append(camTransform.DOMove(camOriginalWorldPos, returnDuration).SetEase(returnEase));
        sequence.Join(camTransform.DORotateQuaternion(camOriginalWorldRot, returnDuration).SetEase(returnEase));
        sequence.OnComplete(RestoreControl);
    }

    void UpdateWaypointTargetLook()
    {
        if (camTransform == null || waypoints == null || waypoints.Length == 0 || lookAtPoints == null || lookAtPoints.Length == 0)
            return;

        Vector3 lookPosition = GetInterpolatedLookPosition(activePathTween != null ? activePathTween.ElapsedPercentage(false) : 0f);
        Vector3 lookDirection = lookPosition - camTransform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        camTransform.rotation = lookSmoothness <= 0f
            ? targetRotation
            : Quaternion.Slerp(camTransform.rotation, targetRotation, lookSmoothness);
    }

    Vector3 GetInterpolatedLookPosition(float normalizedTime)
    {
        int pointCount = Mathf.Min(waypoints.Length, lookAtPoints.Length);
        if (pointCount <= 0)
            return camTransform.position + camTransform.forward;

        if (pointCount == 1)
            return ResolveLookPosition(0);

        float scaledTime = Mathf.Clamp01(normalizedTime) * (pointCount - 1);
        int fromIndex = Mathf.Clamp(Mathf.FloorToInt(scaledTime), 0, pointCount - 1);
        int toIndex = Mathf.Clamp(fromIndex + 1, 0, pointCount - 1);
        float segmentT = scaledTime - fromIndex;

        return Vector3.Lerp(ResolveLookPosition(fromIndex), ResolveLookPosition(toIndex), segmentT);
    }

    Vector3 ResolveLookPosition(int index)
    {
        if (lookAtPoints != null && index >= 0 && index < lookAtPoints.Length && lookAtPoints[index] != null)
            return lookAtPoints[index].position;

        if (waypoints != null && index >= 0 && index < waypoints.Length && waypoints[index] != null)
            return waypoints[index].position + waypoints[index].forward;

        return camTransform.position + camTransform.forward;
    }

    void RestoreControl()
    {
        camTransform.localPosition = camOriginalLocalPos;
        camTransform.localRotation = camOriginalLocalRot;

        if (cameraOrbit != null)
            cameraOrbit.enabled = true;

        playerRb.isKinematic = false;
        playerRb.velocity = Vector3.zero;
        playerController.enabled = true;

        activePathTween = null;
        sequence = null;
    }

    void OnDestroy()
    {
        sequence?.Kill();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = waypoints[0] != null ? waypoints[0].position : transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Vector3 p = waypoints[i].position;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float radius = (i == waypoints.Length - 1) ? 0.3f : 0.15f;
            Gizmos.DrawSphere(waypoints[i].position, radius);
        }

        if (lookAtPoints != null)
        {
            Gizmos.color = Color.green;
            int count = Mathf.Min(waypoints.Length, lookAtPoints.Length);
            for (int i = 0; i < count; i++)
            {
                if (waypoints[i] == null || lookAtPoints[i] == null) continue;
                Gizmos.DrawLine(waypoints[i].position, lookAtPoints[i].position);
                Gizmos.DrawWireSphere(lookAtPoints[i].position, 0.12f);
            }
        }

        Color labelColor = Color.white;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            UnityEditor.Handles.Label(
                waypoints[i].position + Vector3.up * 0.3f,
                i == waypoints.Length - 1 ? $"[{i}] End" : $"[{i}]",
                new GUIStyle { normal = new GUIStyleState { textColor = labelColor } }
            );
        }

        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider bc)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(bc.center, bc.size);
        }
        else if (col is SphereCollider sc)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawSphere(sc.center, sc.radius);
        }
    }
#endif
}
