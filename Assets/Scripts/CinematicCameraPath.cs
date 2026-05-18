using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class CinematicCameraPath : MonoBehaviour
{
    public enum LookMode { None, AlongPath, Target }

    [Header("路径点")]
    [Tooltip("镜头依次经过的点位（终点为最后一个）")]
    public Transform[] waypoints;

    [Header("路径设置")]
    public PathType pathType = PathType.CatmullRom;
    [Tooltip("沿路径移动的总时长（秒）")]
    public float travelDuration = 3f;
    public Ease travelEase = Ease.InOutCubic;

    [Header("注视")]
    public LookMode lookMode = LookMode.AlongPath;
    [Tooltip("注视路径前方的预判量（仅 AlongPath 模式）")]
    public float lookAhead = 0.08f;
    [Tooltip("注视目标（仅 Target 模式）")]
    public Transform lookAtTarget;

    [Header("收尾")]
    [Tooltip("终点停留时长（秒）")]
    public float holdDuration = 1f;
    [Tooltip("回到玩家视角的时长（秒）")]
    public float returnDuration = 1f;
    public Ease returnEase = Ease.InOutCubic;

    [Header("触发")]
    [Tooltip("只触发一次")]
    public bool triggerOnce = true;

    private bool triggered;
    private Sequence sequence;
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

        // 保存相机原始状态
        camOriginalWorldPos = camTransform.position;
        camOriginalWorldRot = camTransform.rotation;
        camOriginalLocalPos = camTransform.localPosition;
        camOriginalLocalRot = camTransform.localRotation;

        // === 接管控制 ===
        // 禁用玩家：先清零速度再切 kinematic（顺序不能换，kinematic 不能设 velocity）
        playerRb.velocity = Vector3.zero;
        playerRb.isKinematic = true;
        playerController.enabled = false;

        // 禁用相机轨道（保留相机 GameObject 激活，只禁用脚本）
        if (cameraOrbit != null)
            cameraOrbit.enabled = false;

        // === 构建路径动画 ===
        sequence = DOTween.Sequence();

        // 路径点数组（从相机当前位置出发，经过所有 waypoint）
        Vector3[] pathPoints = new Vector3[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
            pathPoints[i] = waypoints[i].position;

        // 沿路径移动
        var pathTween = camTransform.DOPath(pathPoints, travelDuration, pathType)
            .SetEase(travelEase);

        // 注视模式
        switch (lookMode)
        {
            case LookMode.AlongPath:
                pathTween.SetLookAt(lookAhead);
                break;
            case LookMode.Target:
                if (lookAtTarget != null)
                    pathTween.SetLookAt(lookAtTarget);
                break;
        }

        sequence.Append(pathTween);

        // 终点停留
        sequence.AppendInterval(holdDuration);

        // 返回原始位置和朝向
        sequence.Append(camTransform.DOMove(camOriginalWorldPos, returnDuration).SetEase(returnEase));
        sequence.Join(camTransform.DORotateQuaternion(camOriginalWorldRot, returnDuration).SetEase(returnEase));

        // === 收尾 ===
        sequence.OnComplete(RestoreControl);
    }

    void RestoreControl()
    {
        // 恢复相机本地坐标（确保和 CameraOrbit 一致）
        camTransform.localPosition = camOriginalLocalPos;
        camTransform.localRotation = camOriginalLocalRot;

        // 恢复相机轨道
        if (cameraOrbit != null)
            cameraOrbit.enabled = true;

        // 恢复玩家
        playerRb.isKinematic = false;
        playerRb.velocity = Vector3.zero;
        playerController.enabled = true;

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

        // 路径线
        Gizmos.color = Color.cyan;
        Vector3 prev = waypoints[0] != null ? waypoints[0].position : transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Vector3 p = waypoints[i].position;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // 路径点球
        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float radius = (i == waypoints.Length - 1) ? 0.3f : 0.15f;
            Gizmos.DrawSphere(waypoints[i].position, radius);
        }

        // 编号标签（Scene 视图显示）
        Color labelColor = Color.white;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            UnityEditor.Handles.Label(
                waypoints[i].position + Vector3.up * 0.3f,
                i == waypoints.Length - 1 ? $"[{i}] 终点" : $"[{i}]",
                new GUIStyle { normal = new GUIStyleState { textColor = labelColor } }
            );
        }

        // 触发区域（半透明）
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
